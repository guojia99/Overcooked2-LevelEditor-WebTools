using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;
using LevelEditorStub;

/// <summary>
/// 按钮/压力开关 ↔ 移动组联动烘焙。在 Design/Button Logic/Btn(Logic|Pair)_*&lt;key&gt; 下创建
/// 隐藏逻辑物体（Animator + 生成的 controller + TriggerOnAnimator 中继），全部用宿主原语实现：
///  - 顺序触发：状态环 Ready_i --Advance--> Run_i（进入即用 SendTriggerToObject 启动组 i）
///    --Done_i--> Ready_{i+1 mod n}（最后一组后循环回第一组）；
///  - 运行期锁定（lockUntilFinished）：Run_i 无 Advance 出口且挂 ClearTriggerDuringState，
///    组运行期间的按压被忽略且不会锁存（"移动组完成后才可再按"）；
///  - 共轭对（一对一，每方至多 2 组）：AReady→ARun→(两组均完成的 AND 门)→BReady→BRun→…→
///    AReady 状态环；ARun 进入时同时启动 A 方各组，全部完成后对方才可按，反之亦然。
/// 场景是唯一事实源：ImportFromScene 从 helper 接线重建文档数据（按钮 stub 的
/// animatorToTrigger / triggerOnAnimator(Enter) 指向 helper 的 Animator）。
/// </summary>
public static class ButtonLinkBakery
{
    public const string ButtonLogicRootPath = "Design/Button Logic";
    public const string HelperSeqPrefix = "BtnLogic_";
    public const string HelperSeqNoLockPrefix = "BtnLogicNL_";
    public const string HelperPairPrefix = "BtnPair_";
    /// <summary>无操作 trigger：压力开关的 Exit 事件必须指向有效参数，否则宿主
    ///  PseudoPrefabPressureSwitch.Setup 会装一个空名 TriggerOnAnimator。参数存在但无过渡消费。</summary>
    public const string NoopTrigger = "BLNoop";
    /// <summary>共轭模式下单个按钮最多绑定的移动组数。</summary>
    public const int PairGroupLimit = 2;

    private const string ImportedSeqMarker = "scene:BtnLogic:";
    private const string ImportedPairMarker = "scene:BtnPair:";

    /// <summary>该路径是否为按钮联动逻辑控制器资产（MoveControlBakery.CleanupStale 据此跳过；
    ///  _BtnEvt 为按钮事件组资产，见 ButtonEventBakery）。</summary>
    public static bool IsButtonLogicAsset(string normalizedAssetPath)
    {
        if (string.IsNullOrEmpty(normalizedAssetPath)) return false;
        return normalizedAssetPath.Contains("_BtnLogic") || normalizedAssetPath.Contains("_BtnPair")
            || normalizedAssetPath.Contains("_BtnEvt");
    }

    // ------------------------------------------------------------------ naming

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

    /// <summary>顺序联动的 helper 名（对回导 link 幂等：已含前缀则原样返回）。</summary>
    private static string HelperNameForSeq(LayoutButtonLinkDto link)
    {
        var id = link != null ? link.id ?? "" : "";
        if (id.StartsWith(ImportedSeqMarker, StringComparison.Ordinal))
            return id.Substring(ImportedSeqMarker.Length);
        var key = Hash8(id);
        return (link != null && !link.lockUntilFinished ? HelperSeqNoLockPrefix : HelperSeqPrefix) + key;
    }

    private static string HelperNameForPair(string pairId)
    {
        pairId = pairId ?? "";
        if (pairId.StartsWith(ImportedPairMarker, StringComparison.Ordinal))
            return pairId.Substring(ImportedPairMarker.Length);
        return HelperPairPrefix + Hash8(pairId);
    }

    private static string AdvanceTrigger(string helperName) { return "BLAdv_" + helperName; }
    private static string PressTrigger(string helperName, string side) { return "BLP_" + helperName + "_" + side; }
    private static string GoTrigger(string helperName, string side, int i)
    {
        return string.IsNullOrEmpty(side)
            ? "BLGo_" + helperName + "_" + i
            : "BLGo_" + helperName + "_" + side + i;
    }
    private static string DoneTrigger(string helperName, string side, int i)
    {
        return string.IsNullOrEmpty(side)
            ? "BLDone_" + helperName + "_" + i
            : "BLD_" + helperName + "_" + side + i;
    }

    // ------------------------------------------------------------------ resolve

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

    private static Transform ResolveGroupRoot(MoveGroupDto group)
    {
        if (group == null) return null;
        if (!string.IsNullOrEmpty(group.groupHierarchyPath))
        {
            var t = LayoutEditorHierarchy.FindByPath(group.groupHierarchyPath);
            if (t != null) return t;
        }
        var name = (string.IsNullOrEmpty(group.displayName) ? "MoveGroup" : group.displayName)
            .Replace('/', '_').Replace('\\', '_');
        return LayoutEditorHierarchy.FindByPath("Design/Animated Objects/" + name);
    }

    private static Dictionary<string, MoveGroupDto> GroupMapByName(LayoutDocumentDto doc)
    {
        var byName = new Dictionary<string, MoveGroupDto>(StringComparer.Ordinal);
        foreach (var g in doc.moveControls != null ? doc.moveControls.groups ?? new MoveGroupDto[0] : new MoveGroupDto[0])
        {
            if (g == null || string.IsNullOrEmpty(g.displayName)) continue;
            if (!byName.ContainsKey(g.displayName))
                byName[g.displayName] = g;
            else
                LayoutEditorLog.LogWarning("button link: 移动组名重复 \"" + g.displayName + "\"，联动只绑定第一个");
        }
        return byName;
    }

    // ------------------------------------------------------------ phase 1: groups

    /// <summary>在 MoveControlBakery.Sync 之前调用：为被联动绑定的移动组覆写
    ///  startTrigger/endTrigger（确定性命名），并清零 startDelay（绑定后由按钮控制，
    ///  保留延迟启动会导致开局自动触发一次）。</summary>
    public static void PrepareGroups(LayoutDocumentDto doc)
    {
        if (doc == null || doc.buttonLinks == null || doc.buttonLinks.links == null) return;
        if (doc.moveControls == null || doc.moveControls.groups == null) return;

        var byName = GroupMapByName(doc);
        var pairSides = new Dictionary<string, string>(StringComparer.Ordinal);
        var boundGroups = new HashSet<string>(StringComparer.Ordinal);

        // 预统计每个 pairId 的 link 数：只有一方的"共轭"降级为普通顺序联动
        // （与 SyncInner 的降级路径命名保持一致，否则组的触发名与 helper 不匹配）。
        var pairCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var l in doc.buttonLinks.links)
        {
            if (l == null || string.IsNullOrEmpty(l.pairId)) continue;
            int c;
            pairCounts.TryGetValue(l.pairId, out c);
            pairCounts[l.pairId] = c + 1;
        }

        foreach (var link in doc.buttonLinks.links)
        {
            if (link == null || string.IsNullOrEmpty(link.sourceId)) continue;
            if (link.groupNames == null || link.groupNames.Length == 0) continue;

            bool isPair = !string.IsNullOrEmpty(link.pairId) && pairCounts[link.pairId] >= 2;
            string side = "";
            string trigBase;
            if (isPair)
            {
                if (!pairSides.ContainsKey(link.pairId))
                {
                    pairSides[link.pairId] = "A";
                    side = "A";
                }
                else
                {
                    side = "B";
                }
                trigBase = HelperNameForPair(link.pairId);
            }
            else
            {
                trigBase = HelperNameForSeq(link);
            }

            int count = link.groupNames.Length;
            if (isPair && count > PairGroupLimit)
            {
                LayoutEditorLog.LogWarning("button link: 共轭模式每个按钮最多 " + PairGroupLimit +
                    " 组，「" + link.sourceId + "」多余的已忽略");
                count = PairGroupLimit;
            }

            for (int i = 0; i < count; i++)
            {
                var gname = link.groupNames[i];
                if (string.IsNullOrEmpty(gname)) continue;
                MoveGroupDto g;
                if (!byName.TryGetValue(gname, out g))
                {
                    LayoutEditorLog.LogWarning("button link: 移动组「" + gname + "」不存在，跳过绑定");
                    continue;
                }
                if (!boundGroups.Add(gname))
                {
                    LayoutEditorLog.LogWarning("button link: 移动组「" + gname + "」已被其他联动绑定，跳过后续绑定");
                    continue;
                }
                g.startTrigger = GoTrigger(trigBase, side, i);
                g.endTrigger = DoneTrigger(trigBase, side, i);
                if (g.startDelay > 0f)
                {
                    LayoutEditorLog.LogWarning("button link: 移动组「" + gname +
                        "」的启动延迟已清零（绑定后由按钮控制）");
                    g.startDelay = 0f;
                }
                if (g.loop)
                    LayoutEditorLog.LogWarning("button link: 移动组「" + gname +
                        "」循环执行不会结束，锁定时按钮将无法再按");
            }
        }
    }

    // ------------------------------------------------------------- phase 2: bake

    public static string Sync(Scene scene, LayoutDocumentDto doc, Dictionary<string, GameObject> createdObjects)
    {
        try
        {
            return SyncInner(scene, doc, createdObjects);
        }
        catch (Exception e)
        {
            LayoutEditorLog.LogWarning("button link: bake exception: " + e);
            return "按钮联动写回异常：" + e.Message;
        }
    }

    private static string SyncInner(Scene scene, LayoutDocumentDto doc, Dictionary<string, GameObject> createdObjects)
    {
        var sceneName = Path.GetFileNameWithoutExtension(scene.path);
        var animDir = MoveControlBakery.GetAnimationsFolder(scene.path);
        var errors = new List<string>();
        var usedHelpers = new HashSet<string>(StringComparer.Ordinal);
        var usedAssets = new HashSet<string>(StringComparer.Ordinal);

        var links = doc != null && doc.buttonLinks != null && doc.buttonLinks.links != null
            ? doc.buttonLinks.links
            : new LayoutButtonLinkDto[0];

        var byName = GroupMapByName(doc);
        var boundGroups = new HashSet<string>(StringComparer.Ordinal);
        var pairSeen = new Dictionary<string, LayoutButtonLinkDto>(StringComparer.Ordinal);
        var pairBaked = new HashSet<string>(StringComparer.Ordinal);

        if (links.Length > 0)
            MoveControlBakery.EnsureFolder(animDir);

        foreach (var link in links)
        {
            if (link == null || string.IsNullOrEmpty(link.sourceId)) continue;
            if (link.groupNames == null || link.groupNames.Length == 0) continue;

            if (!string.IsNullOrEmpty(link.pairId))
            {
                if (pairBaked.Contains(link.pairId)) continue;
                LayoutButtonLinkDto first;
                if (!pairSeen.TryGetValue(link.pairId, out first))
                {
                    pairSeen[link.pairId] = link;
                    continue; // 等配对另一方出现后再整体烘焙
                }
                pairBaked.Add(link.pairId);
                var err = BakePair(scene, first, link, byName, boundGroups, animDir, sceneName,
                    createdObjects, usedHelpers, usedAssets);
                if (!string.IsNullOrEmpty(err)) errors.Add(err);
                continue;
            }

            var seqErr = BakeSequence(scene, link, byName, boundGroups, animDir, sceneName,
                createdObjects, usedHelpers, usedAssets);
            if (!string.IsNullOrEmpty(seqErr)) errors.Add(seqErr);
        }

        // 孤单一方的配对（partner 缺失）按普通顺序联动降级烘焙，避免配置悬空。
        foreach (var kv in pairSeen)
        {
            var link = kv.Value;
            bool baked = false;
            foreach (var l in links)
            {
                if (l != null && l != link && l.pairId == link.pairId) { baked = true; break; }
            }
            if (baked) continue;
            LayoutEditorLog.LogWarning("button link: 共轭对「" + link.pairId + "」只有一方，降级为普通顺序联动");
            link.pairId = null;
            var err = BakeSequence(scene, link, byName, boundGroups, animDir, sceneName,
                createdObjects, usedHelpers, usedAssets);
            if (!string.IsNullOrEmpty(err)) errors.Add(err);
        }

        CleanupStale(animDir, sceneName, usedHelpers, usedAssets);
        AssetDatabase.SaveAssets();

        if (errors.Count > 0)
            LayoutEditorLog.LogWarning("button link: bake errors: " + string.Join("; ", errors.ToArray()));
        return errors.Count > 0 ? string.Join("; ", errors.ToArray()) : null;
    }

    /// <summary>解析 link 的移动组根物体列表（跳过无法解析的项，返回 null 表示有缺失）。</summary>
    private static List<Transform> ResolveGroupRoots(LayoutButtonLinkDto link, int count,
        Dictionary<string, MoveGroupDto> byName, HashSet<string> boundGroups, List<MoveGroupDto> outGroups)
    {
        var roots = new List<Transform>();
        for (int i = 0; i < count; i++)
        {
            var gname = link.groupNames[i];
            MoveGroupDto g;
            if (string.IsNullOrEmpty(gname) || !byName.TryGetValue(gname, out g))
            {
                outGroups.Add(null);
                roots.Add(null);
                continue;
            }
            // 一个组至多属于一条联动（先绑定者生效；PrepareGroups 已跳过后者）
            if (!boundGroups.Add(gname))
            {
                outGroups.Add(null);
                roots.Add(null);
                continue;
            }
            outGroups.Add(g);
            roots.Add(ResolveGroupRoot(g));
        }
        return roots;
    }

    private static string BakeSequence(Scene scene, LayoutButtonLinkDto link,
        Dictionary<string, MoveGroupDto> byName, HashSet<string> boundGroups,
        string animDir, string sceneName, Dictionary<string, GameObject> createdObjects,
        HashSet<string> usedHelpers, HashSet<string> usedAssets)
    {
        var helperName = HelperNameForSeq(link);
        var sourceGo = ResolveObject(link.sourceId, createdObjects);
        if (sourceGo == null)
            return "按钮联动：找不到触发源 " + link.sourceId;

        var groups = new List<MoveGroupDto>();
        var roots = ResolveGroupRoots(link, link.groupNames.Length, byName, boundGroups, groups);
        for (int i = 0; i < roots.Count; i++)
        {
            if (roots[i] == null)
                return "按钮联动：移动组「" + link.groupNames[i] + "」无法绑定（不存在/未烘焙/已被其他联动占用）";
        }

        var helper = EnsureHelper(helperName);
        if (helper == null)
            return "按钮联动：无法创建 " + ButtonLogicRootPath + "/" + helperName;
        usedHelpers.Add(helperName);

        int n = roots.Count;
        var goTrigs = new string[n];
        var doneTrigs = new string[n];
        var rootNames = new string[n];
        for (int i = 0; i < n; i++)
        {
            goTrigs[i] = GoTrigger(helperName, "", i);
            doneTrigs[i] = DoneTrigger(helperName, "", i);
            rootNames[i] = roots[i].name;
        }

        var controllerPath = animDir + "/" + sceneName + "_" + helperName + ".controller";
        var controller = BuildSequenceController(controllerPath, helperName, rootNames, goTrigs, doneTrigs,
            link.lockUntilFinished);
        if (controller == null)
            return "按钮联动：controller 创建失败 " + controllerPath;
        usedAssets.Add(controllerPath);

        var anim = AttachHelperAnimator(helper, controller);
        RebuildDoneRelays(helper.gameObject, anim, doneTrigs);
        for (int i = 0; i < n; i++)
            WireGroupQueue(roots[i], doneTrigs[i], helper.gameObject);
        WireSource(sourceGo, AdvanceTrigger(helperName), anim);
        return null;
    }

    private static string BakePair(Scene scene, LayoutButtonLinkDto linkA, LayoutButtonLinkDto linkB,
        Dictionary<string, MoveGroupDto> byName, HashSet<string> boundGroups,
        string animDir, string sceneName, Dictionary<string, GameObject> createdObjects,
        HashSet<string> usedHelpers, HashSet<string> usedAssets)
    {
        var helperName = HelperNameForPair(linkA.pairId);
        var srcA = ResolveObject(linkA.sourceId, createdObjects);
        var srcB = ResolveObject(linkB.sourceId, createdObjects);
        if (srcA == null || srcB == null)
            return "共轭按钮：找不到触发源（A=" + linkA.sourceId + " B=" + linkB.sourceId + "）";

        int nA = Math.Min(linkA.groupNames.Length, PairGroupLimit);
        int nB = Math.Min(linkB.groupNames.Length, PairGroupLimit);
        var groupsA = new List<MoveGroupDto>();
        var groupsB = new List<MoveGroupDto>();
        var rootsA = ResolveGroupRoots(linkA, nA, byName, boundGroups, groupsA);
        var rootsB = ResolveGroupRoots(linkB, nB, byName, boundGroups, groupsB);
        for (int i = 0; i < rootsA.Count; i++)
            if (rootsA[i] == null) return "共轭按钮：移动组「" + linkA.groupNames[i] + "」无法绑定（不存在/未烘焙/已被占用）";
        for (int i = 0; i < rootsB.Count; i++)
            if (rootsB[i] == null) return "共轭按钮：移动组「" + linkB.groupNames[i] + "」无法绑定（不存在/未烘焙/已被占用）";

        var helper = EnsureHelper(helperName);
        if (helper == null)
            return "共轭按钮：无法创建 " + ButtonLogicRootPath + "/" + helperName;
        usedHelpers.Add(helperName);

        var goA = new string[nA]; var doneA = new string[nA]; var namesA = new string[nA];
        var goB = new string[nB]; var doneB = new string[nB]; var namesB = new string[nB];
        for (int i = 0; i < nA; i++)
        {
            goA[i] = GoTrigger(helperName, "A", i);
            doneA[i] = DoneTrigger(helperName, "A", i);
            namesA[i] = rootsA[i].name;
        }
        for (int i = 0; i < nB; i++)
        {
            goB[i] = GoTrigger(helperName, "B", i);
            doneB[i] = DoneTrigger(helperName, "B", i);
            namesB[i] = rootsB[i].name;
        }

        // 初始抬起方：A.pairStartsUp 为 true → A 抬起；双方一致（歧义）时默认 A 抬起。
        bool aStartsUp = linkA.pairStartsUp || linkA.pairStartsUp == linkB.pairStartsUp;

        var controllerPath = animDir + "/" + sceneName + "_" + helperName + ".controller";
        var controller = BuildPairController(controllerPath, helperName, namesA, goA, doneA,
            namesB, goB, doneB, aStartsUp);
        if (controller == null)
            return "共轭按钮：controller 创建失败 " + controllerPath;
        usedAssets.Add(controllerPath);

        var anim = AttachHelperAnimator(helper, controller);
        var allDone = new List<string>();
        allDone.AddRange(doneA);
        allDone.AddRange(doneB);
        RebuildDoneRelays(helper.gameObject, anim, allDone.ToArray());
        for (int i = 0; i < nA; i++) WireGroupQueue(rootsA[i], doneA[i], helper.gameObject);
        for (int i = 0; i < nB; i++) WireGroupQueue(rootsB[i], doneB[i], helper.gameObject);
        WireSource(srcA, PressTrigger(helperName, "A"), anim);
        WireSource(srcB, PressTrigger(helperName, "B"), anim);
        return null;
    }

    // ---------------------------------------------------------------- components

    private static Transform EnsureHelper(string helperName)
    {
        var t = LayoutEditorHierarchy.FindOrCreatePath(ButtonLogicRootPath + "/" + helperName);
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
            Undo.RecordObject(anim, "Layout Editor Button Link");
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;
        EditorUtility.SetDirty(anim);
        return anim;
    }

    /// <summary>重建 helper 上的 Done 消息 → Animator trigger 中继（组完成时
    ///  TriggerQueue 向 helper 广播 endTrigger，TriggerOnAnimator 转成状态机 trigger）。</summary>
    private static void RebuildDoneRelays(GameObject helper, Animator anim, string[] doneTriggers)
    {
        foreach (var old in helper.GetComponents<TriggerOnAnimator>())
            Undo.DestroyObjectImmediate(old);
        foreach (var t in doneTriggers)
        {
            if (string.IsNullOrEmpty(t)) continue;
            var relay = Undo.AddComponent<TriggerOnAnimator>(helper);
            relay.m_triggerToReceive = t;
            relay.m_triggerToFire = t;
            relay.m_targetAnimator = anim;
            relay.m_triggerToFireHash = Animator.StringToHash(t);
            EditorUtility.SetDirty(relay);
        }
    }

    /// <summary>把组根的 TriggerQueue 完成事件指向 helper（m_endTrigger 已由
    ///  PrepareGroups→MoveControlBakery 写入，这里补 m_endTriggerTarget 并核对）。</summary>
    private static void WireGroupQueue(Transform groupRoot, string doneTrigger, GameObject helper)
    {
        var q = groupRoot.GetComponent<TriggerQueue>();
        if (q == null)
        {
            LayoutEditorLog.LogWarning("button link: 组根 " + groupRoot.name + " 缺少 TriggerQueue");
            return;
        }
        Undo.RecordObject(q, "Layout Editor Button Link");
        q.m_endTrigger = doneTrigger;
        q.m_endTriggerTarget = helper;
        EditorUtility.SetDirty(q);
    }

    /// <summary>写触发源 stub：Switch 用 triggerOnAnimator/animatorToTrigger（按压驱动
    ///  逻辑 Animator）；PressureSwitch 用 triggerOnAnimatorEnter（Exit 指向 BLNoop 占位）。</summary>
    private static void WireSource(GameObject sourceGo, string pressTrigger, Animator targetAnim)
    {
        var sw = sourceGo.GetComponent<PseudoPrefabSwitchStub>();
        if (sw != null)
        {
            Undo.RecordObject(sw, "Layout Editor Button Link");
            sw.triggerOnAnimator = pressTrigger;
            sw.animatorToTrigger = targetAnim;
            EditorUtility.SetDirty(sw);
            return;
        }
        var ps = sourceGo.GetComponent<PseudoPrefabPressureSwitchStub>();
        if (ps != null)
        {
            Undo.RecordObject(ps, "Layout Editor Button Link");
            ps.triggerOnAnimatorEnter = pressTrigger;
            ps.triggerOnAnimatorExit = NoopTrigger;
            ps.animatorToTrigger = targetAnim;
            EditorUtility.SetDirty(ps);
            return;
        }
        LayoutEditorLog.LogWarning("button link: 触发源 " + sourceGo.name +
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

    /// <summary>顺序联动状态机：Ready_i --Advance--> Run_i（进入即启动组 i）
    ///  --Done_i--> Ready_{i+1 mod n}。锁定模式：Run_i 上的按压被 ClearTriggerDuringState
    ///  吞掉（组完成后才接受下一次）；非锁定：Run_i --Advance--> Run_{i+1} 直接连发。</summary>
    private static AnimatorController BuildSequenceController(string path, string helperName,
        string[] rootNames, string[] goTrigs, string[] doneTrigs, bool lockUntilFinished)
    {
        MoveControlBakery.DeleteAssetIfExists(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        if (ctrl == null) return null;
        ctrl.name = Path.GetFileNameWithoutExtension(path);

        var adv = AdvanceTrigger(helperName);
        ctrl.AddParameter(adv, AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter(NoopTrigger, AnimatorControllerParameterType.Trigger);
        for (int i = 0; i < doneTrigs.Length; i++)
            ctrl.AddParameter(doneTrigs[i], AnimatorControllerParameterType.Trigger);

        var sm = ctrl.layers[0].stateMachine;
        int n = rootNames.Length;
        var ready = new AnimatorState[n];
        var run = new AnimatorState[n];
        for (int i = 0; i < n; i++)
        {
            ready[i] = NewState(sm, "Ready_" + i);
            run[i] = NewState(sm, "Run_" + i);
        }
        sm.defaultState = ready[0];

        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            AddTrigTransition(ready[i], run[i], adv);
            AddSendTrigger(run[i], rootNames[i], goTrigs[i]);
            AddTrigTransition(run[i], ready[next], doneTrigs[i]);
            if (lockUntilFinished)
            {
                // 运行期锁定：按压被吞掉且不锁存（"移动组完成后才可再按"）。
                AddClearTrigger(run[i], adv);
            }
            else
            {
                // 非锁定：运行中再按直接触发下一组。
                AddTrigTransition(run[i], run[next], adv);
                // Done_j 可能在其他状态到达（无消费过渡）→ 清空防锁存误触发。
                for (int j = 0; j < n; j++)
                {
                    if (j == i) continue;
                    AddClearTrigger(run[i], doneTrigs[j]);
                    AddClearTrigger(ready[i], doneTrigs[j]);
                }
            }
        }
        AddClearTrigger(ready[0], NoopTrigger);
        return ctrl;
    }

    /// <summary>共轭对状态机：AReady --PA--> ARun（同时启动 A 方各组）→ A 方全部完成
    ///  （AND 门，m=2 时两条顺序无关路径）→ BReady --PB--> BRun → … → AReady。
    ///  对方的按压在任意非就绪态都被吞掉（功能上的"按下状态不可再按"）。</summary>
    private static AnimatorController BuildPairController(string path, string helperName,
        string[] namesA, string[] goA, string[] doneA,
        string[] namesB, string[] goB, string[] doneB, bool aStartsUp)
    {
        MoveControlBakery.DeleteAssetIfExists(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        if (ctrl == null) return null;
        ctrl.name = Path.GetFileNameWithoutExtension(path);

        var pa = PressTrigger(helperName, "A");
        var pb = PressTrigger(helperName, "B");
        ctrl.AddParameter(pa, AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter(pb, AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter(NoopTrigger, AnimatorControllerParameterType.Trigger);
        for (int i = 0; i < doneA.Length; i++)
            ctrl.AddParameter(doneA[i], AnimatorControllerParameterType.Trigger);
        for (int i = 0; i < doneB.Length; i++)
            ctrl.AddParameter(doneB[i], AnimatorControllerParameterType.Trigger);

        var sm = ctrl.layers[0].stateMachine;
        var aReady = NewState(sm, "AReady");
        var aRun = NewState(sm, "ARun");
        var bReady = NewState(sm, "BReady");
        var bRun = NewState(sm, "BRun");
        sm.defaultState = aStartsUp ? aReady : bReady;

        for (int i = 0; i < namesA.Length; i++)
            AddSendTrigger(aRun, namesA[i], goA[i]);
        for (int i = 0; i < namesB.Length; i++)
            AddSendTrigger(bRun, namesB[i], goB[i]);

        AddClearTrigger(aReady, pb); // A 抬起时 B 的按压被吞掉
        AddClearTrigger(bReady, pa);
        foreach (var st in new[] { aRun, bRun })
        {
            AddClearTrigger(st, pa);
            AddClearTrigger(st, pb);
        }

        AddTrigTransition(aReady, aRun, pa);
        AddTrigTransition(bReady, bRun, pb);

        // AND 门：A 方各组全部完成 → BReady（m=1 直接过渡；m=2 走两条顺序无关路径）。
        if (doneA.Length == 1)
        {
            AddTrigTransition(aRun, bReady, doneA[0]);
        }
        else if (doneA.Length >= 2)
        {
            var aWait1 = NewState(sm, "AWait1"); // 已收到 A0，等 A1
            var aWait0 = NewState(sm, "AWait0"); // 已收到 A1，等 A0
            AddTrigTransition(aRun, aWait1, doneA[0]);
            AddTrigTransition(aWait1, bReady, doneA[1]);
            AddTrigTransition(aRun, aWait0, doneA[1]);
            AddTrigTransition(aWait0, bReady, doneA[0]);
            AddClearTrigger(aWait0, pa); AddClearTrigger(aWait0, pb);
            AddClearTrigger(aWait1, pa); AddClearTrigger(aWait1, pb);
        }
        if (doneB.Length == 1)
        {
            AddTrigTransition(bRun, aReady, doneB[0]);
        }
        else if (doneB.Length >= 2)
        {
            var bWait1 = NewState(sm, "BWait1");
            var bWait0 = NewState(sm, "BWait0");
            AddTrigTransition(bRun, bWait1, doneB[0]);
            AddTrigTransition(bWait1, aReady, doneB[1]);
            AddTrigTransition(bRun, bWait0, doneB[1]);
            AddTrigTransition(bWait0, aReady, doneB[0]);
            AddClearTrigger(bWait0, pa); AddClearTrigger(bWait0, pb);
            AddClearTrigger(bWait1, pa); AddClearTrigger(bWait1, pb);
        }
        AddClearTrigger(aReady, NoopTrigger);
        return ctrl;
    }

    // ------------------------------------------------------------------- cleanup

    private static void CleanupStale(string animDir, string sceneName,
        HashSet<string> usedHelpers, HashSet<string> usedAssets)
    {
        var root = LayoutEditorHierarchy.FindByPath(ButtonLogicRootPath);
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
                LayoutEditorLog.Log("button link: cleanup stale helper " + go.name);
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
            if (!file.StartsWith(sceneName + "_BtnLogic", StringComparison.Ordinal) &&
                !file.StartsWith(sceneName + "_BtnPair", StringComparison.Ordinal)) continue;
            if (usedAssets.Contains(path)) continue;
            AssetDatabase.DeleteAsset(path);
        }
    }

    // ------------------------------------------------------------------- import

    /// <summary>从场景重建按钮联动（场景是唯一事实源）：扫描 Design/Button Logic 下的
    ///  helper，按 controller 状态里的 SendTriggerToObject 反查移动组，按 stub 接线反查触发源。</summary>
    public static List<LayoutButtonLinkDto> ImportFromScene(Scene scene,
        List<MoveGroupDto> groups, List<LayoutItemDto> items)
    {
        var result = new List<LayoutButtonLinkDto>();
        if (!scene.IsValid()) return result;
        var root = LayoutEditorHierarchy.FindByPath(ButtonLogicRootPath);
        if (root == null) return result;

        // startTrigger → 移动组（由 PrepareGroups 覆写为 BLGo_* 名，随 MoveControlImporter 回导）
        var groupByStartTrigger = new Dictionary<string, MoveGroupDto>(StringComparer.Ordinal);
        foreach (var g in groups ?? new List<MoveGroupDto>())
        {
            if (g == null || string.IsNullOrEmpty(g.startTrigger)) continue;
            if (!groupByStartTrigger.ContainsKey(g.startTrigger))
                groupByStartTrigger[g.startTrigger] = g;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            var helper = root.GetChild(i);
            var anim = helper.GetComponent<Animator>();
            var ctrl = anim != null ? anim.runtimeAnimatorController as AnimatorController : null;
            if (ctrl == null || ctrl.layers.Length == 0) continue;
            var sm = ctrl.layers[0].stateMachine;
            if (sm == null) continue;

            var name = helper.name;
            if (name.StartsWith(HelperPairPrefix, StringComparison.Ordinal))
                ImportPair(result, name, helper.gameObject, anim, sm, groupByStartTrigger, items);
            else if (name.StartsWith(HelperSeqPrefix, StringComparison.Ordinal) ||
                     name.StartsWith(HelperSeqNoLockPrefix, StringComparison.Ordinal))
                ImportSequence(result, name, helper.gameObject, anim, sm, groupByStartTrigger, items);
        }
        return result;
    }

    private static void ImportSequence(List<LayoutButtonLinkDto> result, string helperName,
        GameObject helperGo, Animator anim, AnimatorStateMachine sm,
        Dictionary<string, MoveGroupDto> groupByStartTrigger, List<LayoutItemDto> items)
    {
        // Run_i 状态按序号排列，读取其 SendTriggerToObject 反查移动组。
        var runStates = new SortedDictionary<int, AnimatorState>();
        foreach (var cs in sm.states)
        {
            var st = cs.state;
            if (st == null || !st.name.StartsWith("Run_", StringComparison.Ordinal)) continue;
            int idx;
            if (!int.TryParse(st.name.Substring(4), out idx)) continue;
            runStates[idx] = st;
        }
        if (runStates.Count == 0) return;

        var groupNames = new List<string>();
        foreach (var kv in runStates)
        {
            var g = GroupOfState(kv.Value, groupByStartTrigger);
            if (g == null) return; // 引用的组已被删除 → 整条联动视为失效
            groupNames.Add(g.displayName);
        }

        var sourceId = FindSourceId(items, anim, AdvanceTrigger(helperName));
        if (string.IsNullOrEmpty(sourceId))
        {
            LayoutEditorLog.LogWarning("button link: helper " + helperName + " 找不到触发源，跳过导入");
            return;
        }

        result.Add(new LayoutButtonLinkDto
        {
            id = ImportedSeqMarker + helperName,
            sourceId = sourceId,
            groupNames = groupNames.ToArray(),
            lockUntilFinished = helperName.StartsWith(HelperSeqPrefix, StringComparison.Ordinal),
        });
    }

    private static void ImportPair(List<LayoutButtonLinkDto> result, string helperName,
        GameObject helperGo, Animator anim, AnimatorStateMachine sm,
        Dictionary<string, MoveGroupDto> groupByStartTrigger, List<LayoutItemDto> items)
    {
        var pairId = ImportedPairMarker + helperName;
        var srcA = FindSourceId(items, anim, PressTrigger(helperName, "A"));
        var srcB = FindSourceId(items, anim, PressTrigger(helperName, "B"));
        if (string.IsNullOrEmpty(srcA) || string.IsNullOrEmpty(srcB))
        {
            LayoutEditorLog.LogWarning("button link: 共轭 helper " + helperName + " 触发源不全，跳过导入");
            return;
        }

        var groupsA = GroupsOfRunState(sm, "ARun", groupByStartTrigger);
        var groupsB = GroupsOfRunState(sm, "BRun", groupByStartTrigger);
        if (groupsA == null || groupsB == null || groupsA.Count == 0 || groupsB.Count == 0)
        {
            LayoutEditorLog.LogWarning("button link: 共轭 helper " + helperName + " 的移动组不完整，跳过导入");
            return;
        }

        bool aStartsUp = sm.defaultState != null && sm.defaultState.name == "AReady";
        result.Add(new LayoutButtonLinkDto
        {
            id = pairId + ":A",
            sourceId = srcA,
            groupNames = groupsA.ToArray(),
            lockUntilFinished = true,
            pairId = pairId,
            pairStartsUp = aStartsUp,
        });
        result.Add(new LayoutButtonLinkDto
        {
            id = pairId + ":B",
            sourceId = srcB,
            groupNames = groupsB.ToArray(),
            lockUntilFinished = true,
            pairId = pairId,
            pairStartsUp = !aStartsUp,
        });
    }

    /// <summary>读取 Run 状态上全部 SendTriggerToObject 的触发名，反查移动组（顺序任意，
    ///  组内顺序对共轭无意义——两组同时启动）。任一组缺失返回 null。</summary>
    private static List<string> GroupsOfRunState(AnimatorStateMachine sm, string stateName,
        Dictionary<string, MoveGroupDto> groupByStartTrigger)
    {
        foreach (var cs in sm.states)
        {
            var st = cs.state;
            if (st == null || st.name != stateName) continue;
            var names = new List<string>();
            foreach (var trigger in ReadSendTriggers(st))
            {
                MoveGroupDto g;
                if (!groupByStartTrigger.TryGetValue(trigger, out g)) return null;
                names.Add(g.displayName);
            }
            return names;
        }
        return null;
    }

    private static MoveGroupDto GroupOfState(AnimatorState st,
        Dictionary<string, MoveGroupDto> groupByStartTrigger)
    {
        foreach (var trigger in ReadSendTriggers(st))
        {
            MoveGroupDto g;
            if (groupByStartTrigger.TryGetValue(trigger, out g))
                return g;
        }
        return null;
    }

    /// <summary>读取状态上所有 SendTriggerToObject 的 m_triggerToSend（私有字段走
    ///  SerializedObject；Unity 2017 无公开 behaviours API）。</summary>
    private static List<string> ReadSendTriggers(AnimatorState st)
    {
        var triggers = new List<string>();
        var so = new SerializedObject(st);
        var behaviours = so.FindProperty("m_Behaviours");
        if (behaviours == null || !behaviours.isArray) return triggers;
        for (int i = 0; i < behaviours.arraySize; i++)
        {
            var bref = behaviours.GetArrayElementAtIndex(i).objectReferenceValue;
            var send = bref as SendTriggerToObject;
            if (send == null) continue;
            var bso = new SerializedObject(send);
            var tp = bso.FindProperty("m_triggerToSend");
            if (tp != null && !string.IsNullOrEmpty(tp.stringValue))
                triggers.Add(tp.stringValue);
        }
        return triggers;
    }

    /// <summary>反查触发源：stub 的 animatorToTrigger 指向该 helper 且触发名匹配。 </summary>
    private static string FindSourceId(List<LayoutItemDto> items, Animator anim, string pressTrigger)
    {
        foreach (var item in items ?? new List<LayoutItemDto>())
        {
            if (item == null || string.IsNullOrEmpty(item.instanceId)) continue;
            var go = ResolveObject(item.instanceId, null);
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
