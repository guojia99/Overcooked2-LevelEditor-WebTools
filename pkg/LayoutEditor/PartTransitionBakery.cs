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
    ///       P1 的 Exit 目的地 = 自身 park（缺省 down/60≈50 格）——初始在场偏移 0 由默认态
///       Idle_Active 保证；若 P1 强制零 park，Exit 就是 0→0 空动画（P1 永不退场）。
    ///    c. 每个 slot 根挂独立 QuadGridManager（原版移动地面同构，s_test_level_5 平台
    ///       Area）：桌台 StaticGridLocation 沿父链找到本网格注册；网格坐标经
    ///       transform.TransformPoint 随根移动，slot 升降后占格查询仍正确。不挂则
    ///       parked 桌台注册进主网格（位置被 clamp 到错误格点），升起后不可交互。
    ///       halfSize = 文档侧（items/floors，stub 物件场景侧不可见）∪ 场景侧
    ///       （Renderer/Collider）包围盒换算 —— 两来源缺一不可。
    ///    d. Design/PartTimeline 时间轴链（全原版组件，GameObject.SendTrigger 只达
    ///       同物体接收器 → 全部 TriggerTimer/中继挂在同一个时间轴物体上）：
    ///       稳定计时 → PartSwitch →（sink 再经 outSeconds 延时发 _in）→ Stable_<to>；
    ///       清理 = Stable_<to> + cleanupDelaySeconds → Kill_<from>（slot 根
    ///       TriggerKillAttachments 清内容物——时机为下一 P 上浮完成之后，
    ///       v3.4/D16；被杀道具走原版重生、落到新 P 空闲站台 = 跨 P 携带）。
    ///       v3.4（D15）废除 Design 根全场大扫除（KillLoose_*）：base 层任何东西
    ///       不清扫；陈旧组件由 DestroyTimeline 的 KillLoose_ 前缀清理自愈。
///    e. 清理：已删阶段的 slot 根与动画资产、旧时间轴、旧 KillAttachments。
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
            // park：P1 初始在场恒 0；P2+ 停放于 park 偏移（子树随之移动）。
            root.localPosition = ParkVector(p, i == 0);
            // 移动平台独立网格（交互注册，见类头 c）。
            EnsureSlotGrid(root, p.id, doc);
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

    /// <summary>park 偏移向量：down/up = ∓Y，left/right = ∓X（世界轴）。
    /// isFirst 仅用于 Sync 的初始摆放（P1 开局在场恒零）；BakeSlotAnimator 一律传
    /// false —— Exit/Idle_Park 需要真实退场去向，P1 也不例外。
    /// offset ≤ 0 视为未设置（历史档 JsonUtility null 塌陷值），按缺省 60 兜底
    /// （≈50 格——更小会开局看见停泊平台/转场穿模，v3.4 修订；上限 1000）。</summary>
    private static Vector3 ParkVector(PartDto p, bool isFirst)
    {
        if (isFirst || p == null)
            return Vector3.zero;
        var off = p.park != null && p.park.offset > 0f ? p.park.offset : 60f;
        off = Mathf.Clamp(off, 5f, 1000f);
        var axis = p.park != null && !string.IsNullOrEmpty(p.park.axis) ? p.park.axis : "down";
        if (axis == "up")
            return new Vector3(0f, off, 0f);
        if (axis == "left")
            return new Vector3(-off, 0f, 0f);
        if (axis == "right")
            return new Vector3(off, 0f, 0f);
        return new Vector3(0f, -off, 0f);
    }

    // ---------- slot 局部网格（移动平台桌台交互） ----------

    /// <summary>slot 根挂独立 QuadGridManager：m_size=(1.2,1,1.2)、m_origin=0、
    /// m_gridHalfSize 按内容包围盒自动计算（cell 1.2m 向上取整 +1 格余量）。
    /// 原版先例：移动地面带桌台必须单独加网格（Docs/zh/tutorial「移动地面」、
    /// s_test_level_5 各平台 Area）。幂等：已存在则仅刷新 halfSize；空 slot 移除。</summary>
    private static void EnsureSlotGrid(Transform root, string pid, LayoutDocumentDto doc)
    {
        int halfX, halfZ;
        if (!ComputeSlotHalfSize(root, doc, pid, out halfX, out halfZ))
        {
            var stale = root.GetComponent<QuadGridManager>();
            if (stale != null)
                Undo.DestroyObjectImmediate(stale);
            return;
        }

        var grid = root.GetComponent<QuadGridManager>();
        if (grid == null)
            grid = Undo.AddComponent<QuadGridManager>(root.gameObject);

        var so = new SerializedObject(grid);
        so.Update();
        var sizeProp = so.FindProperty("m_size");
        if (sizeProp != null)
            sizeProp.vector3Value = new Vector3(1.2f, 1f, 1.2f);
        var originProp = so.FindProperty("m_origin");
        if (originProp != null)
            originProp.vector3Value = Vector3.zero;
        WriteGridHalfSize(so, halfX, 1, halfZ);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(grid);
        LayoutEditorLog.Log("[PartLevel] slot 网格 " + pid + "：gridHalfSize=(" + halfX + ",1," + halfZ + ")");
    }

    /// <summary>m_gridHalfSize 双形态写入（Point3 相对属性 / Vector3 回退），
    /// 与 LayoutEditorGridBake / GridReader 的读法保持一致。</summary>
    private static void WriteGridHalfSize(SerializedObject so, int halfX, int halfY, int halfZ)
    {
        var half = so.FindProperty("m_gridHalfSize");
        if (half == null)
            half = so.FindProperty("gridHalfSize");
        if (half == null)
            return;
        var x = half.FindPropertyRelative("X");
        var y = half.FindPropertyRelative("Y");
        var z = half.FindPropertyRelative("Z");
        if (x != null && y != null && z != null)
        {
            x.intValue = halfX;
            y.intValue = halfY;
            z.intValue = halfZ;
        }
        else if (half.propertyType == SerializedPropertyType.Vector3)
        {
            half.vector3Value = new Vector3(halfX, halfY, halfZ);
        }
    }

    /// <summary>slot 内容包围盒 → 网格半宽。两个来源取并集（stub 免疫）：
    /// 1) 文档侧：partId == pid 的 items（worldPosition 点 ± 0.6m）与 floors
    ///    （worldPosition + widthCells/depthCells × 1.2m）——桌台 prefab 是
    ///    stub（无 Renderer/Collider），场景侧看不到，文档是唯一权威；
    /// 2) 场景侧：根下 Renderer/Collider 世界包围盒（真实地板 Plane 等）。
    /// park/转场只动根本身，坐标经 InverseTransformPoint 归一到根局部，
    /// 与 park 轴/深度无关。返回 false = 空 slot（两侧都无内容）。</summary>
    private static bool ComputeSlotHalfSize(Transform root, LayoutDocumentDto doc, string pid,
        out int halfX, out int halfZ)
    {
        halfX = 0;
        halfZ = 0;
        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;
        bool found = false;

        // ---- 文档侧 ----
        if (doc != null && doc.items != null)
        {
            for (int i = 0; i < doc.items.Length; i++)
            {
                var it = doc.items[i];
                if (it == null || it.partId != pid)
                    continue;
                // 布局期 slot 根归零 → 设计局部坐标 == 设计世界坐标；worldPosition
                // 缺失（匹配既有物件的文档）时回退 localPosition，避免引入原点噪声。
                var wp = it.worldPosition != null ? it.worldPosition.ToVector3()
                    : (it.localPosition != null ? it.localPosition.ToVector3() : Vector3.zero);
                AccumulateLocalSphere(root, wp, 0.6f, ref min, ref max, ref found);
            }
        }
        if (doc != null && doc.floors != null)
        {
            for (int i = 0; i < doc.floors.Length; i++)
            {
                var f = doc.floors[i];
                if (f == null || f.partId != pid)
                    continue;
                var wp = f.worldPosition != null ? f.worldPosition.ToVector3() : Vector3.zero;
                var halfW = Mathf.Max(f.widthCells, 0) * 0.6f;
                var halfD = Mathf.Max(f.depthCells, 0) * 0.6f;
                if (halfW > 0f || halfD > 0f)
                    AccumulateLocalBox(root, wp, halfW, halfD, ref min, ref max, ref found);
                else
                    AccumulateLocalSphere(root, wp, 0f, ref min, ref max, ref found);
            }
        }

        // ---- 场景侧（真实 Plane / 碰撞体） ----
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                AccumulateLocalBounds(renderers[i].bounds, root, ref min, ref max, ref found);
        }
        var colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                AccumulateLocalBounds(colliders[i].bounds, root, ref min, ref max, ref found);
        }

        if (!found)
            return false;

        // 网格以 slot 根为中心对称扩展（总格数 2N+1）；须覆盖全部桌台，
        // 否则注册时 GetGridLocationFromPos 会把桌台 clamp 挤到边缘格点。
        const float cell = 1.2f;
        float extentX = Mathf.Max(Mathf.Abs(min.x), Mathf.Abs(max.x));
        float extentZ = Mathf.Max(Mathf.Abs(min.z), Mathf.Abs(max.z));
        halfX = Mathf.Clamp(Mathf.CeilToInt(extentX / cell) + 1, 1, 50);
        halfZ = Mathf.Clamp(Mathf.CeilToInt(extentZ / cell) + 1, 1, 50);
        return true;
    }

    /// <summary>世界坐标点 ± radius 并入根局部 min/max（桌台 stub 的近似占格）。</summary>
    private static void AccumulateLocalSphere(Transform root, Vector3 world, float radius,
        ref Vector3 min, ref Vector3 max, ref bool found)
    {
        var lp = root.InverseTransformPoint(world);
        if (!found)
        {
            min = lp - new Vector3(radius, 0f, radius);
            max = lp + new Vector3(radius, 0f, radius);
            found = true;
            return;
        }
        min = Vector3.Min(min, lp - new Vector3(radius, 0f, radius));
        max = Vector3.Max(max, lp + new Vector3(radius, 0f, radius));
    }

    /// <summary>世界轴对齐盒（中心 + XZ 半宽）四角并入根局部 min/max（地板矩形）。</summary>
    private static void AccumulateLocalBox(Transform root, Vector3 world, float halfW, float halfD,
        ref Vector3 min, ref Vector3 max, ref bool found)
    {
        if (halfW <= 0f && halfD <= 0f)
            return;
        for (int cx = 0; cx < 2; cx++)
        {
            for (int cz = 0; cz < 2; cz++)
            {
                var corner = world + new Vector3(cx == 0 ? -halfW : halfW, 0f, cz == 0 ? -halfD : halfD);
                var lp = root.InverseTransformPoint(corner);
                if (!found)
                {
                    min = lp;
                    max = lp;
                    found = true;
                }
                else
                {
                    min = Vector3.Min(min, lp);
                    max = Vector3.Max(max, lp);
                }
            }
        }
    }

    /// <summary>世界包围盒 8 角点转根局部后并入 min/max（Bounds 直角近似足够：
    /// 网格半宽只需覆盖，不需要紧贴）。</summary>
    private static void AccumulateLocalBounds(Bounds world, Transform root,
        ref Vector3 min, ref Vector3 max, ref bool found)
    {
        for (int cx = 0; cx < 2; cx++)
        {
            for (int cy = 0; cy < 2; cy++)
            {
                for (int cz = 0; cz < 2; cz++)
                {
                    var corner = new Vector3(
                        cx == 0 ? world.min.x : world.max.x,
                        cy == 0 ? world.min.y : world.max.y,
                        cz == 0 ? world.min.z : world.max.z);
                    var local = root.InverseTransformPoint(corner);
                    if (!found)
                    {
                        min = local;
                        max = local;
                        found = true;
                    }
                    else
                    {
                        min = Vector3.Min(min, local);
                        max = Vector3.Max(max, local);
                    }
                }
            }
        }
    }

    // ---------- slot 转场 Animator ----------

    private static Animator BakeSlotAnimator(Scene scene, Transform root, PartLevelDto pl, int index,
        string animDir, string sceneName)
    {
        var pid = pl.parts[index].id;
        // park = 本阶段自身退场去向（P1 缺省 down/60）：Entry 恒 park→0、Exit 恒
        // 0→park；P1 若沿用「初始在场偏移 0」当 park，Exit 会烘成 0→0 空动画。
        var park = ParkVector(pl.parts[index], false);
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

            // 5) 清理链：新 P 就位（Stable_<to>）+ cleanupDelaySeconds → 杀旧 slot
            //    内容物。时机 = 下一 P 上浮完成之后（v3.4/D16 用户决策），此时
            //    新 P 已在场（Idle_Active），被 TakeItem 的锅/盘/灭火器等带重生
            //    行为的道具走原版重生协程、respawnTime 后落到新 P 的空闲站台
            //    ——跨 P 携带语义；非在场 slot 站台由 PartSlotRespawnGuard
            //    （CustomStub Harmony）拦停，不会重生回沉没/停泊站台。
            //    v3.4（D15）：Design 根全场大扫除（KillLoose_*、killLooseEverywhere/
            //    killAttachedEverywhere）整段废除——base 层任何东西不清扫；
            //    陈旧组件由 DestroyTimeline 的 KillLoose_ 前缀清理自愈，存量场景
            //    下次写回自动摘除。手持物保留（Chefs 子树，D6b 不变）。
            Transform fromRoot;
            if (slotRoots.TryGetValue(from.id, out fromRoot))
            {
                if (t == null || t.cleanupContents)
                {
                    var killTimer = Undo.AddComponent<TriggerTimer>(tl.gameObject);
                    killTimer.m_startTrigger = "Stable_" + to.id;
                    // ServerTriggerTimer 只在 m_timer > 0 时倒计时——cleanupDelay=0
                    // 会让 Kill 永不触发（2026-10-15 实测：旧 P 道具全程留在沉没
                    // 平台上），0 延迟按 0.1s 兜底（其余计时器均有同款下限）。
                    killTimer.m_time = Mathf.Max(0.1f, cleanupDelay);
                    killTimer.m_completeTrigger = "Kill_" + from.id;
                    AddObjectRelay(tl, "Kill_" + from.id, fromRoot.gameObject);
                    EnsureKillAttachments(fromRoot.gameObject, "Kill_" + from.id, 3);
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

    /// <summary>幂等挂 TriggerKillAttachments。killMode 位掩码（KillMode.Loose=0
    /// → 位0/值1 散落 IAttachment；Attached=1 → 位1/值2 站台 ServerAttachStation
    /// TakeItem）：1 = 仅 Loose，2 = 仅 Attached，3 = 全杀。</summary>
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
