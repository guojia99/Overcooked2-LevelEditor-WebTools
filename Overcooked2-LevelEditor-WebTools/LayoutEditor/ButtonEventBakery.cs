using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;
using LevelEditorStub;

/// <summary>
/// 按钮事件组烘焙。在 Design/Button Event Logic 下创建隐藏逻辑物体
/// （Animator + 生成的 controller + TriggerOnAnimator done 中继），全部用宿主原语实现：
///  - 顺序广播：状态环 Ready_g --按压--> Fire_g（进入即用 SendTriggerToObject 向组内
///    每个事件目标广播其触发消息）--完成--> Ready_{g+1 mod n}（最后一组后循环回第一组）；
///  - 完成门控：事件可配 doneTrigger（目标完成事件时广播的触发名）；组内全部带
///    doneTrigger 的事件完成（AND 门）后才接受下一次按压；未配 doneTrigger 的事件
///    视为立即完成（整组无 done 事件时 Fire 态 0 秒自动过渡）；
///  - 运行期锁定：Fire/等待态挂 ClearTriggerDuringState 吞掉按压（不锁存）。
/// 事件目标为伪根 GameObject（SendTriggerToObject 按名字查找），目标 child 上的
/// 监听字段（m_switchTrigger/m_workTrigger）与 done 中继由
/// LayoutEditorSwitchLinkPatch 在 Play 期接线；done 中继配置以
/// LayoutEditorEventDoneRelay 组件写到目标伪根（随场景保存）。
/// 场景是唯一事实源：ImportFromScene 从 helper 接线重建文档数据。
/// </summary>
public static class ButtonEventBakery
{
    public const string ButtonEventRootPath = "Design/Button Event Logic";
    public const string HelperPrefix = "BtnEvt_";
    public const string ImportedMarker = "scene:BtnEvt:";
    /// <summary>无操作 trigger：压力开关的 Exit 事件必须指向有效参数（同按钮联动约定）。</summary>
    public const string NoopTrigger = "BENoop";

    private static string Hash8(string s)
    {
        using (var md5 = System.Security.Cryptography.MD5.Create())
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(s ?? "");
            var hash = md5.ComputeHash(bytes);
            var sb = new System.Text.StringBuilder(8);
            for (int i = 0; i < 4; i++) sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }
    }

    /// <summary>事件组的 helper 名（对回导 link 幂等：已含前缀则原样返回）。</summary>
    private static string HelperNameFor(LayoutButtonEventLinkDto link)
    {
        var id = link != null ? link.id ?? "" : "";
        if (id.StartsWith(ImportedMarker, StringComparison.Ordinal))
            return id.Substring(ImportedMarker.Length);
        return HelperPrefix + Hash8(id);
    }

    private static string PressTrigger(string helperName) { return "BEP_" + helperName; }
    private static string DoneTrigger(string helperName, int g, int i)
    {
        return "BED_" + helperName + "_" + g + "_" + i;
    }

    // ---------------------------------------------------------------- resolve

    private static GameObject ResolveObject(string id, Dictionary<string, GameObject> createdObjects)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (id.StartsWith("u:", StringComparison.Ordinal))
        {
            int iid;
            if (int.TryParse(id.Substring(2), out iid))
                return EditorUtility.InstanceIDToObject(iid) as GameObject;
            return null;
        }
        GameObject go;
        if (createdObjects != null && createdObjects.TryGetValue(id, out go))
            return go;
        return null;
    }

    // ---------------------------------------------------------------- bake

    public static string Sync(Scene scene, LayoutDocumentDto doc, Dictionary<string, GameObject> createdObjects)
    {
        try
        {
            return SyncInner(scene, doc, createdObjects);
        }
        catch (Exception e)
        {
            LayoutEditorLog.LogWarning("button event: bake exception: " + e);
            return "按钮事件组写回异常：" + e.Message;
        }
    }

    private static string SyncInner(Scene scene, LayoutDocumentDto doc, Dictionary<string, GameObject> createdObjects)
    {
        var sceneName = Path.GetFileNameWithoutExtension(scene.path);
        var animDir = AnimGroupBakery.GetAnimationsFolder(scene.path);
        var errors = new List<string>();
        var usedHelpers = new HashSet<string>(StringComparer.Ordinal);
        var usedAssets = new HashSet<string>(StringComparer.Ordinal);

        var links = doc != null && doc.buttonEvents != null && doc.buttonEvents.links != null
            ? doc.buttonEvents.links
            : new LayoutButtonEventLinkDto[0];

        if (links.Length > 0)
            AnimGroupBakery.EnsureFolder(animDir);

        foreach (var link in links)
        {
            if (link == null || string.IsNullOrEmpty(link.sourceId)) continue;
            if (link.groups == null || link.groups.Length == 0) continue;
            var err = BakeLink(scene, link, animDir, sceneName, createdObjects, usedHelpers, usedAssets);
            if (!string.IsNullOrEmpty(err)) errors.Add(err);
        }

        CleanupStale(animDir, sceneName, usedHelpers, usedAssets);
        AssetDatabase.SaveAssets();

        if (errors.Count > 0)
            LayoutEditorLog.LogWarning("button event: bake errors: " + string.Join("; ", errors.ToArray()));
        return errors.Count > 0 ? string.Join("; ", errors.ToArray()) : null;
    }

    private static string BakeLink(Scene scene, LayoutButtonEventLinkDto link,
        string animDir, string sceneName, Dictionary<string, GameObject> createdObjects,
        HashSet<string> usedHelpers, HashSet<string> usedAssets)
    {
        var helperName = HelperNameFor(link);
        var sourceGo = ResolveObject(link.sourceId, createdObjects);
        if (sourceGo == null)
            return "按钮事件组：找不到触发源 " + link.sourceId;

        var helper = EnsureHelper(helperName);
        if (helper == null)
            return "按钮事件组：无法创建 " + ButtonEventRootPath + "/" + helperName;
        usedHelpers.Add(helperName);

        // 解析事件目标（伪根）：缺失的目标整条联动视为失败
        int n = link.groups.Length;
        var eventTargets = new List<List<GameObject>>();
        var eventTriggers = new List<List<string>>();
        var eventDones = new List<List<string>>();
        var targetNames = new List<List<string>>();
        for (int g = 0; g < n; g++)
        {
            var group = link.groups[g];
            var targets = new List<GameObject>();
            var trigs = new List<string>();
            var dones = new List<string>();
            var names = new List<string>();
            eventTargets.Add(targets);
            eventTriggers.Add(trigs);
            eventDones.Add(dones);
            targetNames.Add(names);
            if (group == null || group.events == null) continue;
            for (int i = 0; i < group.events.Length; i++)
            {
                var ev = group.events[i];
                if (ev == null) continue;
                var target = ResolveObject(ev.targetId, createdObjects);
                if (target == null)
                    return "按钮事件组：事件目标无法解析（" + ev.targetId + "）";
                targets.Add(target);
                trigs.Add(string.IsNullOrEmpty(ev.trigger) ? "Switch" : ev.trigger);
                dones.Add(string.IsNullOrEmpty(ev.doneTrigger) ? "" : ev.doneTrigger);
                names.Add(target.name);
            }
        }

        // done 触发名（helper 监听）
        var doneNames = new List<List<string>>();
        for (int g = 0; g < n; g++)
        {
            var row = new List<string>();
            doneNames.Add(row);
            for (int i = 0; i < eventDones[g].Count; i++)
                row.Add(eventDones[g][i] != "" ? DoneTrigger(helperName, g, i) : "");
        }

        var controllerPath = animDir + "/" + sceneName + "_" + helperName + ".controller";
        var controller = BuildController(controllerPath, helperName, doneNames, targetNames, eventTriggers);
        if (controller == null)
            return "按钮事件组：controller 创建失败 " + controllerPath;
        usedAssets.Add(controllerPath);

        var anim = AttachHelperAnimator(helper, controller);
        // helper 上的 done 中继（目标广播 doneTrigger → 状态机触发器）
        RebuildDoneRelays(helper.gameObject, anim, doneNames);
        // 目标伪根上的 done 中继配置（Play 期由 LayoutEditorSwitchLinkPatch 挂到 child）
        WriteTargetDoneRelays(helper.gameObject, helperName, eventTargets, eventDones, doneNames);
        WireSource(sourceGo, PressTrigger(helperName), anim);
        return null;
    }

    // ---------------------------------------------------------------- components

    private static Transform EnsureHelper(string helperName)
    {
        var t = LayoutEditorHierarchy.FindOrCreatePath(ButtonEventRootPath + "/" + helperName);
        if (t != null)
        {
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
        }
        return t;
    }

    private static Animator AttachHelperAnimator(Transform helper, AnimatorController controller)
    {
        var go = helper.gameObject;
        var anim = go.GetComponent<Animator>();
        if (anim == null)
            anim = Undo.AddComponent<Animator>(go);
        else
            Undo.RecordObject(anim, "Layout Editor Button Event");
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;
        EditorUtility.SetDirty(anim);
        return anim;
    }

    /// <summary>重建 helper 上的 done 消息 → Animator trigger 中继。</summary>
    private static void RebuildDoneRelays(GameObject helper, Animator anim, List<List<string>> doneNames)
    {
        foreach (var old in helper.GetComponents<TriggerOnAnimator>())
            Undo.DestroyObjectImmediate(old);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int g = 0; g < doneNames.Count; g++)
        {
            for (int i = 0; i < doneNames[g].Count; i++)
            {
                var t = doneNames[g][i];
                if (string.IsNullOrEmpty(t) || !seen.Add(t)) continue;
                var relay = Undo.AddComponent<TriggerOnAnimator>(helper);
                relay.m_triggerToReceive = t;
                relay.m_triggerToFire = t;
                relay.m_targetAnimator = anim;
                relay.m_triggerToFireHash = Animator.StringToHash(t);
                EditorUtility.SetDirty(relay);
            }
        }
    }

    /// <summary>事件目标伪根上的 done 中继配置（Play 期挂到 child）。先清理本 helper
    ///  名下旧配置（forwardTrigger 前缀匹配），再按当前事件组写入。</summary>
    private static void WriteTargetDoneRelays(GameObject helper, string helperName,
        List<List<GameObject>> eventTargets, List<List<string>> eventDones, List<List<string>> doneNames)
    {
        var prefix = "BED_" + helperName + "_";
        var touched = new HashSet<GameObject>();
        for (int g = 0; g < eventTargets.Count; g++)
        {
            for (int i = 0; i < eventTargets[g].Count; i++)
            {
                var target = eventTargets[g][i];
                if (target == null) continue;
                if (touched.Add(target))
                {
                    var old = target.GetComponents<LayoutEditorEventDoneRelay>();
                    if (old != null)
                    {
                        for (int r = 0; r < old.Length; r++)
                        {
                            if (old[r] != null && !string.IsNullOrEmpty(old[r].m_forwardTrigger) &&
                                old[r].m_forwardTrigger.StartsWith(prefix, StringComparison.Ordinal))
                                Undo.DestroyObjectImmediate(old[r]);
                        }
                    }
                }
                var done = eventDones[g][i];
                var doneName = doneNames[g][i];
                if (string.IsNullOrEmpty(done) || string.IsNullOrEmpty(doneName))
                    continue;
                var relay = Undo.AddComponent<LayoutEditorEventDoneRelay>(target);
                relay.m_listenTrigger = done;
                relay.m_forwardTrigger = doneName;
                relay.m_forwardTo = helper;
                EditorUtility.SetDirty(relay);
            }
        }
    }

    /// <summary>写触发源 stub：Switch 用 triggerOnAnimator/animatorToTrigger（按压驱动
    ///  逻辑 Animator）；PressureSwitch 用 triggerOnAnimatorEnter（Exit 指向 BENoop 占位）。</summary>
    private static void WireSource(GameObject sourceGo, string pressTrigger, Animator targetAnim)
    {
        var sw = sourceGo.GetComponent<PseudoPrefabSwitchStub>();
        if (sw != null)
        {
            if (!string.IsNullOrEmpty(sw.triggerOnAnimator) && sw.animatorToTrigger != null &&
                sw.animatorToTrigger != targetAnim)
                LayoutEditorLog.LogWarning("button event: 触发源 " + sourceGo.name +
                    " 同时绑定了动画组联动与事件组，后者覆盖前者接线");
            Undo.RecordObject(sw, "Layout Editor Button Event");
            sw.triggerOnAnimator = pressTrigger;
            sw.animatorToTrigger = targetAnim;
            EditorUtility.SetDirty(sw);
            return;
        }
        var ps = sourceGo.GetComponent<PseudoPrefabPressureSwitchStub>();
        if (ps != null)
        {
            Undo.RecordObject(ps, "Layout Editor Button Event");
            ps.triggerOnAnimatorEnter = pressTrigger;
            ps.triggerOnAnimatorExit = NoopTrigger;
            ps.animatorToTrigger = targetAnim;
            EditorUtility.SetDirty(ps);
            return;
        }
        LayoutEditorLog.LogWarning("button event: 触发源 " + sourceGo.name +
            " 上没有 Switch/PressureSwitch stub，无法接线");
    }

    // ---------------------------------------------------------------- controller

    private static AnimatorStateTransition AddTrigTransition(AnimatorState from, AnimatorState to, string trigger)
    {
        var tr = from.AddTransition(to);
        tr.hasExitTime = false;
        tr.duration = 0f;
        tr.hasFixedDuration = true;
        tr.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        return tr;
    }

    private static AnimatorStateTransition AddAutoTransition(AnimatorState from, AnimatorState to)
    {
        var tr = from.AddTransition(to);
        tr.hasExitTime = true;
        tr.exitTime = 0f;
        tr.duration = 0f;
        tr.hasFixedDuration = true;
        return tr;
    }

    private static void AddSendTrigger(AnimatorState st, string objectName, string triggerToSend)
    {
        var smb = st.AddStateMachineBehaviour<SendTriggerToObject>();
        smb.name = "Send_" + triggerToSend;
        var so = new SerializedObject(smb);
        SetString(so, "m_objectName", objectName);
        SetString(so, "m_triggerToSend", triggerToSend);
        var tt = so.FindProperty("m_triggerTime");
        if (tt != null) tt.floatValue = 0f;
        var oe = so.FindProperty("m_orTriggerOnExit");
        if (oe != null) oe.boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddClearTrigger(AnimatorState st, string triggerName)
    {
        var smb = st.AddStateMachineBehaviour<ClearTriggerDuringState>();
        smb.name = "Clear_" + triggerName;
        var so = new SerializedObject(smb);
        SetString(so, "m_triggerName", triggerName);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetString(SerializedObject so, string prop, string value)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.stringValue = value;
    }

    private static AnimatorState NewState(AnimatorStateMachine sm, string name)
    {
        var st = sm.AddState(name);
        st.writeDefaultValues = false;
        return st;
    }

    /// <summary>事件组状态机：Ready_g --按压--> Fire_g（广播整组事件）→ 组内 done
    ///  AND 门 → Ready_{g+1 mod n}；整组无 done 事件时 Fire 态 0 秒自动过渡；
    ///  Fire/等待态吞按压（完成前不可再按）。</summary>
    private static AnimatorController BuildController(string path, string helperName,
        List<List<string>> doneNames, List<List<string>> targetNames, List<List<string>> eventTriggers)
    {
        AnimGroupBakery.DeleteAssetIfExists(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        if (ctrl == null) return null;
        ctrl.name = Path.GetFileNameWithoutExtension(path);

        var press = PressTrigger(helperName);
        ctrl.AddParameter(press, AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter(NoopTrigger, AnimatorControllerParameterType.Trigger);
        var seenDone = new HashSet<string>(StringComparer.Ordinal);
        for (int g = 0; g < doneNames.Count; g++)
        {
            for (int i = 0; i < doneNames[g].Count; i++)
            {
                var t = doneNames[g][i];
                if (!string.IsNullOrEmpty(t) && seenDone.Add(t))
                    ctrl.AddParameter(t, AnimatorControllerParameterType.Trigger);
            }
        }

        var sm = ctrl.layers[0].stateMachine;
        int n = targetNames.Count;
        var ready = new AnimatorState[n];
        var fire = new AnimatorState[n];
        for (int g = 0; g < n; g++)
        {
            ready[g] = NewState(sm, "Ready_" + g);
            fire[g] = NewState(sm, "Fire_" + g);
        }
        sm.defaultState = ready[0];

        for (int g = 0; g < n; g++)
        {
            int next = (g + 1) % n;
            AddTrigTransition(ready[g], fire[g], press);
            for (int i = 0; i < eventTriggers[g].Count; i++)
                AddSendTrigger(fire[g], targetNames[g][i], eventTriggers[g][i]);

            // 本组需要等待的 done 触发器（去重）
            var wait = new List<string>();
            for (int i = 0; i < doneNames[g].Count; i++)
            {
                var t = doneNames[g][i];
                if (!string.IsNullOrEmpty(t) && !wait.Contains(t))
                    wait.Add(t);
            }

            if (wait.Count == 0)
            {
                AddAutoTransition(fire[g], ready[next]);
            }
            else if (wait.Count == 1)
            {
                AddTrigTransition(fire[g], ready[next], wait[0]);
            }
            else if (wait.Count == 2)
            {
                var w0 = NewState(sm, "Wait_" + g + "_0");
                var w1 = NewState(sm, "Wait_" + g + "_1");
                AddTrigTransition(fire[g], w0, wait[0]);
                AddTrigTransition(w0, ready[next], wait[1]);
                AddTrigTransition(fire[g], w1, wait[1]);
                AddTrigTransition(w1, ready[next], wait[0]);
                AddClearTrigger(w0, press);
                AddClearTrigger(w1, press);
            }
            else
            {
                // 一般化 AND 门（k>=3）：每收到一个 done 前进一级（容忍重复触发）。
                var waitStates = new AnimatorState[wait.Count - 1];
                for (int r = 1; r < wait.Count; r++)
                    waitStates[r - 1] = NewState(sm, "Wait_" + g + "_" + r);
                for (int j = 0; j < wait.Count; j++)
                    AddTrigTransition(fire[g], waitStates[0], wait[j]);
                for (int r = 1; r < waitStates.Length; r++)
                {
                    for (int j = 0; j < wait.Count; j++)
                        AddTrigTransition(waitStates[r - 1], waitStates[r], wait[j]);
                }
                for (int j = 0; j < wait.Count; j++)
                    AddTrigTransition(waitStates[waitStates.Length - 1], ready[next], wait[j]);
                for (int r = 0; r < waitStates.Length; r++)
                    AddClearTrigger(waitStates[r], press);
            }
            // 运行期锁定：组完成前按压被吞掉且不锁存
            AddClearTrigger(fire[g], press);
        }
        AddClearTrigger(ready[0], NoopTrigger);
        return ctrl;
    }

    // ------------------------------------------------------------------- cleanup

    private static void CleanupStale(string animDir, string sceneName,
        HashSet<string> usedHelpers, HashSet<string> usedAssets)
    {
        var root = LayoutEditorHierarchy.FindByPath(ButtonEventRootPath);
        if (root != null)
        {
            var stale = new List<GameObject>();
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (!usedHelpers.Contains(c.name))
                    stale.Add(c.gameObject);
            }
            foreach (var go in stale)
            {
                LayoutEditorLog.Log("button event: cleanup stale helper " + go.name);
                Undo.DestroyObjectImmediate(go);
            }
            if (root.childCount == 0 && stale.Count > 0)
                Undo.DestroyObjectImmediate(root.gameObject);
        }

        if (!AssetDatabase.IsValidFolder(animDir)) return;
        foreach (var guid in AssetDatabase.FindAssets("t:Object", new[] { animDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
            var file = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrEmpty(file)) continue;
            if (!file.StartsWith(sceneName + "_BtnEvt", StringComparison.Ordinal)) continue;
            if (usedAssets.Contains(path)) continue;
            AssetDatabase.DeleteAsset(path);
        }
    }

    // ------------------------------------------------------------------- import

    /// <summary>从场景重建按钮事件组（场景是唯一事实源）：扫描 Design/Button Event Logic
    ///  下的 helper，按 controller 状态里的 SendTriggerToObject 反查事件目标，按目标伪根
    ///  上的 LayoutEditorEventDoneRelay 反查完成触发器，按 stub 接线反查触发源。</summary>
    public static List<LayoutButtonEventLinkDto> ImportFromScene(Scene scene, List<LayoutItemDto> items)
    {
        var result = new List<LayoutButtonEventLinkDto>();
        if (!scene.IsValid()) return result;
        var root = LayoutEditorHierarchy.FindByPath(ButtonEventRootPath);
        if (root == null) return result;

        for (int h = 0; h < root.childCount; h++)
        {
            var helper = root.GetChild(h);
            var anim = helper.GetComponent<Animator>();
            var ctrl = anim != null ? anim.runtimeAnimatorController as AnimatorController : null;
            if (ctrl == null || ctrl.layers.Length == 0) continue;
            var sm = ctrl.layers[0].stateMachine;
            if (sm == null) continue;
            var helperName = helper.name;
            if (!helperName.StartsWith(HelperPrefix, StringComparison.Ordinal)) continue;

            var link = ImportLink(helperName, helper.gameObject, anim, sm, items);
            if (link != null)
                result.Add(link);
        }
        return result;
    }

    private static LayoutButtonEventLinkDto ImportLink(string helperName, GameObject helperGo,
        Animator anim, AnimatorStateMachine sm, List<LayoutItemDto> items)
    {
        // Fire_g 状态按序号排列
        var fireStates = new SortedDictionary<int, AnimatorState>();
        foreach (var cs in sm.states)
        {
            var st = cs.state;
            if (st == null || !st.name.StartsWith("Fire_", StringComparison.Ordinal)) continue;
            int idx;
            if (!int.TryParse(st.name.Substring(5), out idx)) continue;
            fireStates[idx] = st;
        }
        if (fireStates.Count == 0) return null;

        var groups = new List<LayoutButtonEventGroupDto>();
        int g = 0;
        foreach (var kv in fireStates)
        {
            var sends = ReadSendTriggers(kv.Value);
            if (sends.Count == 0) return null; // Fire 态无事件 → 联动失效
            var events = new List<LayoutButtonEventDto>();
            for (int i = 0; i < sends.Count; i++)
            {
                var targetItem = FindItemByName(items, sends[i].Key);
                if (targetItem == null) return null; // 目标已删 → 联动失效
                var doneName = DoneTrigger(helperName, g, i);
                events.Add(new LayoutButtonEventDto
                {
                    targetId = targetItem.instanceId,
                    trigger = sends[i].Value,
                    doneTrigger = FindDoneTrigger(targetItem, doneName)
                });
            }
            groups.Add(new LayoutButtonEventGroupDto
            {
                id = ImportedMarker + helperName + ":g" + g,
                events = events.ToArray()
            });
            g++;
        }

        var sourceId = FindSourceId(items, anim, PressTrigger(helperName));
        if (string.IsNullOrEmpty(sourceId))
        {
            LayoutEditorLog.LogWarning("button event: helper " + helperName + " 找不到触发源，跳过导入");
            return null;
        }

        return new LayoutButtonEventLinkDto
        {
            id = ImportedMarker + helperName,
            sourceId = sourceId,
            groups = groups.ToArray()
        };
    }

    private static string FindDoneTrigger(LayoutItemDto item, string doneName)
    {
        if (item == null || string.IsNullOrEmpty(item.instanceId)) return "";
        if (!item.instanceId.StartsWith("u:", StringComparison.Ordinal)) return "";
        int id;
        if (!int.TryParse(item.instanceId.Substring(2), out id)) return "";
        var go = EditorUtility.InstanceIDToObject(id) as GameObject;
        if (go == null) return "";
        var relays = go.GetComponents<LayoutEditorEventDoneRelay>();
        if (relays == null) return "";
        for (int r = 0; r < relays.Length; r++)
        {
            if (relays[r] != null && relays[r].m_forwardTrigger == doneName)
                return relays[r].m_listenTrigger ?? "";
        }
        return "";
    }

    private static LayoutItemDto FindItemByName(List<LayoutItemDto> items, string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return null;
        // 优先 hierarchyPath 精确匹配（唯一）；displayName 兜底（历史场景可能
        // 存在同名兄弟，此时取第一个匹配，与 GameObject.Find 行为一致）
        LayoutItemDto fallback = null;
        foreach (var item in items ?? new List<LayoutItemDto>())
        {
            if (item == null || string.IsNullOrEmpty(item.instanceId))
                continue;
            var path = item.hierarchyPath ?? "";
            if (path.EndsWith("/" + objectName, StringComparison.Ordinal))
                return item;
            if (fallback == null && item.displayName == objectName)
                fallback = item;
        }
        return fallback;
    }

    /// <summary>读取状态上所有 SendTriggerToObject 的 (目标名, 触发名)（私有字段走
    ///  SerializedObject；Unity 2017 无公开 behaviours API）。</summary>
    private static List<KeyValuePair<string, string>> ReadSendTriggers(AnimatorState st)
    {
        var sends = new List<KeyValuePair<string, string>>();
        var so = new SerializedObject(st);
        var behaviours = so.FindProperty("m_Behaviours");
        if (behaviours == null || !behaviours.isArray) return sends;
        for (int i = 0; i < behaviours.arraySize; i++)
        {
            var bref = behaviours.GetArrayElementAtIndex(i).objectReferenceValue;
            var send = bref as SendTriggerToObject;
            if (send == null) continue;
            var bso = new SerializedObject(send);
            var on = bso.FindProperty("m_objectName");
            var tp = bso.FindProperty("m_triggerToSend");
            if (tp != null && !string.IsNullOrEmpty(tp.stringValue))
                sends.Add(new KeyValuePair<string, string>(
                    on != null ? on.stringValue : "", tp.stringValue));
        }
        return sends;
    }

    /// <summary>反查触发源：stub 的 animatorToTrigger 指向该 helper 且触发名匹配。</summary>
    private static string FindSourceId(List<LayoutItemDto> items, Animator anim, string pressTrigger)
    {
        foreach (var item in items ?? new List<LayoutItemDto>())
        {
            if (item == null || string.IsNullOrEmpty(item.instanceId)) continue;
            if (!item.instanceId.StartsWith("u:", StringComparison.Ordinal)) continue;
            int id;
            if (!int.TryParse(item.instanceId.Substring(2), out id)) continue;
            var go = EditorUtility.InstanceIDToObject(id) as GameObject;
            if (go == null) continue;
            var sw = go.GetComponent<PseudoPrefabSwitchStub>();
            if (sw != null && sw.animatorToTrigger == anim && sw.triggerOnAnimator == pressTrigger)
                return item.instanceId;
            var ps = go.GetComponent<PseudoPrefabPressureSwitchStub>();
            if (ps != null && ps.animatorToTrigger == anim && ps.triggerOnAnimatorEnter == pressTrigger)
                return item.instanceId;
        }
        return null;
    }
}
