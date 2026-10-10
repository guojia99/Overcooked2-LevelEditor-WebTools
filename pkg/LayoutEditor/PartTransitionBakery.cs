using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 分 P（多阶段布局）场景结构与转场烘焙器 —— docs/10-分P关卡功能规格.md §4/§5.4。
///
/// 职责（写回链全自动，作者只管画布与配置）：
/// 1. EnsureSlotRoots（布局期）：为每个阶段确保 Design/PartSlots/&lt;pid&gt; 根存在并
///    归零 —— 布局期世界坐标 == 设计坐标，物件/地板的既有世界摆放数学不变。
/// 2. Sync（收尾期，全部 pass 之后、SaveScene 之前）：
///    a. slot 根 park：P1 恒 0；P2+ 置于 parts[].park 偏移（默认 y−30）。
///       子物体（物件/地板/子碰撞/动画组根）随之整体移动。
///    b. 每个 slot 根烘焙转场 Animator（自研 controller，非 AnimGroupBakery 产物）：
///       Idle_Park / Idle_Active 定持 + Entry(park→0) / Exit(0→park) 单成员位移动画，
///       触发器名 = PartSwitch_&lt;from&gt;_&lt;to&gt;（由时间轴中继 SetTrigger）。
///    c. Design/PartTimeline 时间轴链（全原版组件，GameObject.SendTrigger 只达
///       同物体接收器 → 全部 TriggerTimer/中继挂在同一个时间轴物体上）：
///       稳定计时 → PartSwitch →（sink 再经 outSeconds 延时发 _in）→ Stable_&lt;to&gt;
///       → 延迟 cleanupDelaySeconds → Kill_&lt;from&gt;（slot 根 TriggerKillAttachments
///       清内容物）+ KillLoose_&lt;from&gt;（Design 根 Loose-only 全场散落食材大扫除）。
///    d. 清理：已删阶段的 slot 根与动画资产、旧时间轴、旧 KillAttachments。
///
/// 资产命名：&lt;scene&gt;_partslot_&lt;pid&gt;.controller / *.anim（SlotAssetMarker 供
/// AnimGroupBakery.CleanupStale 排除，避免误删）。导入器不受影响：slot 根无
/// TriggerQueue/TriggerTimer、时间轴无 Animator，均不构成动画组候选。
/// </summary>
public static class PartTransitionBakery
{
    public const string SlotsRootPath = "Design/PartSlots";
    public const string TimelinePath = "Design/PartTimeline";
    public const string SlotAssetMarker = "_partslot_";
    private const float StateBlend = 0.15f;

    // ---------- 通用判定（供 SceneLayoutApplier / AnimGroupBakery 复用） ----------

    /** 分 P 资产（controller/clips）判定：AnimGroupBakery.CleanupStale 须排除。 */
    public static bool IsPartSlotAsset(string assetPath)
    {
        return assetPath != null && assetPath.Contains(SlotAssetMarker);
    }

    /** partId 是否属于某个阶段（null/""/"base" = 基础层/普关，不进 slot）。 */
    public static bool HasPartScope(string partId)
    {
        return !string.IsNullOrEmpty(partId) && partId != "base";
    }

    public static bool IsPartEnabled(LayoutDocumentDto doc)
    {
        return doc != null && doc.partLevel != null && doc.partLevel.enabled;
    }

    // ---------- 布局期 ----------

    /// <summary>确保全部 slot 根存在并归零（世界坐标 == 设计坐标）。在物件 pass 之前调用。</summary>
    public static void EnsureSlotRoots(LayoutDocumentDto doc)
    {
        if (!IsPartEnabled(doc) || doc.partLevel.parts == null)
            return;
        LayoutEditorHierarchy.FindOrCreatePath(SlotsRootPath);
        foreach (var p in doc.partLevel.parts)
        {
            if (p == null || string.IsNullOrEmpty(p.id))
                continue;
            var root = LayoutEditorHierarchy.FindOrCreatePath(SlotsRootPath + "/" + p.id);
            if (root == null)
                continue;
            root.localPosition = Vector3.zero;
        }
    }

    // ---------- 收尾期 ----------

    public static void Sync(Scene scene, LayoutDocumentDto doc)
    {
        var pl = IsPartEnabled(doc) ? doc.partLevel : null;

        // 已删阶段：销毁残留 slot 根（其内容已被物件/地板删除 pass 清掉）与动画资产。
        CleanupStaleSlots(pl);
        var sceneName = Path.GetFileNameWithoutExtension(scene.path);
        CleanupStaleAssets(scene, sceneName, pl);

        if (pl == null || pl.parts == null || pl.parts.Length == 0)
        {
            DestroyTimeline();
            return;
        }

        var animDir = AnimationsFolderOf(scene);
        var slotRoots = new Dictionary<string, Transform>();
        var slotAnims = new Dictionary<string, Animator>();

        for (int i = 0; i < pl.parts.Length; i++)
        {
            var p = pl.parts[i];
            if (p == null || string.IsNullOrEmpty(p.id))
                continue;
            var root = LayoutEditorHierarchy.FindOrCreatePath(SlotsRootPath + "/" + p.id);
            if (root == null)
                continue;
            // park：P1 恒 0；P2+ 停放于 park 偏移（子树随之移动）。
            root.localPosition = ParkVector(p, i == 0);
            slotRoots[p.id] = root;
        }

        // 转场只在线性链 N−1 段有意义；单阶段仅保留 slot 结构，不烘动画/时间轴。
        if (pl.parts.Length >= 2)
        {
            for (int i = 0; i < pl.parts.Length; i++)
            {
                var p = pl.parts[i];
                if (p == null || string.IsNullOrEmpty(p.id) || !slotRoots.ContainsKey(p.id))
                    continue;
                var anim = BakeSlotAnimator(scene, slotRoots[p.id], pl, i, animDir, sceneName);
                if (anim != null)
                    slotAnims[p.id] = anim;
            }
            BakeTimeline(pl, slotRoots, slotAnims);
        }

        AssetDatabase.SaveAssets();
    }

    /// <summary>park 偏移向量：down/up = ∓Y，left/right = ∓X（世界轴）；P1 恒零。</summary>
    private static Vector3 ParkVector(PartDto p, bool isFirst)
    {
        if (isFirst || p == null)
            return Vector3.zero;
        var off = Mathf.Clamp(p.park != null ? p.park.offset : 30f, 5f, 200f);
        var axis = p.park != null && !string.IsNullOrEmpty(p.park.axis) ? p.park.axis : "down";
        if (axis == "up")
            return new Vector3(0f, off, 0f);
        if (axis == "left")
            return new Vector3(-off, 0f, 0f);
        if (axis == "right")
            return new Vector3(off, 0f, 0f);
        return new Vector3(0f, -off, 0f);
    }

    // ---------- slot 转场 Animator ----------

    private static Animator BakeSlotAnimator(Scene scene, Transform root, PartLevelDto pl, int index,
        string animDir, string sceneName)
    {
        var pid = pl.parts[index].id;
        var park = ParkVector(pl.parts[index], index == 0);
        var baseName = sceneName + SlotAssetMarker + pid;
        var ctrlPath = animDir + "/" + baseName + ".controller";

        // 幂等：删旧资产重烘（成员固定为根自身，无外部引用残留）。
        DeleteAssetIfExists(ctrlPath);
        DeleteAssetIfExists(animDir + "/" + baseName + "_holdpark.anim");
        DeleteAssetIfExists(animDir + "/" + baseName + "_holdact.anim");
        DeleteAssetIfExists(animDir + "/" + baseName + "_exit.anim");
        DeleteAssetIfExists(animDir + "/" + baseName + "_entry.anim");

        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        var sm = ctrl.layers[0].stateMachine;

        // 触发器名（与时间轴链 §3.6 约定一致）。
        var entryTrig = index > 0 ? "PartSwitch_" + pl.parts[index - 1].id + "_" + pid : null;
        var exitTrig = index + 1 < pl.parts.Length ? "PartSwitch_" + pid + "_" + pl.parts[index + 1].id : null;

        var idlePark = AddHoldState(sm, ctrl, animDir, baseName + "_holdpark", "Idle_Park", park);
        var idleAct = AddHoldState(sm, ctrl, animDir, baseName + "_holdact", "Idle_Active", Vector3.zero);

        if (!string.IsNullOrEmpty(entryTrig))
        {
            ctrl.AddParameter(entryTrig, AnimatorControllerParameterType.Trigger);
            var entry = AddMoveState(sm, ctrl, animDir, baseName + "_entry", "Entry", park, Vector3.zero,
                Mathf.Max(0.1f, TransitionSeconds(pl, index - 1, true)));
            WireTrigger(sm, idlePark, entry, entryTrig);
            WireExitTime(sm, entry, idleAct);
        }
        if (!string.IsNullOrEmpty(exitTrig))
        {
            ctrl.AddParameter(exitTrig, AnimatorControllerParameterType.Trigger);
            var exit = AddMoveState(sm, ctrl, animDir, baseName + "_exit", "Exit", Vector3.zero, park,
                Mathf.Max(0.1f, TransitionSeconds(pl, index, false)));
            WireTrigger(sm, idleAct, exit, exitTrig);
            WireExitTime(sm, exit, idlePark);
        }

        // 默认状态：P1 已在场（Active）；P2+ 停放中（Park）。
        sm.defaultState = index == 0 ? idleAct : idlePark;
        EditorUtility.SetDirty(ctrl);

        var anim = root.GetComponent<Animator>();
        if (anim == null)
            anim = Undo.AddComponent<Animator>(root.gameObject);
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        return anim;
    }

    /// <summary>取第 k 段转场的入场/退场秒数（容错：字段缺省按 3/2）。</summary>
    private static float TransitionSeconds(PartLevelDto pl, int transitionIndex, bool inbound)
    {
        if (pl.transitions == null || transitionIndex < 0 || transitionIndex >= pl.transitions.Length)
            return inbound ? 3f : 2f;
        var t = pl.transitions[transitionIndex];
        if (t == null)
            return inbound ? 3f : 2f;
        var v = inbound ? t.inSeconds : t.outSeconds;
        return v > 0f ? v : (inbound ? 3f : 2f);
    }

    private static AnimatorState AddHoldState(AnimatorStateMachine sm, AnimatorController ctrl,
        string animDir, string clipName, string stateName, Vector3 pos)
    {
        var clip = new AnimationClip();
        clip.SetCurve("", typeof(Transform), "m_LocalPosition.x", HoldCurve(pos.x));
        clip.SetCurve("", typeof(Transform), "m_LocalPosition.y", HoldCurve(pos.y));
        clip.SetCurve("", typeof(Transform), "m_LocalPosition.z", HoldCurve(pos.z));
        AssetDatabase.CreateAsset(clip, animDir + "/" + clipName + ".anim");
        var st = sm.AddState(stateName);
        st.motion = clip;
        st.writeDefaultValues = false;
        return st;
    }

    private static AnimatorState AddMoveState(AnimatorStateMachine sm, AnimatorController ctrl,
        string animDir, string clipName, string stateName, Vector3 from, Vector3 to, float dur)
    {
        var clip = new AnimationClip();
        clip.SetCurve("", typeof(Transform), "m_LocalPosition.x", LineCurve(from.x, to.x, dur));
        clip.SetCurve("", typeof(Transform), "m_LocalPosition.y", LineCurve(from.y, to.y, dur));
        clip.SetCurve("", typeof(Transform), "m_LocalPosition.z", LineCurve(from.z, to.z, dur));
        AssetDatabase.CreateAsset(clip, animDir + "/" + clipName + ".anim");
        var st = sm.AddState(stateName);
        st.motion = clip;
        st.writeDefaultValues = false;
        return st;
    }

    private static void WireTrigger(AnimatorStateMachine sm, AnimatorState from, AnimatorState to, string trig)
    {
        var tr = from.AddTransition(to);
        tr.hasExitTime = false;
        tr.duration = StateBlend;
        tr.AddCondition(AnimatorConditionMode.If, 0f, trig);
    }

    private static void WireExitTime(AnimatorStateMachine sm, AnimatorState from, AnimatorState to)
    {
        var tr = from.AddTransition(to);
        tr.hasExitTime = true;
        tr.exitTime = 1f;
        tr.duration = StateBlend;
    }

    private static AnimationCurve HoldCurve(float v)
    {
        return new AnimationCurve(new Keyframe(0f, v));
    }

    private static AnimationCurve LineCurve(float a, float b, float dur)
    {
        if (dur < 0.01f)
            dur = 0.01f;
        return new AnimationCurve(new Keyframe(0f, a), new Keyframe(dur, b));
    }

    // ---------- 时间轴链 ----------

    private static void BakeTimeline(PartLevelDto pl, Dictionary<string, Transform> slotRoots,
        Dictionary<string, Animator> slotAnims)
    {
        DestroyTimeline();
        var tl = LayoutEditorHierarchy.FindOrCreatePath(TimelinePath);
        if (tl == null)
            return;
        var designRoot = FindSceneRoot("Design");

        for (int k = 0; k + 1 < pl.parts.Length; k++)
        {
            var from = pl.parts[k];
            var to = pl.parts[k + 1];
            if (from == null || to == null)
                continue;
            var t = FindTransition(pl, from.id, to.id);
            var swName = "PartSwitch_" + from.id + "_" + to.id;
            var sink = t != null && t.preset == "sink_then_rise";
            var stable = t != null && t.stableSeconds > 0f ? t.stableSeconds : 60f;
            var outSec = t != null && t.outSeconds > 0f ? t.outSeconds : 2f;
            var total = t != null && t.totalSeconds > 0f ? t.totalSeconds : outSec + (t != null && t.inSeconds > 0f ? t.inSeconds : 3f);
            var cleanupDelay = t != null && t.cleanupDelaySeconds > 0f ? t.cleanupDelaySeconds : 0f;

            // 1) 稳定计时：首段回合开始自动（m_startTiming），后续由 Stable_<from> 装填。
            var stableTimer = Undo.AddComponent<TriggerTimer>(tl.gameObject);
            stableTimer.m_startTrigger = k == 0 ? null : "Stable_" + from.id;
            stableTimer.m_startTiming = k == 0;
            stableTimer.m_time = Mathf.Max(1f, stable);
            stableTimer.m_completeTrigger = swName;
            stableTimer.m_triggerAtStart = false;

            // 2) sink 节奏：旧 P 退场完成后再放新 P 入场。
            if (sink)
            {
                var inTimer = Undo.AddComponent<TriggerTimer>(tl.gameObject);
                inTimer.m_startTrigger = swName;
                inTimer.m_time = Mathf.Max(0.1f, outSec);
                inTimer.m_completeTrigger = swName + "_in";
            }

            // 3) 转场总时长 → Stable_<to>（新 P 就绪信号；同时装填下一段稳定计时与清理）。
            var totalTimer = Undo.AddComponent<TriggerTimer>(tl.gameObject);
            totalTimer.m_startTrigger = swName;
            totalTimer.m_time = Mathf.Max(0.1f, total);
            totalTimer.m_completeTrigger = "Stable_" + to.id;

            // 4) 退场/入场中继（TriggerOnAnimator：收广播 → SetTrigger 到 slot Animator）。
            Animator fromAnim, toAnim;
            if (slotAnims.TryGetValue(from.id, out fromAnim))
                AddAnimRelay(tl, swName, fromAnim, swName);
            if (slotAnims.TryGetValue(to.id, out toAnim))
                AddAnimRelay(tl, sink ? swName + "_in" : swName, toAnim, swName);

            // 5) 清理链：Stable 后延迟 → 杀旧 slot 内容物 +（可选）全场散落食材大扫除。
            Transform fromRoot;
            if (slotRoots.TryGetValue(from.id, out fromRoot))
            {
                if (t == null || t.cleanupContents)
                {
                    var killTimer = Undo.AddComponent<TriggerTimer>(tl.gameObject);
                    killTimer.m_startTrigger = "Stable_" + to.id;
                    killTimer.m_time = cleanupDelay;
                    killTimer.m_completeTrigger = "Kill_" + from.id;
                    AddObjectRelay(tl, "Kill_" + from.id, fromRoot.gameObject);
                    EnsureKillAttachments(fromRoot.gameObject, "Kill_" + from.id, 3);
                }
                if (t == null || t.killLooseEverywhere)
                {
                    if (designRoot != null)
                    {
                        var looseName = "KillLoose_" + from.id;
                        var looseTimer = Undo.AddComponent<TriggerTimer>(tl.gameObject);
                        looseTimer.m_startTrigger = "Stable_" + to.id;
                        looseTimer.m_time = cleanupDelay;
                        looseTimer.m_completeTrigger = looseName;
                        AddObjectRelay(tl, looseName, designRoot.gameObject);
                        EnsureKillAttachments(designRoot.gameObject, looseName, 1);
                    }
                }
            }
        }
    }

    private static PartTransitionDto FindTransition(PartLevelDto pl, string fromId, string toId)
    {
        if (pl.transitions == null)
            return null;
        foreach (var t in pl.transitions)
        {
            if (t != null && t.fromPartId == fromId && t.toPartId == toId)
                return t;
        }
        return null;
    }

    private static void AddAnimRelay(Transform tl, string receive, Animator target, string fire)
    {
        var r = Undo.AddComponent<TriggerOnAnimator>(tl.gameObject);
        r.m_triggerToReceive = receive;
        r.m_triggerToFire = fire;
        r.m_targetAnimator = target;
    }

    private static void AddObjectRelay(Transform tl, string receive, GameObject target)
    {
        var r = Undo.AddComponent<TriggerOnObject>(tl.gameObject);
        r.m_trigger = receive;
        r.m_triggerToFire = receive;
        r.m_targetObject = target;
        r.m_targetObjects = new GameObject[0];
    }

    /// <summary>幂等挂 TriggerKillAttachments（killMode：3 = Loose|Attached 全杀，1 = 仅 Loose）。</summary>
    private static void EnsureKillAttachments(GameObject host, string trigger, int killMode)
    {
        var existing = host.GetComponents<TriggerKillAttachments>();
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i].m_trigger == trigger)
            {
                existing[i].m_killMode = killMode;
                return;
            }
        }
        var k = Undo.AddComponent<TriggerKillAttachments>(host);
        k.m_trigger = trigger;
        k.m_killMode = killMode;
    }

    // ---------- 清理 ----------

    private static void CleanupStaleSlots(PartLevelDto pl)
    {
        var slotsRoot = LayoutEditorHierarchy.FindByPath(SlotsRootPath);
        if (slotsRoot == null)
            return;
        var keep = new HashSet<string>();
        if (pl != null && pl.parts != null)
        {
            foreach (var p in pl.parts)
            {
                if (p != null && !string.IsNullOrEmpty(p.id))
                    keep.Add(p.id);
            }
        }
        var doomed = new List<GameObject>();
        for (int i = 0; i < slotsRoot.childCount; i++)
        {
            var c = slotsRoot.GetChild(i);
            if (c != null && !keep.Contains(c.name))
                doomed.Add(c.gameObject);
        }
        foreach (var go in doomed)
        {
            LayoutEditorLog.Log("[PartLevel] 清理已删除阶段的 slot 根：" + go.name);
            Undo.DestroyObjectImmediate(go);
        }
    }

    private static void DestroyTimeline()
    {
        var tl = LayoutEditorHierarchy.FindByPath(TimelinePath);
        if (tl != null)
            Undo.DestroyObjectImmediate(tl.gameObject);
        // 旧的全场大扫除组件（时间轴重建后触发名已失效）。
        var designRoot = FindSceneRoot("Design");
        if (designRoot != null)
        {
            var stale = new List<TriggerKillAttachments>();
            var ks = designRoot.GetComponents<TriggerKillAttachments>();
            for (int i = 0; i < ks.Length; i++)
            {
                if (ks[i].m_trigger != null && ks[i].m_trigger.StartsWith("KillLoose_", System.StringComparison.Ordinal))
                    stale.Add(ks[i]);
            }
            foreach (var k in stale)
                Undo.DestroyObjectImmediate(k);
        }
    }

    private static void CleanupStaleAssets(Scene scene, string sceneName, PartLevelDto pl)
    {
        var animDir = AnimationsFolderOf(scene);
        if (string.IsNullOrEmpty(animDir) || !AssetDatabase.IsValidFolder(animDir))
            return;
        var keep = new HashSet<string>();
        if (pl != null && pl.parts != null)
        {
            foreach (var p in pl.parts)
            {
                if (p != null && !string.IsNullOrEmpty(p.id))
                    keep.Add(sceneName + SlotAssetMarker + p.id);
            }
        }
        var marker = sceneName + SlotAssetMarker;
        foreach (var guid in AssetDatabase.FindAssets("t:Object", new[] { animDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
            var fileName = Path.GetFileNameWithoutExtension(path);
            if (!fileName.Contains(marker))
                continue;
            // 文件名形如 <scene>_partslot_<pid>[_holdpark|_holdact|_exit|_entry]，
            // 取 marker 后、第一个 "_" 前的部分还原 pid 比对。
            var afterMarker = fileName.Substring(fileName.IndexOf(marker) + marker.Length);
            var pid = afterMarker.Split('_')[0];
            if (keep.Contains(sceneName + SlotAssetMarker + pid))
                continue;
            AssetDatabase.DeleteAsset(path);
        }
    }

    // ---------- 杂项 ----------

    private static void DeleteAssetIfExists(string path)
    {
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
            AssetDatabase.DeleteAsset(path);
    }

    private static Transform FindSceneRoot(string name)
    {
        var scene = EditorSceneManager.GetActiveScene();
        foreach (var go in scene.GetRootGameObjects())
        {
            if (go != null && go.name == name)
                return go.transform;
        }
        return null;
    }

    /// <summary>动画资产目录（镜像 AnimGroupBakery.GetAnimationsFolder：场景在
    /// LevelSets/scenes/ 下 → Assets/LevelSets/&lt;set&gt;/animations；否则场景同级 animations/）。</summary>
    private static string AnimationsFolderOf(Scene scene)
    {
        var sceneDir = (Path.GetDirectoryName(scene.path) ?? "").Replace('\\', '/');
        if (sceneDir.EndsWith("/scenes"))
        {
            var setDir = sceneDir.Substring(0, sceneDir.Length - "/scenes".Length);
            return setDir + "/animations";
        }
        return sceneDir + "/animations";
    }
}
