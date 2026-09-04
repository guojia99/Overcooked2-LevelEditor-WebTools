using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Bakes move-control groups into the same structure the original game levels use:
/// a plain "Design/Animated Objects/&lt;Group&gt;" root GameObject carrying an Animator +
/// the game's TriggerQueue/TriggerTimer components, with AnimationClips that animate
/// each item's localPosition along child paths. Sequencing is fully trigger-driven
/// (TriggerQueue delays + named trigger parameters on the controller), exactly like
/// the reference levels (1_3/3_2/4_3/4_4 counters, 3_4/6_3 islands, ...).
/// </summary>
public static class AnimGroupBakery
{
    private const string AnimatedObjectsRootName = "Animated Objects";
    private const string OldMoveAnimsFolderName = "move_anims";
    private const string QueueStartTrigger = "Start";
    /// <summary>嵌入每组 .controller 的 TextAsset 子资产名：存放本轮烘焙使用的
    /// 原始编排 JSON（AnimGroupDto），回读时优先反序列化它（见
    /// AnimGroupImporter.TryImportFromSource），避免「烘焙→片段分析回读」的
    /// 有损往返（并行簇合并丢事件、镜像路线变路点、触发器丢失等）。</summary>
    internal const string SourceAssetName = "AnimGroupSource";

    // -------------------------------------------------------------- fullscreen fx

    /// <summary>全屏闪电特效的专用灯层级路径（root "Lights" 对齐 4_2 的 Lights
    /// 宿主与导出器/写回器的灯光扫描约定）。烘焙拥有其生命周期：不存在则创建、
    /// 存在则复用；特效组删除后由 CleanupStale 清理。灯光面板不管理它
    /// （SceneLayoutExporter 导出跳过 + SceneLayoutApplier 写回跳过）。</summary>
    internal const string FxLightPath = "Lights/FX_Lightning";

    /// <summary>FX 灯方向/默认色 —— 对齐 oc1_story 4-2 的 day 灯（euler
    /// 42.1/172.48/0，淡蓝白）。基准强度 0：平时全黑，闪电 clip 临时拉高强度。</summary>
    private static readonly Vector3 FxLightEuler = new Vector3(42.1f, 172.48f, 0f);
    private static readonly Color FxLightDefaultColor = new Color(0.868f, 0.885f, 1f);

    /// <summary>抖动宿主包装 rig 名：把相机包进一个空 rig（世界位姿保持），
    /// FX Animator/TriggerQueue 挂 rig 而非相机本体。相机上常有游戏自带的
    /// Camera.controller（PullBackIntro→Camera_Idle 开场拉远），直接接管会毁掉
    /// 开场动画；rig 抖动是叠加效果，运行时相机脚本照常驱动 Camera 子物体。
    /// rig 由本烘焙器独占（按名识别），特效组删除后 CleanupStale 解包回收。</summary>
    internal const string FxShakeRigName = "FX_CameraShake";

    /// <summary>Bakes every group in <paramref name="data"/> into the scene, assigning
    /// auto-generated trigger names / group hierarchy paths back into the DTO.
    /// Returns an error string (non-null) when groups could not be baked.</summary>
    public static string Sync(Scene scene, AnimControlDataDto data)
    {
        try
        {
            return SyncInner(scene, data);
        }
        catch (Exception e)
        {
            // Never let a bake exception abort the apply pass — the caller must
            // still save the scene ("write-back did not persist" symptom).
            LayoutEditorLog.LogWarning("anim group: bake exception: " + e);
            return "动画控制写回异常：" + e.Message;
        }
    }

    private static string SyncInner(Scene scene, AnimControlDataDto data)
    {
        var sceneDir = Path.GetDirectoryName(scene.path);
        if (string.IsNullOrEmpty(sceneDir)) return "Move control: scene path unavailable.";
        var sceneName = Path.GetFileNameWithoutExtension(scene.path);
        var animDir = GetAnimationsFolder(scene.path);

        var usedAssets = new HashSet<string>();
        var errors = new List<string>();

        if (data != null && data.groups != null && data.groups.Length > 0)
        {
            EnsureFolder(animDir);
            // FX 宿主预检：shake→相机、flash→灯，同一宿主同轮只允许一个特效组
            // （TriggerQueue 单 animator 目标）。后来的组报错并跳过烘焙，避免
            // BakeFxGroup 接管覆盖掉先烘组的产物。
            var hostOwner = new Dictionary<string, string>();
            var skipFx = new HashSet<AnimGroupDto>();
            foreach (var g in data.groups)
            {
                if (g == null || g.groupKind != "fx") continue;
                var ft = FxTypeOf(g);
                if (string.IsNullOrEmpty(ft)) continue;
                string owner;
                if (hostOwner.TryGetValue(ft, out owner))
                {
                    errors.Add("特效组「" + (g.displayName ?? "?") + "」与「" + owner +
                        "」同为" + (ft == "shake" ? "抖动（相机宿主）" : "闪电（灯宿主）") +
                        "：每组只允许一个同类特效组，请合并或删除");
                    skipFx.Add(g);
                }
                else
                {
                    hostOwner[ft] = g.displayName ?? "?";
                }
            }
            var assetKeys = new Dictionary<string, string>();
            foreach (var g in data.groups)
            {
                if (g == null) continue;
                if (skipFx.Contains(g)) continue;
                var key = BuildAssetKey(sceneName, g);
                string otherName;
                if (assetKeys.TryGetValue(key, out otherName))
                {
                    LayoutEditorLog.LogWarning("anim group: asset key collision — \"" +
                        (g.displayName ?? "?") + "\" and \"" + otherName +
                        "\" both map to " + key);
                }
                else
                {
                    assetKeys[key] = g.displayName ?? "?";
                }
                var err = BakeGroup(scene, g, animDir, sceneName, usedAssets);
                if (!string.IsNullOrEmpty(err))
                    errors.Add(err);
            }
        }
        else
        {
            LayoutEditorLog.Log("anim group: bake with no groups — cleanup pass only");
        }

        CleanupStale(scene, animDir, sceneName, usedAssets);
        AssetDatabase.SaveAssets();

        // One-time migration: the old external move_controls.json is obsolete —
        // the scene itself is now the single source of truth.
        try
        {
            var jsonPath = Path.Combine(sceneDir, sceneName + "_move_controls.json");
            if (File.Exists(jsonPath))
            {
                File.Delete(jsonPath);
                LayoutEditorLog.Log("anim group: removed obsolete " + jsonPath);
            }
        }
        catch (Exception e)
        {
            LayoutEditorLog.LogWarning("anim group: failed to remove old json: " + e.Message);
        }

        if (errors.Count > 0)
            LayoutEditorLog.LogWarning("anim group: bake errors: " + string.Join("; ", errors.ToArray()));
        return errors.Count > 0 ? string.Join("; ", errors.ToArray()) : null;
    }

    // ------------------------------------------------------------------ resolve

    private static GameObject ResolveGameObject(string instanceId)
    {
        if (!string.IsNullOrEmpty(instanceId) &&
            instanceId.StartsWith("u:", StringComparison.Ordinal))
        {
            int id;
            if (int.TryParse(instanceId.Substring(2), out id))
            {
                var obj = EditorUtility.InstanceIDToObject(id) as GameObject;
                if (obj != null) return obj;
            }
        }
        return null;
    }

    /// <summary>Resolve a member by instance id first, then by the importer-stamped
    /// hierarchyPath (ids go stale across Unity restarts / domain reloads).</summary>
    private static GameObject ResolveMember(string instanceId, Dictionary<string, string> pathById)
    {
        var go = ResolveGameObject(instanceId);
        if (go != null) return go;
        string hp;
        if (!string.IsNullOrEmpty(instanceId) && pathById.TryGetValue(instanceId, out hp))
        {
            var t = LayoutEditorHierarchy.FindByPath(hp);
            if (t != null) return t.gameObject;
        }
        return null;
    }

    private static void FilterDict<TK, TV>(Dictionary<TK, TV> dict, HashSet<TK> liveKeys)
    {
        var stale = new List<TK>();
        foreach (var k in dict.Keys)
        {
            if (!liveKeys.Contains(k)) stale.Add(k);
        }
        for (int i = 0; i < stale.Count; i++) dict.Remove(stale[i]);
    }

    private static void FilterNames(Dictionary<string, string> names,
        Dictionary<string, string> paths, HashSet<string> liveIds)
    {
        FilterDict(names, liveIds);
        FilterDict(paths, liveIds);
    }

    private static void FilterStatic(HashSet<string> set,
        Dictionary<string, string> names, Dictionary<string, string> paths, HashSet<string> liveIds)
    {
        set.RemoveWhere(id => !liveIds.Contains(id));
        FilterDict(names, liveIds);
        FilterDict(paths, liveIds);
    }

    // ------------------------------------------------------------------- groups

    private static string BakeGroup(Scene scene, AnimGroupDto group, string animDir,
        string sceneName, HashSet<string> usedAssets)
    {
        LayoutEditorLog.Log("anim group: baking group \"" + (group.displayName ?? "?") +
            "\" id=" + (group.id ?? "?") + " path=" + (group.groupHierarchyPath ?? "?") +
            " items:" + (group.itemInstanceIds == null ? 0 : group.itemInstanceIds.Length) +
            " floors:" + (group.floorInstanceIds == null ? 0 : group.floorInstanceIds.Length) +
            " objects:" + (group.objectInstanceIds == null ? 0 : group.objectInstanceIds.Length) +
            " events:" + (group.events == null ? 0 : group.events.Length) +
            " waypoints:" + (group.waypoints == null ? 0 : group.waypoints.Length));

        if (group.events == null || group.events.Length == 0)
            return "动画组「" + (group.displayName ?? "?") + "」没有事件";

        // 全屏特效组：无成员，烘焙到特效宿主（相机/专用灯）——独立管线。
        if (group.groupKind == "fx")
            return BakeFxGroup(group, animDir, sceneName, usedAssets);
        foreach (var e in group.events)
        {
            if (e != null && (e.type == "shake" || e.type == "flash"))
                return "动画组「" + (group.displayName ?? "?") + "」包含全屏特效事件（" +
                    e.type + "）：抖动/闪电事件需要特效组（创建时选「全屏特效组」）";
        }

        // Cross-session fallback: instance ids go stale after a Unity restart /
        // domain reload, which would make every member unresolvable ("没有可解析
        // 的物品") and abort the bake. The importer stamps each member's
        // hierarchyPath into memberOffsets/memberStatic, so resolve by path too.
        var pathById = new Dictionary<string, string>();
        foreach (var o in group.memberOffsets ?? new AnimGroupMemberOffsetDto[0])
        {
            if (o == null || string.IsNullOrEmpty(o.instanceId) || string.IsNullOrEmpty(o.hierarchyPath)) continue;
            pathById[o.instanceId] = o.hierarchyPath;
        }
        foreach (var m in group.memberStatic ?? new AnimGroupMemberDto[0])
        {
            if (m == null || string.IsNullOrEmpty(m.instanceId) || string.IsNullOrEmpty(m.hierarchyPath)) continue;
            pathById[m.instanceId] = m.hierarchyPath;
        }

        var members = new List<GameObject>();
        foreach (var id in group.itemInstanceIds ?? new string[0])
        {
            if (string.IsNullOrEmpty(id)) continue;
            var go = ResolveMember(id, pathById);
            if (go == null)
            {
                LayoutEditorLog.LogWarning("anim group: scene object not found for item " +
                    (group.displayName ?? "?") + " (" + id + ")");
                continue;
            }
            members.Add(go);
        }
        foreach (var id in group.floorInstanceIds ?? new string[0])
        {
            if (string.IsNullOrEmpty(id)) continue;
            var go = ResolveMember(id, pathById);
            if (go == null)
            {
                LayoutEditorLog.LogWarning("anim group: scene object not found for floor " +
                    (group.displayName ?? "?") + " (" + id + ")");
                continue;
            }
            members.Add(go);
        }
        foreach (var id in group.objectInstanceIds ?? new string[0])
        {
            if (string.IsNullOrEmpty(id)) continue;
            var go = ResolveMember(id, pathById);
            if (go == null)
            {
                LayoutEditorLog.LogWarning("anim group: scene object not found for member " +
                    (group.displayName ?? "?") + " (" + id + ")");
                continue;
            }
            members.Add(go);
        }
        if (members.Count == 0)
            return "动画组「" + (group.displayName ?? "?") + "」没有可解析的物品或地板";

        LayoutEditorLog.Log("anim group: resolved " + members.Count + " member(s) for \"" +
            (group.displayName ?? "?") + "\": " +
            string.Join(", ", members.ConvertAll(m => m.name).ToArray()));

        // Ensure Design/Animated Objects (exact name; host ResetChild relies on it).
        var animatedRoot = LayoutEditorHierarchy.FindOrCreatePath("Design/" + AnimatedObjectsRootName);
        if (animatedRoot == null)
            return "动画组「" + (group.displayName ?? "?") + "」：无法创建 Design/" + AnimatedObjectsRootName;

        var groupRoot = ResolveOrCreateGroupRoot(group, animatedRoot);
        if (groupRoot == null)
            return "动画组「" + (group.displayName ?? "?") + "」：无法创建组根物体";
        group.groupHierarchyPath = LayoutEditorHierarchy.GetHierarchyPath(groupRoot);

        // 上次写回若因 hierarchyPath 过期克隆了 Col_AirFloor 副本，它们会留在组根
        // 下但不参与本轮 members 动画——Play 期脚下仍踩静止碰撞，岛走了人留原地。
        RemoveStaleFloorColliderChildren(groupRoot, members);

        // 空气地板对齐 oc1_story 3-4：AirFloor(ObjectContainer) + Ground(碰撞)，动画驱动容器。
        for (int i = 0; i < members.Count; i++)
            members[i] = AirFloorRig.EnsureRig(members[i], groupRoot);
        RemoveStaleFloorColliderChildren(groupRoot, members);

        // Reparent items under the group root (world position preserved), giving each
        // a unique direct-child name so animation-curve child paths stay unambiguous.
        // Members already under the root (from a previous bake) must NOT occupy the
        // rename pool themselves — otherwise every write-back would re-append " (1)"
        // to their own name (Floor -> Floor (1) -> Floor (1) (1) ...).
        // Also fold repeated suffixes left over from older builds:
        // "Floor (1) (1) (1)" -> "Floor (1)".
        foreach (var go in members)
        {
            var cleaned = Regex.Replace(go.name, @"(\(\d+\))( \1)+$", "$1");
            if (cleaned != go.name)
            {
                Undo.RecordObject(go, "Layout Editor Move Group");
                go.name = cleaned;
            }
        }
        var childNames = new HashSet<string>();
        foreach (var child in groupRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child.parent != groupRoot) continue;
            if (members.Contains(child.gameObject)) continue;
            childNames.Add(child.name);
        }
        for (int i = 0; i < members.Count; i++)
        {
            var go = members[i];
            if (go.transform.parent != groupRoot)
            {
                Undo.SetTransformParent(go.transform, groupRoot, "Layout Editor Move Group");
            }
            if (!childNames.Add(go.name))
            {
                var newName = go.name;
                int n = 1;
                while (!childNames.Add(newName = go.name + " (" + n + ")")) n++;
                go.name = newName;
            }
            // ObjectContainer（IParentable）必须挂在**成员自身**：烘焙动画动的是各成员的
            // localPosition（组根本身不动），实体（chef/食材容器）经 DynamicLandscapeParenting
            // 从脚下碰撞体向上找到的第一个 IParentable 就是该成员 —— 父挂载到成员才会随
            // 动画走（挂在组根上等于挂到永不移动的物体）。无碰撞的装饰成员挂着无副作用。
            if (!AirFloorRig.IsColliderObject(go) && go.GetComponent<ObjectContainer>() == null)
                Undo.AddComponent<ObjectContainer>(go);
        }

        // User member groups become named sub-roots under the group root; their
        // members are reparented underneath (world position preserved, sub-roots
        // stay at identity so the offset math below keeps working). Only runs when
        // the web document carries memberGroups — existing levels are untouched.
        // Sub-root names avoid ALL direct children (members included).
        var subNames = new HashSet<string>();
        foreach (var child in groupRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child.parent == groupRoot) subNames.Add(child.name);
        }
        foreach (var mg in group.memberGroups ?? new AnimGroupMemberGroupDto[0])
        {
            if (mg == null || string.IsNullOrEmpty(mg.name)) continue;
            var subName = SanitizeFileName(mg.name.Replace('/', '_').Replace('\\', '_'));
            if (string.IsNullOrEmpty(subName)) continue;
            if (!subNames.Add(subName))
            {
                int n = 1;
                while (!subNames.Add(subName = subName + " (" + n + ")")) n++;
            }
            var subRoot = new GameObject(subName);
            Undo.RegisterCreatedObjectUndo(subRoot, "Layout Editor Move Group");
            subRoot.transform.SetParent(groupRoot, false);
            subRoot.transform.localPosition = Vector3.zero;
            subRoot.transform.localRotation = Quaternion.identity;
            subRoot.transform.localScale = Vector3.one;
            foreach (var mid in mg.memberInstanceIds ?? new string[0])
            {
                if (string.IsNullOrEmpty(mid)) continue;
                GameObject go = null;
                for (int mi = 0; mi < members.Count; mi++)
                {
                    if (("u:" + members[mi].GetInstanceID()) == mid)
                    {
                        go = members[mi];
                        break;
                    }
                }
                if (go == null || go.transform.parent != groupRoot) continue;
                Undo.SetTransformParent(go.transform, subRoot.transform, "Layout Editor Move Group");
            }
        }

        // Assign stable trigger names + build waypoint lookup.
        var wpById = new Dictionary<string, AnimGroupWaypointDto>();
        foreach (var w in group.waypoints ?? new AnimGroupWaypointDto[0])
            if (w != null && !string.IsNullOrEmpty(w.id)) wpById[w.id] = w;

        int moveIdx = 0, waitIdx = 0, liftIdx = 0, dropIdx = 0, rotIdx = 0;
        foreach (var e in group.events)
        {
            if (e == null) continue;
            if (string.IsNullOrEmpty(e.triggerName))
            {
                if (e.type == "move") e.triggerName = "Move" + (++moveIdx);
                else if (e.type == "lift") e.triggerName = "Lift" + (++liftIdx);
                else if (e.type == "drop") e.triggerName = "Drop" + (++dropIdx);
                else if (e.type == "rotate") e.triggerName = "Rotate" + (++rotIdx);
                else e.triggerName = "Wait" + (++waitIdx);
            }
        }
        // 时间轴迁移：旧顺序数据（startTime < 0）按 delay 链换算绝对开始时间
        // （0.1s 对齐，与 web 端 migrateGroupTimeline 同一规则）。
        MigrateEventTimeline(group, wpById);
        // 时间轴模型：事件按 startTime 绝对调度（LINQ OrderBy 稳定排序），重叠即并行。
        var animEvents = group.events
            .Where(e => e != null && (e.type == "move" || e.type == "lift" ||
                e.type == "drop" || e.type == "wait" || e.type == "rotate"))
            .OrderBy(e => e.startTime)
            .ToList();
        if (animEvents.Count == 0)
            return "动画组「" + (group.displayName ?? "?") + "」没有可烘焙事件";

        // 时间簇：事件时间区间 [start, start+dur) 与簇区间重叠即并入同一簇，
        // 簇内所有事件烘焙为一个组合 clip（单状态 Animator 的并行 = 曲线合并）。
        var clusters = BuildClusters(animEvents, wpById);
        var queueTriggers = new List<string>();
        var queueDelays = new List<float>();
        float prevClusterStart = 0f;
        for (int ci = 0; ci < clusters.Count; ci++)
        {
            queueTriggers.Add(clusters[ci].events[0].triggerName);
            // TriggerQueue 语义：delays[i] 相对上一触发 firing 时刻（waitForFinished
            // 时相对上一 clip 完成回调）——ΔstartTime 恰好表达时间轴绝对调度。
            queueDelays.Add(Mathf.Max(0f, clusters[ci].start - prevClusterStart));
            prevClusterStart = clusters[ci].start;
        }

        // First waypoint of the first move event anchors every member's start pose;
        // members follow parallel tracks via their stored (or captured) offsets, or
        // time-shifted copies of the route (phase, looping scroll patterns).
        AnimGroupEventDto firstMoveEvent = null;
        foreach (var ev in animEvents)
        {
            if (ev.type == "move")
            {
                firstMoveEvent = ev;
                break;
            }
        }
        var firstWp = firstMoveEvent != null ? FirstWaypoint(firstMoveEvent, wpById) : null;
        var memberOffsets = new Dictionary<string, Vector2>();
        var memberPhases = new Dictionary<string, float>();
        var memberNames = new Dictionary<string, string>();
        var memberPaths = new Dictionary<string, string>();
        // Follow members: pinned at a specific waypoint (+ offset) instead of
        // riding the route. Kept in a separate dict so the write-back below can
        // preserve the link (the offset dict only holds x/z values).
        var memberFollowPos = new Dictionary<string, Vector2>();
        var origOffsetDtos = new Dictionary<string, AnimGroupMemberOffsetDto>();
        // Path-keyed secondaries: after a domain reload the doc's u: ids are stale,
        // so lookups fall back to the member's current hierarchyPath. Hits are
        // re-keyed under the fresh id below so the clip builders see fresh keys.
        var offsetByPath = new Dictionary<string, AnimGroupMemberOffsetDto>();
        var phaseByPath = new Dictionary<string, float>();
        var followByPath = new Dictionary<string, string>();
        foreach (var o in group.memberOffsets ?? new AnimGroupMemberOffsetDto[0])
        {
            if (o == null || string.IsNullOrEmpty(o.instanceId)) continue;
            origOffsetDtos[o.instanceId] = o;
            memberOffsets[o.instanceId] = new Vector2(o.x, o.z);
            if (o.t != 0f) memberPhases[o.instanceId] = o.t;
            if (!string.IsNullOrEmpty(o.followWaypointId))
            {
                AnimGroupWaypointDto fw;
                if (wpById.TryGetValue(o.followWaypointId, out fw))
                {
                    memberFollowPos[o.instanceId] = new Vector2(fw.x, fw.z);
                    if (!string.IsNullOrEmpty(o.hierarchyPath))
                        followByPath[o.hierarchyPath] = o.followWaypointId;
                }
                else
                {
                    LayoutEditorLog.LogWarning("anim group: member " + o.instanceId +
                        " follows unknown waypoint " + o.followWaypointId + " — ignored");
                }
            }
            if (!string.IsNullOrEmpty(o.displayName)) memberNames[o.instanceId] = o.displayName;
            if (!string.IsNullOrEmpty(o.hierarchyPath))
            {
                memberPaths[o.instanceId] = o.hierarchyPath;
                offsetByPath[o.hierarchyPath] = o;
                if (o.t != 0f) phaseByPath[o.hierarchyPath] = o.t;
            }
        }
        var staticSet = new HashSet<string>();
        var staticNames = new Dictionary<string, string>();
        var staticPaths = new Dictionary<string, string>();
        var staticByPath = new HashSet<string>();
        foreach (var m in group.memberStatic ?? new AnimGroupMemberDto[0])
        {
            if (m == null || string.IsNullOrEmpty(m.instanceId)) continue;
            staticSet.Add(m.instanceId);
            if (!string.IsNullOrEmpty(m.displayName)) staticNames[m.instanceId] = m.displayName;
            if (!string.IsNullOrEmpty(m.hierarchyPath))
            {
                staticPaths[m.instanceId] = m.hierarchyPath;
                staticByPath.Add(m.hierarchyPath);
            }
        }
        if (firstWp != null)
        {
            foreach (var go in members)
            {
                var id = "u:" + go.GetInstanceID();
                var goPath = LayoutEditorHierarchy.GetHierarchyPath(go.transform);

                // Stale-id recovery (Unity restarted since the web export): the doc
                // dicts are keyed by dead ids. Recover each entry via the member's
                // current hierarchyPath and re-key it under the fresh id.
                if (!staticSet.Contains(id) && staticByPath.Contains(goPath))
                    staticSet.Add(id);
                if (!memberPhases.ContainsKey(id))
                {
                    float ph;
                    if (phaseByPath.TryGetValue(goPath, out ph)) memberPhases[id] = ph;
                }
                if (!memberFollowPos.ContainsKey(id))
                {
                    string followWpId;
                    if (followByPath.TryGetValue(goPath, out followWpId))
                    {
                        AnimGroupWaypointDto fw;
                        if (wpById.TryGetValue(followWpId, out fw))
                            memberFollowPos[id] = new Vector2(fw.x, fw.z);
                    }
                }
                if (!memberOffsets.ContainsKey(id))
                {
                    AnimGroupMemberOffsetDto byPath;
                    if (offsetByPath.TryGetValue(goPath, out byPath))
                        memberOffsets[id] = new Vector2(byPath.x, byPath.z);
                }

                if (staticSet.Contains(id))
                {
                    if (!staticNames.ContainsKey(id)) staticNames[id] = go.name;
                    if (!staticPaths.ContainsKey(id)) staticPaths[id] = goPath;
                    continue;
                }
                // Phase members keep their scene position (the route is time-shifted,
                // not value-shifted) and are never repositioned.
                float phase;
                if (memberPhases.TryGetValue(id, out phase))
                {
                    if (!memberNames.ContainsKey(id)) memberNames[id] = go.name;
                    if (!memberPaths.ContainsKey(id)) memberPaths[id] = goPath;
                    continue;
                }
                Vector2 off;
                if (!memberOffsets.TryGetValue(id, out off))
                {
                    off = new Vector2(
                        go.transform.localPosition.x - firstWp.Value.x,
                        go.transform.localPosition.z - firstWp.Value.y);
                    memberOffsets[id] = off;
                }
                if (!memberNames.ContainsKey(id)) memberNames[id] = go.name;
                if (!memberPaths.ContainsKey(id)) memberPaths[id] = goPath;
                Undo.RecordObject(go.transform, "Layout Editor Move Group");
                var lp = go.transform.localPosition;
                lp.x = firstWp.Value.x + off.x;
                lp.z = firstWp.Value.y + off.y;
                go.transform.localPosition = lp;
            }
            // Drop entries that did not resolve to a live member this pass (stale
            // ids after a reload) so the write-back below returns fresh ids only.
            var liveIds = new HashSet<string>();
            foreach (var go in members) liveIds.Add("u:" + go.GetInstanceID());
            FilterDict(memberOffsets, liveIds);
            FilterDict(memberPhases, liveIds);
            FilterDict(memberFollowPos, liveIds);
            FilterNames(memberNames, memberPaths, liveIds);
            FilterStatic(staticSet, staticNames, staticPaths, liveIds);
            // Follow link lookup tolerates stale ids: fall back to the path-keyed map.
            Func<string, string> followOf = delegate(string id2)
            {
                AnimGroupMemberOffsetDto od;
                if (origOffsetDtos.TryGetValue(id2, out od) && !string.IsNullOrEmpty(od.followWaypointId))
                    return od.followWaypointId;
                string p;
                string fw;
                if (memberPaths.TryGetValue(id2, out p) && followByPath.TryGetValue(p, out fw))
                    return fw;
                return null;
            };
            group.memberOffsets = memberOffsets
                .Select(kv => new AnimGroupMemberOffsetDto
                {
                    instanceId = kv.Key,
                    x = kv.Value.x,
                    z = kv.Value.y,
                    followWaypointId = followOf(kv.Key),
                    displayName = memberNames.ContainsKey(kv.Key) ? memberNames[kv.Key] : null,
                    hierarchyPath = memberPaths.ContainsKey(kv.Key) ? memberPaths[kv.Key] : null
                })
                .Concat(memberPhases.Select(kv => new AnimGroupMemberOffsetDto
                {
                    instanceId = kv.Key,
                    t = kv.Value,
                    followWaypointId = followOf(kv.Key),
                    displayName = memberNames.ContainsKey(kv.Key) ? memberNames[kv.Key] : null,
                    hierarchyPath = memberPaths.ContainsKey(kv.Key) ? memberPaths[kv.Key] : null
                }))
                .ToArray();
            group.memberStatic = staticSet
                .Select(id => new AnimGroupMemberDto
                {
                    instanceId = id,
                    displayName = staticNames.ContainsKey(id) ? staticNames[id] : null,
                    hierarchyPath = staticPaths.ContainsKey(id) ? staticPaths[id] : null
                })
                .ToArray();
        }

        // Bake clips + controller.
        var key = BuildAssetKey(sceneName, group);
        var controllerPath = animDir + "/" + key + ".controller";
        DeleteAssetIfExists(controllerPath);
        for (int i = 0; i < 64; i++)
        {
            if (!DeleteAssetIfExists(animDir + "/" + key + "_s" + i + ".anim"))
                break;
        }

        var clips = new Dictionary<string, AnimationClip>();
        var clipIndex = 0;
        // Completion events are only emitted when the queue waits for finished
        // (waitForFinished) — otherwise they are pure noise.
        string finishedTrigger = group.waitForFinished
            ? (string.IsNullOrEmpty(group.finishedTrigger) ? "AnimationFinished" : group.finishedTrigger)
            : null;
        // A drop event falls from the height the group was last lifted to; move
        // events after a lift bake a constant Y at that height so the group glides
        // through the air (no per-event height config needed).
        // 语义：seqLiftY 是**相对各成员自身基准高度的位移量**（Δy，非绝对高度）。
        float seqLiftY = 0f;
        // Position where the previous move event's route ends. Pure-Y (lift/drop)
        // and wait clips hold XZ there (not at the bake anchor = first waypoint),
        // otherwise the move -> drop hand-off "flashes back" to the route start.
        Vector2? lastRouteEnd = null;
        // 旋转事件的角度跨事件累积（非循环）：第 N 个旋转从前面所有旋转的终态
        // 继续（same rule as the preview 的 rotY 叠加）。
        float rotAccum = 0f;
        foreach (var cluster in clusters)
        {
            var head = cluster.events[0];
            if (cluster.events.Count > 1)
            {
                // 并行时间簇：移动 + 旋转 / 升降合并为一个组合 clip。
                foreach (var ce in cluster.events)
                {
                    if ((ce.loop || ce.pingpong) && ce.type != "wait")
                        Debug.LogWarning("[LayoutEditor] anim group: event " + ce.triggerName +
                            " loop/pingpong 只在单事件时间簇生效（并行簇内忽略）");
                }
                var mclip = BuildClusterClip(key, "s" + clipIndex++, members, groupRoot,
                    cluster, wpById, memberOffsets, staticSet, memberFollowPos,
                    lastRouteEnd, seqLiftY, rotAccum, finishedTrigger, animDir, usedAssets);
                if (mclip != null) clips[head.triggerName] = mclip;
            }
            else
            {
                var evt = head;
                if (evt.type == "wait")
                {
                    // Constant-position clip of `duration` seconds so the event
                    // survives the scene round-trip.
                    var wclip = BuildWaitClip(key, "s" + clipIndex++, members, groupRoot,
                        evt.duration > 0f ? evt.duration : 1f,
                        finishedTrigger, animDir, usedAssets, memberOffsets, memberFollowPos, lastRouteEnd);
                    if (wclip != null) clips[evt.triggerName] = wclip;
                }
                else if (evt.type == "rotate")
                {
                    var rclip = BuildRotateClip(key, "s" + clipIndex++, members, groupRoot, evt,
                        rotAccum, finishedTrigger, staticSet, animDir, usedAssets);
                    if (rclip != null) clips[evt.triggerName] = rclip;
                }
                else
                {
                    var yp = new ClipProfile();
                    if (evt.type == "lift" || evt.type == "drop")
                    {
                        // Pure-Y clip: XZ holds still, members rise/fall BY yTo
                        // (relative Δy from each member's own height). A lift with
                        // yTo == 0 would bake a constant curve and come back as a
                        // wait event — default the rise to 1 (web UI 同默认值)。
                        yp.pureY = true;
                        yp.isDrop = evt.type == "drop";
                        yp.yTo = evt.type == "lift" && evt.yTo == 0f ? 1f : evt.yTo;
                        yp.ySeconds = Mathf.Max(0.1f, evt.liftSeconds > 0f ? evt.liftSeconds : 1f);
                        // 继承的悬空高度（Δy 累积）：lift 在其上继续抬升，drop 从其下落。
                        yp.dropFrom = seqLiftY;
                        yp.holdPos = lastRouteEnd;
                    }
                    else if (evt.liftHeight > 0f && !evt.loop && !evt.pingpong)
                    {
                        // Integrated lift: rise at the start, move, lower at the end.
                        yp.liftHeight = evt.liftHeight;
                        yp.liftSeconds = Mathf.Max(0f, evt.liftSeconds);
                        yp.dropSeconds = Mathf.Max(0f, evt.dropSeconds);
                    }
                    else if (seqLiftY > 0f)
                    {
                        // Flying after a lift: bake the inherited height as a constant Y.
                        yp.constantY = seqLiftY;
                    }
                    List<AnimGroupWaypointDto> pts = evt.type == "move"
                        ? ResolveRoute(evt, wpById)
                        : new List<AnimGroupWaypointDto>();
                    if (evt.type == "move" && (pts == null || pts.Count == 0))
                    {
                        Debug.LogWarning("[LayoutEditor] anim group: event " + evt.triggerName +
                            " has no resolvable waypoints in group 「" + (group.displayName ?? "?") + "」");
                    }
                    else
                    {
                        var clip = BuildMoveClip(key, "s" + clipIndex++, members, groupRoot, pts,
                            Mathf.Max(0.05f, evt.intervalSeconds > 0f ? evt.intervalSeconds : 2f),
                            (evt.loop || evt.pingpong) && evt.type == "move", evt.pingpong && evt.type == "move",
                            finishedTrigger, memberOffsets, memberPhases, staticSet, animDir, usedAssets, yp, memberFollowPos);
                        if (clip != null) clips[evt.triggerName] = clip;
                    }
                }
            }
            // Fold sequence state across cluster events (in startTime order):
            // lift/drop update the inherited height, moves remember the route end,
            // non-loop rotates accumulate the final angle.
            foreach (var ce in cluster.events)
            {
                // lift：在已抬升高度上继续叠加（连续两次抬起 = 高度累加）。
                if (ce.type == "lift") seqLiftY += ce.yTo != 0f ? ce.yTo : 1f;
                // drop：yTo>0 = 下落指定量（可部分下落），否则落回原高度（Δ 清零）。
                else if (ce.type == "drop")
                    seqLiftY = Mathf.Max(0f, seqLiftY - (ce.yTo != 0f ? ce.yTo : seqLiftY));
                else if (ce.type == "move")
                {
                    var pts2 = ResolveRoute(ce, wpById);
                    if (pts2.Count > 0)
                        lastRouteEnd = new Vector2(pts2[pts2.Count - 1].x, pts2[pts2.Count - 1].z);
                }
                else if (ce.type == "rotate" && !ce.loop)
                {
                    float rd = ce.rotateDegrees > 0f ? Mathf.Min(360f, ce.rotateDegrees) : 180f;
                    rotAccum += (ce.rotateDirection == "ccw" ? -1f : 1f) * rd;
                }
            }
        }
        if (clips.Count == 0)
            return "动画组「" + (group.displayName ?? "?") + "」：所有事件都没有可烘焙内容";

        // Controller: each cluster is one state (keyed by the head event's trigger).
        var clusterHeads = clusters.Select(c => c.events[0]).ToList();
        var controller = BuildController(controllerPath, key, group, clusterHeads, clips);
        if (controller == null)
            return "动画组「" + (group.displayName ?? "?") + "」：AnimatorController 创建失败";
        usedAssets.Add(controllerPath);

        AttachComponents(groupRoot, group, queueTriggers, queueDelays, controller, false);

        // 原始编排数据嵌入 controller 子资产：烘焙产物（Animator/TriggerQueue/
        // 合并 clip）是有损表示，回读时优先用这份原始数据重建 web 模型。
        PersistAuthoringSource(controllerPath, group);
        return null;
    }

    // ------------------------------------------------------------------- fullscreen fx

    /// <summary>特效组的特效类型（组内首个 shake/flash 事件的类型；空组/全 wait
    /// 返回 null）。宿主类别由此决定：shake→相机，flash→FX 灯。</summary>
    internal static string FxTypeOf(AnimGroupDto group)
    {
        if (group == null || group.events == null) return null;
        foreach (var e in group.events)
        {
            if (e == null) continue;
            if (e.type == "shake") return "shake";
            if (e.type == "flash") return "flash";
        }
        return null;
    }

    /// <summary>特效组烘焙：无成员，宿主 = 场景相机（shake，位姿抖动，对齐 OC1
    /// earthquake 的 camera_shake）或 Lights/FX_Lightning 专用方向光（flash，
    /// 基准强度 0，明暗交替对齐 oc1 4-2 的 4_2_Lightning）。宿主挂 Animator +
    /// TriggerQueue（与成员组同构，Importer.Collect 扫描全场景可无损回读）。
    /// 组内仅允许单一特效类型（TriggerQueue 单 animator 目标，两宿主无法共用
    /// 一条队列）；wait 事件只占队列延迟，不烘 clip。</summary>
    private static string BakeFxGroup(AnimGroupDto group, string animDir,
        string sceneName, HashSet<string> usedAssets)
    {
        string fxType = null;
        foreach (var e in group.events)
        {
            if (e == null || e.type == "wait") continue;
            if (e.type != "shake" && e.type != "flash")
                return "特效组「" + (group.displayName ?? "?") + "」包含非特效事件（" +
                    e.type + "）：特效组仅支持 抖动 / 闪电 / 等待";
            if (fxType == null) fxType = e.type;
            else if (fxType != e.type)
                return "特效组「" + (group.displayName ?? "?") +
                    "」同时包含抖动和闪电：宿主不同（相机 / 灯），请拆成两个特效组";
        }
        if (fxType == null)
            return "特效组「" + (group.displayName ?? "?") + "」没有抖动 / 闪电事件";

        GameObject host;
        if (fxType == "shake")
        {
            host = EnsureFxShakeRig();
            if (host == null)
                return "特效组「" + (group.displayName ?? "?") + "」：场景中未找到游戏相机（抖动特效宿主）";
        }
        else
        {
            host = EnsureFxLight(group);
            if (host == null)
                return "特效组「" + (group.displayName ?? "?") + "」：无法创建 / 解析闪电灯 " + FxLightPath;
        }

        // 资产键与旧 clip/controller 清理（同成员组规则）。先删再查宿主占用：
        // 重烘时旧引用随资产销毁置空，不会误判为“被他组占用”。
        var key = BuildAssetKey(sceneName, group);
        var controllerPath = animDir + "/" + key + ".controller";
        DeleteAssetIfExists(controllerPath);
        for (int i = 0; i < 64; i++)
        {
            if (!DeleteAssetIfExists(animDir + "/" + key + "_s" + i + ".anim"))
                break;
        }

        // 宿主独占：只拦截「外来」控制器（不在本场景烘焙目录下的资产，如用户
        // 手工挂的相机动画）。两类残留直接接管：
        //  a) 刚被 DeleteAssetIfExists 删掉的旧引用 —— 资产已销毁，GetAssetPath
        //     为空但 runtimeAnimatorController 在内存里仍非 null（本帧不立即置空）；
        //  b) 本场景前缀下的资产（改名残留 / 上轮烘焙产物）—— SyncInner 的宿主
        //     预检已保证同轮没有别的组占用该宿主，孤儿资产由 CleanupStale 清理。
        var existingAnim = host.GetComponent<Animator>();
        if (existingAnim != null && existingAnim.runtimeAnimatorController != null)
        {
            var existingPath = AssetDatabase.GetAssetPath(existingAnim.runtimeAnimatorController);
            bool foreign = !string.IsNullOrEmpty(existingPath) &&
                !existingPath.Replace('\\', '/')
                    .StartsWith(animDir + "/" + sceneName + "_", StringComparison.Ordinal);
            if (foreign)
                return "特效组「" + (group.displayName ?? "?") + "」：宿主 " + host.name +
                    " 已被外来动画占用（" + host.name + " 上有一个非本编辑器烘焙的 Animator 控制器），请先移除该动画";
        }

        // 触发器命名（与成员组同一规则：Shake1../Flash1../Wait1..）。
        int shakeIdx = 0, flashIdx = 0, waitIdx = 0;
        foreach (var e in group.events)
        {
            if (e == null) continue;
            if (string.IsNullOrEmpty(e.triggerName))
            {
                if (e.type == "shake") e.triggerName = "Shake" + (++shakeIdx);
                else if (e.type == "flash") e.triggerName = "Flash" + (++flashIdx);
                else e.triggerName = "Wait" + (++waitIdx);
            }
        }

        // 时间轴迁移 + 簇装箱（特效组无路点，簇延迟即节奏——同 4_2 的 7×Flash
        // delays [6,9,12,11,6,8,8] 模式）。
        var wpById = new Dictionary<string, AnimGroupWaypointDto>();
        MigrateEventTimeline(group, wpById);
        var animEvents = group.events
            .Where(e => e != null && (e.type == "shake" || e.type == "flash" || e.type == "wait"))
            .OrderBy(e => e.startTime)
            .ToList();
        var clusters = BuildClusters(animEvents, wpById);
        var queueTriggers = new List<string>();
        var queueDelays = new List<float>();
        float prevClusterStart = 0f;
        for (int ci = 0; ci < clusters.Count; ci++)
        {
            queueTriggers.Add(clusters[ci].events[0].triggerName);
            queueDelays.Add(Mathf.Max(0f, clusters[ci].start - prevClusterStart));
            prevClusterStart = clusters[ci].start;
        }

        var clips = new Dictionary<string, AnimationClip>();
        var clipIndex = 0;
        string finishedTrigger = group.waitForFinished
            ? (string.IsNullOrEmpty(group.finishedTrigger) ? "AnimationFinished" : group.finishedTrigger)
            : null;
        foreach (var cluster in clusters)
        {
            // 簇内取首个特效事件烘 clip；纯 wait 簇不烘（队列延迟即停顿）。
            AnimGroupEventDto evt = null;
            foreach (var ce in cluster.events)
            {
                if (ce.type == "shake" || ce.type == "flash") { evt = ce; break; }
            }
            if (evt == null) continue;
            AnimationClip clip = fxType == "shake"
                ? BuildShakeClip(key, "s" + clipIndex++, evt, finishedTrigger, animDir, usedAssets, host.transform)
                : BuildFlashClip(key, "s" + clipIndex++, evt, finishedTrigger, animDir, usedAssets);
            if (clip != null) clips[evt.triggerName] = clip;
        }
        if (clips.Count == 0)
            return "特效组「" + (group.displayName ?? "?") + "」：所有事件都没有可烘焙内容";

        var clusterHeads = clusters.Select(c => c.events[0]).ToList();
        var controller = BuildController(controllerPath, key, group, clusterHeads, clips);
        if (controller == null)
            return "特效组「" + (group.displayName ?? "?") + "」：AnimatorController 创建失败";
        usedAssets.Add(controllerPath);

        AttachComponents(host.transform, group, queueTriggers, queueDelays, controller, true);

        // 雷声接收器：flash clip 内嵌 AudioTrigger 动画事件，宿主必须有
        // AnimatorAudioComponent 才有接收者（对齐 4_2 的 Lights 宿主接线），
        // 否则 Unity 报 "AnimationEvent 'AudioTrigger' has no receiver"。
        bool hasThunder = false;
        foreach (var e in group.events)
        {
            if (e != null && e.type == "flash" && !string.IsNullOrEmpty(e.soundCue))
            {
                hasThunder = true;
                break;
            }
        }
        if (hasThunder && host.GetComponent<AnimatorAudioComponent>() == null)
            Undo.AddComponent<AnimatorAudioComponent>(host);

        PersistAuthoringSource(controllerPath, group);
        return null;
    }

    /// <summary>定位场景游戏相机（抖动特效宿主）：优先 tag=MainCamera，兜底第一
    /// 个启用相机（与 SceneLayoutApplier.FindSceneCamera 同规则）。</summary>
    private static GameObject FindFxSceneCamera()
    {
        var cam = Camera.main;
        if (cam != null)
            return cam.gameObject;

        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid())
            return null;
        foreach (var rootGo in scene.GetRootGameObjects())
        {
            var c = rootGo.GetComponentInChildren<Camera>();
            if (c != null)
                return c.gameObject;
        }
        return null;
    }

    /// <summary>抖动宿主 rig：把场景相机包进 FX_CameraShake 空层（世界位姿保持，
    /// rig 继承相机的局部位姿、相机归零为 rig 的恒等子级）。已包过则直接复用。
    /// 抖动 clip 驱动 rig 的 localPosition/localRotation —— 相机本体（含游戏自带
    /// 的 Camera.controller 与跟随脚本）不受影响，抖动为叠加效果。</summary>
    private static GameObject EnsureFxShakeRig()
    {
        var camGo = FindFxSceneCamera();
        if (camGo == null) return null;
        var t = camGo.transform;
        if (t.parent != null && t.parent.name == FxShakeRigName)
            return t.parent.gameObject;

        var rig = new GameObject(FxShakeRigName);
        Undo.RegisterCreatedObjectUndo(rig, "Layout Editor FX Shake Rig");
        rig.transform.SetParent(t.parent, false);
        rig.transform.localPosition = t.localPosition;
        rig.transform.localRotation = t.localRotation;
        rig.transform.localScale = t.localScale;
        // worldPositionStays：相机保持世界位姿，local 归零 —— rig 顶替它原本的
        // 局部变换，运行时相机脚本继续写 Camera 自身 transform，与 rig 无争用。
        Undo.SetTransformParent(t, rig.transform, "Layout Editor FX Shake Rig");
        return rig;
    }

    /// <summary>解析/创建 FX 闪电灯（Lights/FX_Lightning，方向光，基准强度 0，
    /// 方向/颜色对齐 4_2 的 day 灯；flashColor 取组内首个声明了颜色的事件）。
    /// 灯光容器约定为**根级** "Lights"（导出器/写回器的灯光扫描根）——场景缺
    /// 失时在此创建（FindOrCreatePath 无法建根级物体）；同名残留缺 Light 组件
    /// 则补挂修复，不产生重复物体。</summary>
    private static GameObject EnsureFxLight(AnimGroupDto group)
    {
        Color color = FxLightDefaultColor;
        foreach (var e in group.events)
        {
            if (e == null || e.type != "flash" || string.IsNullOrEmpty(e.flashColor)) continue;
            Color parsed;
            if (ColorUtility.TryParseHtmlString(e.flashColor, out parsed))
            {
                color = parsed;
                break;
            }
        }

        Transform t = LayoutEditorHierarchy.FindByPath(FxLightPath);
        Light light = t != null ? t.GetComponent<Light>() : null;
        if (t != null && light == null)
        {
            // 残留修复：同名物体缺 Light 组件（异常中间态）——补挂而不是另建，
            // 避免出现两个 FX_Lightning。
            light = Undo.AddComponent<Light>(t.gameObject);
        }
        else if (t == null)
        {
            var lightsRoot = LayoutEditorHierarchy.FindByPath("Lights");
            if (lightsRoot == null)
            {
                var rootGo = new GameObject("Lights");
                Undo.RegisterCreatedObjectUndo(rootGo, "Layout Editor FX Light");
                lightsRoot = rootGo.transform;
            }
            var go = new GameObject("FX_Lightning");
            Undo.RegisterCreatedObjectUndo(go, "Layout Editor FX Light");
            go.transform.SetParent(lightsRoot, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localEulerAngles = FxLightEuler;
            go.transform.localScale = Vector3.one;
            t = go.transform;
            light = go.AddComponent<Light>();
        }
        // 基准态每轮重申：强度 0 + 组配色（clip 曲线是相对这基准的明暗交替）。
        Undo.RecordObject(light, "Layout Editor FX Light");
        light.type = LightType.Directional;
        light.shadows = LightShadows.None;
        light.color = color;
        light.intensity = 0f;
        EditorUtility.SetDirty(light);
        return t.gameObject;
    }

    /// <summary>shake 事件 clip：宿主相机 localPosition/localRotation 抖动关键帧。
    /// 固定种子伪随机（种子 = 事件 id 的 FNV 哈希，重烘曲线逐位一致），~15Hz
    /// 采样 + 线性衰减包络，clip 首尾精确停在基准位姿（残留漂移会累积）。
    /// 位姿混合（位移为主 + ±0.5° 滚转/俯仰）对齐 OC1 camera_shake 的观感。</summary>
    private static AnimationClip BuildShakeClip(string key, string suffix, AnimGroupEventDto evt,
        string finishedTrigger, string animDir, HashSet<string> usedAssets, Transform host)
    {
        float amp = evt.shakeAmplitude > 0f ? evt.shakeAmplitude : 0.15f;
        float dur = Mathf.Max(0.1f, evt.duration > 0f ? evt.duration : 2f);
        var clip = new AnimationClip();
        clip.name = key + "_" + suffix;
        clip.legacy = false;
        clip.frameRate = 60f;

        var basePos = host.localPosition;
        var baseEuler = host.localEulerAngles;
        var rng = new System.Random(StableSeed(string.IsNullOrEmpty(evt.id) ? evt.triggerName : evt.id));
        var px = new AnimationCurve();
        var py = new AnimationCurve();
        var pz = new AnimationCurve();
        var rx = new AnimationCurve();
        var ry = new AnimationCurve();
        var rz = new AnimationCurve();
        var rw = new AnimationCurve();
        float step = 1f / 15f;
        int n = Mathf.Max(2, Mathf.CeilToInt(dur / step));
        for (int i = 0; i <= n; i++)
        {
            float t = Mathf.Min(dur, i * step);
            bool edge = i == 0 || t >= dur - 0.0001f;
            float decay = Mathf.Max(0f, 1f - t / dur);
            float jx = edge ? 0f : (float)(rng.NextDouble() * 2.0 - 1.0) * amp * decay;
            float jy = edge ? 0f : (float)(rng.NextDouble() * 2.0 - 1.0) * amp * 0.6f * decay;
            float jz = edge ? 0f : (float)(rng.NextDouble() * 2.0 - 1.0) * amp * decay;
            float jPitch = edge ? 0f : (float)(rng.NextDouble() * 2.0 - 1.0) * 0.3f * decay;
            float jRoll = edge ? 0f : (float)(rng.NextDouble() * 2.0 - 1.0) * 0.5f * decay;
            var q = Quaternion.Euler(baseEuler.x + jPitch, baseEuler.y, baseEuler.z + jRoll);
            px.AddKey(new Keyframe(t, basePos.x + jx));
            py.AddKey(new Keyframe(t, basePos.y + jy));
            pz.AddKey(new Keyframe(t, basePos.z + jz));
            rx.AddKey(new Keyframe(t, q.x));
            ry.AddKey(new Keyframe(t, q.y));
            rz.AddKey(new Keyframe(t, q.z));
            rw.AddKey(new Keyframe(t, q.w));
        }
        SetLinear(px); SetLinear(py); SetLinear(pz);
        SetLinear(rx); SetLinear(ry); SetLinear(rz); SetLinear(rw);
        SetCycleWrap(px); SetCycleWrap(py); SetCycleWrap(pz);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x"), px);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.y"), py);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.z"), pz);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalRotation.x"), rx);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalRotation.y"), ry);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalRotation.z"), rz);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalRotation.w"), rw);

        var path2 = animDir + "/" + key + "_" + suffix + ".anim";
        AssetDatabase.CreateAsset(clip, path2);
        var events = new List<AnimationEvent>();
        if (!string.IsNullOrEmpty(finishedTrigger))
        {
            var animEvent = new AnimationEvent();
            animEvent.functionName = "OnTrigger";
            animEvent.stringParameter = finishedTrigger;
            animEvent.time = dur;
            events.Add(animEvent);
        }
        if (events.Count > 0)
        {
            AnimationUtility.SetAnimationEvents(clip, events.ToArray());
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
        }
        usedAssets.Add(path2);
        return clip;
    }

    /// <summary>flash 事件 clip：FX 灯 m_Intensity 明暗交替。关键帧形状取自
    /// oc1 4-2 的 4_2_Lightning.anim（快攻双闪 + 余晖，归一化后按 flashIntensity
    /// 缩放、按 duration 拉伸）；m_Color 常量曲线写事件配色；soundCue 非空时在
    /// 0.67×时长处内嵌 AudioTrigger 动画事件（4_2 同款雷声）。</summary>
    private static AnimationClip BuildFlashClip(string key, string suffix, AnimGroupEventDto evt,
        string finishedTrigger, string animDir, HashSet<string> usedAssets)
    {
        float peak = evt.flashIntensity > 0f ? evt.flashIntensity : 4f;
        float dur = Mathf.Max(0.1f, evt.duration > 0f ? evt.duration : 0.8f);
        var clip = new AnimationClip();
        clip.name = key + "_" + suffix;
        clip.legacy = false;
        clip.frameRate = 60f;

        // 归一化关键帧（x = t/dur，y = value/peak），实测自 4_2_Lightning.anim：
        // 0 → 峰 → 0.125 → 0.625峰 → 0 →(静默)→ 0.375峰 → 0。
        float[] kx = { 0f, 0.04f, 0.16f, 0.22f, 0.36f, 0.60f, 0.64f, 1f };
        float[] ky = { 0f, 1f, 0.125f, 0.625f, 0f, 0f, 0.375f, 0f };
        var curve = new AnimationCurve();
        for (int i = 0; i < kx.Length; i++)
            curve.AddKey(new Keyframe(kx[i] * dur, ky[i] * peak));
        SetLinear(curve);
        SetCycleWrap(curve);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Light), "m_Intensity"), curve);

        // 事件配色（灯的本体色同时被 EnsureFxLight 重申，这里保证多事件不同色）。
        Color color = FxLightDefaultColor;
        if (!string.IsNullOrEmpty(evt.flashColor))
        {
            Color parsed;
            if (ColorUtility.TryParseHtmlString(evt.flashColor, out parsed)) color = parsed;
        }
        var cr = new AnimationCurve(new Keyframe(0f, color.r), new Keyframe(dur, color.r));
        var cg = new AnimationCurve(new Keyframe(0f, color.g), new Keyframe(dur, color.g));
        var cb = new AnimationCurve(new Keyframe(0f, color.b), new Keyframe(dur, color.b));
        var ca = new AnimationCurve(new Keyframe(0f, color.a), new Keyframe(dur, color.a));
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Light), "m_Color.r"), cr);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Light), "m_Color.g"), cg);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Light), "m_Color.b"), cb);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Light), "m_Color.a"), ca);

        var path2 = animDir + "/" + key + "_" + suffix + ".anim";
        AssetDatabase.CreateAsset(clip, path2);
        var events = new List<AnimationEvent>();
        if (!string.IsNullOrEmpty(evt.soundCue))
        {
            var thunder = new AnimationEvent();
            thunder.functionName = "AudioTrigger";
            thunder.stringParameter = evt.soundCue;
            thunder.time = dur * 0.67f;
            events.Add(thunder);
        }
        if (!string.IsNullOrEmpty(finishedTrigger))
        {
            var animEvent = new AnimationEvent();
            animEvent.functionName = "OnTrigger";
            animEvent.stringParameter = finishedTrigger;
            animEvent.time = dur;
            events.Add(animEvent);
        }
        if (events.Count > 0)
        {
            AnimationUtility.SetAnimationEvents(clip, events.ToArray());
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
        }
        usedAssets.Add(path2);
        return clip;
    }

    /// <summary>稳定种子（FNV-1a 32bit，截正）：同一事件 id 跨会话产生完全一致的
    /// 抖动曲线（重烘不闪变场景 diff）。</summary>
    private static int StableSeed(string s)
    {
        uint h = 2166136261u;
        if (s == null) s = "";
        for (int i = 0; i < s.Length; i++)
            h = (h ^ s[i]) * 16777619u;
        return (int)(h & 0x7fffffff);
    }

    /// <summary>把本轮烘焙使用的原始编排数据（时间轴事件/路点/触发器/成员偏移等）
    /// 序列化为 JSON，作为 TextAsset 子资产嵌入该组的 .controller（随资产一起
    /// 保存/清理，游戏运行时无视）。写入前按 hierarchyPath 把成员 id 重盖章为
    /// 当前会话的 u: id，保证跨 Unity 重启回读时成员仍可解析。</summary>
    private static void PersistAuthoringSource(string controllerPath, AnimGroupDto group)
    {
        var pathById = new Dictionary<string, string>();
        foreach (var o in group.memberOffsets ?? new AnimGroupMemberOffsetDto[0])
        {
            if (o == null || string.IsNullOrEmpty(o.instanceId) || string.IsNullOrEmpty(o.hierarchyPath)) continue;
            pathById[o.instanceId] = o.hierarchyPath;
        }
        foreach (var m in group.memberStatic ?? new AnimGroupMemberDto[0])
        {
            if (m == null || string.IsNullOrEmpty(m.instanceId) || string.IsNullOrEmpty(m.hierarchyPath)) continue;
            pathById[m.instanceId] = m.hierarchyPath;
        }

        var idRemap = new Dictionary<string, string>();
        foreach (var o in group.memberOffsets ?? new AnimGroupMemberOffsetDto[0])
        {
            if (o == null || string.IsNullOrEmpty(o.instanceId)) continue;
            var go = ResolveMember(o.instanceId, pathById);
            if (go == null) continue;
            var fresh = "u:" + go.GetInstanceID();
            idRemap[o.instanceId] = fresh;
            o.instanceId = fresh;
            o.hierarchyPath = LayoutEditorHierarchy.GetHierarchyPath(go.transform);
            if (string.IsNullOrEmpty(o.displayName)) o.displayName = go.name;
        }
        foreach (var m in group.memberStatic ?? new AnimGroupMemberDto[0])
        {
            if (m == null || string.IsNullOrEmpty(m.instanceId)) continue;
            var go = ResolveMember(m.instanceId, pathById);
            if (go == null) continue;
            var fresh = "u:" + go.GetInstanceID();
            idRemap[m.instanceId] = fresh;
            m.instanceId = fresh;
            m.hierarchyPath = LayoutEditorHierarchy.GetHierarchyPath(go.transform);
            if (string.IsNullOrEmpty(m.displayName)) m.displayName = go.name;
        }
        // 平铺成员数组与成员组里的 id 一并重映射（解析不了的保留原值，回读时
        // 由 importer 按场景现状重建这些数组，见 TryImportFromSource）。
        group.itemInstanceIds = RemapMemberIds(group.itemInstanceIds, idRemap, pathById);
        group.floorInstanceIds = RemapMemberIds(group.floorInstanceIds, idRemap, pathById);
        group.objectInstanceIds = RemapMemberIds(group.objectInstanceIds, idRemap, pathById);
        foreach (var mg in group.memberGroups ?? new AnimGroupMemberGroupDto[0])
        {
            if (mg == null || mg.memberInstanceIds == null) continue;
            mg.memberInstanceIds = RemapMemberIds(mg.memberInstanceIds, idRemap, pathById);
        }

        string json;
        try
        {
            json = JsonUtility.ToJson(group, true);
        }
        catch (Exception e)
        {
            LayoutEditorLog.LogWarning("anim group: source serialize failed: " + e.Message);
            return;
        }
        // Controller 本轮已 DeleteAssetIfExists 重建 —— 旧子资产随之销毁，直接嵌入新资产。
        // 嵌入失败不影响烘焙产物本身（回读退化为片段分析），只记警告。
        try
        {
            // Unity 2017 的 TextAsset 无带参构造函数（CS1729）：无参创建后经
            // SerializedObject 写序列化文本字段 m_Script。
            var ta = new TextAsset();
            ta.name = SourceAssetName;
            var so = new SerializedObject(ta);
            var prop = so.FindProperty("m_Script");
            if (prop == null)
            {
                LayoutEditorLog.LogWarning("anim group: source embed failed: TextAsset.m_Script not found");
                return;
            }
            prop.stringValue = json;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.AddObjectToAsset(ta, controllerPath);
            EditorUtility.SetDirty(ta);
        }
        catch (Exception e)
        {
            LayoutEditorLog.LogWarning("anim group: source embed failed: " + e.Message);
        }
    }

    /// <summary>把 id 列表中的成员 id 重映射为当前会话 id：先查 idRemap，
    /// 再尝试按 id/hierarchyPath 直接解析（存活 id 场景内有效）。</summary>
    private static string[] RemapMemberIds(string[] ids, Dictionary<string, string> idRemap,
        Dictionary<string, string> pathById)
    {
        if (ids == null) return null;
        var outIds = new string[ids.Length];
        for (int i = 0; i < ids.Length; i++)
        {
            var id = ids[i];
            if (string.IsNullOrEmpty(id)) { outIds[i] = id; continue; }
            string mapped;
            if (idRemap.TryGetValue(id, out mapped))
            {
                outIds[i] = mapped;
                continue;
            }
            var go = ResolveMember(id, pathById);
            outIds[i] = go != null ? "u:" + go.GetInstanceID() : id;
        }
        return outIds;
    }

    private static Transform ResolveOrCreateGroupRoot(AnimGroupDto group, Transform animatedRoot)
    {
        if (!string.IsNullOrEmpty(group.groupHierarchyPath))
        {
            var existing = LayoutEditorHierarchy.FindByPath(group.groupHierarchyPath);
            if (existing != null) return existing;
        }
        var name = string.IsNullOrEmpty(group.displayName) ? "MoveGroup" : group.displayName;
        // Keep the hierarchy path safe (a "/" in the name would create nested objects).
        name = name.Replace('/', '_').Replace('\\', '_');
        return LayoutEditorHierarchy.FindOrCreatePath(
            "Design/" + AnimatedObjectsRootName + "/" + name);
    }

    private static Vector2? FirstWaypoint(AnimGroupEventDto evt, Dictionary<string, AnimGroupWaypointDto> wpById)
    {
        foreach (var wpId in evt.waypointIds ?? new string[0])
        {
            AnimGroupWaypointDto w;
            if (wpId != null && wpById.TryGetValue(wpId, out w))
                return new Vector2(w.x, w.z);
        }
        return null;
    }

    private static List<AnimGroupWaypointDto> ResolveRoute(AnimGroupEventDto evt, Dictionary<string, AnimGroupWaypointDto> wpById)
    {
        var pts = new List<AnimGroupWaypointDto>();
        foreach (var wpId in evt.waypointIds ?? new string[0])
        {
            AnimGroupWaypointDto w;
            if (wpId != null && wpById.TryGetValue(wpId, out w))
                pts.Add(w);
        }
        return pts;
    }

    // ------------------------------------------------------------------- clips

    /// <summary>Vertical motion profile of a clip: either a pure-Y event (lift/drop,
    /// XZ holds still) or an integrated lift on a move clip (rise → move → lower).
    /// 高度语义为**相对位移 Δy**：每个成员从自身当前高度起升/起降（yTo=位移量），
    /// 台上叠放物品（本地 Y≈1）与工作台（Y=0）在同一组里都能获得升降动画。</summary>
    private class ClipProfile
    {
        public bool pureY;
        /** Pure-Y: 位移量（lift=上升量，0 默认 1；drop=下降量，0 默认落回原高度）。 */
        public float yTo;
        /** Pure-Y: 是否落下事件（相对位移的方向向下）。 */
        public bool isDrop;
        public float ySeconds;
        /** Pure-Y drop: 当前悬空高度（上一 lift 的 Δy 累积）——没有它落下曲线
         *  会从地面高度开始，坠落不可见。 */
        public float dropFrom;
        /** Move events following a lift fly at the inherited height **delta above
         *  each member's own base**（各成员基准高度不同，曲线值须 per-member 加）。 */
        public float constantY;
        public float liftHeight;
        public float liftSeconds;
        public float dropSeconds;
        /** Pure-Y events: XZ position where the previous move event left off (route
         *  end), so the hand-off is seamless — null when no move event precedes
         *  (first event is lift/drop): members hold their current scene position. */
        public Vector2? holdPos;
    }

    /// <summary>Route keyframe timeline. Waypoint waits add a "hold" key at the same
    /// position after arrival (dwell N seconds before continuing to the next node);
    /// per-waypoint segmentSeconds override the uniform interval. Imported routes
    /// keep their original key timing unless any timing was edited.</summary>
    private static void BuildTimeline(List<AnimGroupWaypointDto> pts, float interval,
        out List<float> times, out List<Vector2> values)
    {
        times = new List<float>();
        values = new List<Vector2>();
        bool anyEdit = false;
        for (int i = 0; i < pts.Count; i++)
        {
            if (pts[i].wait > 0f || pts[i].segmentSeconds > 0f)
            {
                anyEdit = true;
                break;
            }
        }
        if (!anyEdit && pts.Count > 0 && pts[0].hasTime)
        {
            // Imported routes keep their original key timing.
            for (int i = 0; i < pts.Count; i++)
            {
                times.Add(pts[i].t);
                values.Add(new Vector2(pts[i].x, pts[i].z));
            }
            return;
        }
        float t = 0f;
        for (int i = 0; i < pts.Count; i++)
        {
            var p = new Vector2(pts[i].x, pts[i].z);
            times.Add(t);
            values.Add(p);
            if (pts[i].wait > 0f)
            {
                times.Add(t + pts[i].wait);
                values.Add(p);
            }
            if (i < pts.Count - 1)
            {
                float seg = pts[i].segmentSeconds > 0f ? pts[i].segmentSeconds : interval;
                t += seg + Mathf.Max(0f, pts[i].wait);
            }
        }
    }

    /// <summary>时间轴粒度：所有时间吸附到 0.1 秒（与 web 端 snapTime 一致）。</summary>
    internal static float SnapTime(float v)
    {
        return Mathf.Max(0f, Mathf.Round(v * 10f) / 10f);
    }

    /// <summary>事件 clip 时长（与 web 端 eventDuration 同一规则）：move = 路线
    /// 关键帧末时间；lift/drop = liftSeconds；rotate = rotateSeconds；wait = duration。</summary>
    internal static float EventDuration(AnimGroupEventDto evt, Dictionary<string, AnimGroupWaypointDto> wpById)
    {
        if (evt.type == "move")
        {
            var pts = ResolveRoute(evt, wpById);
            if (pts.Count == 0) return 0f;
            List<float> times;
            List<Vector2> values;
            BuildTimeline(pts, Mathf.Max(0.05f, evt.intervalSeconds > 0f ? evt.intervalSeconds : 2f),
                out times, out values);
            return times.Count > 0 ? times[times.Count - 1] : 0f;
        }
        if (evt.type == "lift" || evt.type == "drop")
            return Mathf.Max(0.1f, evt.liftSeconds > 0f ? evt.liftSeconds : 1f);
        if (evt.type == "rotate")
            return Mathf.Max(0.1f, evt.rotateSeconds > 0f ? evt.rotateSeconds : 2f);
        return Mathf.Max(0.1f, evt.duration > 0f ? evt.duration : 1f);
    }

    /// <summary>旧顺序事件（startTime &lt; 0）自动迁移为时间轴绝对开始时间：
    /// 首事件 startTime = max(0, delay)，后续按烘焙链规则换算（waitForFinished
    /// 时 delay 叠加在上一 clip 结束之后），全部 0.1s 对齐。</summary>
    internal static void MigrateEventTimeline(AnimGroupDto group, Dictionary<string, AnimGroupWaypointDto> wpById)
    {
        var evts = group.events;
        if (evts == null || evts.Length == 0) return;
        bool need = false;
        foreach (var e in evts)
        {
            if (e != null && e.startTime < 0f) { need = true; break; }
        }
        if (!need) return;
        float acc = 0f, prevDur = 0f;
        bool first = true;
        foreach (var e in evts)
        {
            if (e == null) continue;
            float start;
            if (first)
            {
                start = Mathf.Max(0f, e.delay);
            }
            else if (group.waitForFinished)
            {
                start = acc + prevDur + Mathf.Max(0f, e.delay);
            }
            else
            {
                start = acc + Mathf.Max(prevDur, Mathf.Max(0f, e.delay));
            }
            e.startTime = SnapTime(start);
            prevDur = EventDuration(e, wpById);
            acc = start;
            first = false;
        }
    }

    /// <summary>一组时间区间重叠的事件，烘焙为一个组合 clip。</summary>
    internal class EventCluster
    {
        public readonly List<AnimGroupEventDto> events = new List<AnimGroupEventDto>();
        public float start;
        public float end;
    }

    /// <summary>把按 startTime 排序的事件装箱为时间簇：事件与当前簇区间
    /// 重叠（start &lt; cluster.end）即并入；否则开新簇。</summary>
    internal static List<EventCluster> BuildClusters(List<AnimGroupEventDto> sorted,
        Dictionary<string, AnimGroupWaypointDto> wpById)
    {
        var clusters = new List<EventCluster>();
        foreach (var evt in sorted)
        {
            float s = Mathf.Max(0f, evt.startTime);
            float e = s + EventDuration(evt, wpById);
            var last = clusters.Count > 0 ? clusters[clusters.Count - 1] : null;
            if (last != null && s < last.end - 0.0001f)
            {
                last.events.Add(evt);
                if (e > last.end) last.end = e;
            }
            else
            {
                var c = new EventCluster { start = s, end = e };
                c.events.Add(evt);
                clusters.Add(c);
            }
        }
        return clusters;
    }

    /// <summary>Rotate event clip: every non-static member spins around its own
    /// local Y axis (self-rotation), from the accumulated sequence angle to
    /// +degrees. Quaternion curves keyed every ≤90° so the arc is exact; linear
    /// tangents keep angular velocity constant. Loop = self-transition replay
    /// (BuildController), so clip.loopTime stays off (Unity 2017 gap).</summary>
    private static AnimationClip BuildRotateClip(string key, string suffix,
        List<GameObject> members, Transform groupRoot, AnimGroupEventDto evt,
        float rotAccum, string finishedTrigger, HashSet<string> staticSet,
        string animDir, HashSet<string> usedAssets)
    {
        float deg = evt.rotateDegrees > 0f ? Mathf.Min(360f, evt.rotateDegrees) : 180f;
        float dir = evt.rotateDirection == "ccw" ? -1f : 1f;
        float dur = Mathf.Max(0.1f, evt.rotateSeconds > 0f ? evt.rotateSeconds : 2f);
        var clip = new AnimationClip();
        clip.name = key + "_" + suffix;
        clip.legacy = false;
        clip.frameRate = 60f;
        foreach (var go in members)
        {
            var id = "u:" + go.GetInstanceID();
            var path = GetChildPath(go.transform, groupRoot);
            if (string.IsNullOrEmpty(path)) continue;
            // Static members get constant rotation curves so every clip animates
            // the same path set (the importer rejects mismatched subsets).
            WriteRotationCurves(clip, path, go.transform.localRotation, rotAccum,
                staticSet.Contains(id) ? 0f : dir * deg, 0f, dur, dur);
        }
        var path2 = animDir + "/" + key + "_" + suffix + ".anim";
        AssetDatabase.CreateAsset(clip, path2);
        if (!evt.loop && !string.IsNullOrEmpty(finishedTrigger))
        {
            var animEvent = new AnimationEvent();
            animEvent.functionName = "OnTrigger";
            animEvent.stringParameter = finishedTrigger;
            animEvent.time = dur;
            AnimationUtility.SetAnimationEvents(clip, new AnimationEvent[] { animEvent });
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
        }
        usedAssets.Add(path2);
        return clip;
    }

    /// <summary>写一个成员的自身 Y 轴旋转曲线：localRotation = baseRot *
    /// AngleAxis(from + delta·k, up)，区间 [t0, t0+dur]，每 ≤90° 一个关键帧保证
    /// 圆弧精确（四元数分量线性插值 + 归一化在 ≤90° 内误差可忽略）。</summary>
    private static void WriteRotationCurves(AnimationClip clip, string path, Quaternion baseRot,
        float fromDeg, float deltaDeg, float t0, float dur, float clipLen)
    {
        var cx = new AnimationCurve();
        var cy = new AnimationCurve();
        var cz = new AnimationCurve();
        var cw = new AnimationCurve();
        int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(deltaDeg) / 90f));
        for (int i = 0; i <= steps; i++)
        {
            float k = (float)i / steps;
            float t = Mathf.Min(t0 + dur * k, clipLen);
            var q = baseRot * Quaternion.AngleAxis(fromDeg + deltaDeg * k, Vector3.up);
            cx.AddKey(new Keyframe(t, q.x));
            cy.AddKey(new Keyframe(t, q.y));
            cz.AddKey(new Keyframe(t, q.z));
            cw.AddKey(new Keyframe(t, q.w));
        }
        SetLinear(cx);
        SetLinear(cy);
        SetLinear(cz);
        SetLinear(cw);
        SetCycleWrap(cx);
        SetCycleWrap(cy);
        SetCycleWrap(cz);
        SetCycleWrap(cw);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.x"), cx);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.y"), cy);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.z"), cz);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.w"), cw);
    }

    /// <summary>并行时间簇的组合 clip：移动（XZ 路线）+ 升降（Y）+ 旋转
    /// （localRotation）曲线按各自 startTime 偏移合并进同一时间轴。多移动/多
    /// 升降重叠时最晚开始者生效（与 web 预览一致）；旋转角度全部叠加。</summary>
    private static AnimationClip BuildClusterClip(string key, string suffix,
        List<GameObject> members, Transform groupRoot, EventCluster cluster,
        Dictionary<string, AnimGroupWaypointDto> wpById,
        Dictionary<string, Vector2> memberOffsets, HashSet<string> staticSet,
        Dictionary<string, Vector2> memberFollowPos,
        Vector2? lastRouteEnd, float seqLiftY, float rotAccum,
        string finishedTrigger, string animDir, HashSet<string> usedAssets)
    {
        AnimGroupEventDto moveEvt = null;
        // 簇内全部 lift/drop 事件（已按 startTime 排序）：逐个串接为分段 Δy
        // 曲线 —— move 中途「抬起→继续移动→落下」等组合在簇内也能全部生效
        // （旧实现只保留最后一个 Y 事件，其余静默丢失）。
        var yEvts = new List<AnimGroupEventDto>();
        var rotates = new List<AnimGroupEventDto>();
        foreach (var ce in cluster.events)
        {
            if (ce.type == "move")
            {
                if (moveEvt != null)
                    Debug.LogWarning("[LayoutEditor] anim group: 同一时间簇存在多个移动事件，仅最晚开始的 " +
                        ce.triggerName + " 生效");
                moveEvt = ce;
            }
            else if (ce.type == "lift" || ce.type == "drop")
            {
                yEvts.Add(ce);
            }
            else if (ce.type == "rotate")
            {
                rotates.Add(ce);
            }
        }
        float clipLen = Mathf.Max(0.1f, cluster.end - cluster.start);

        // Move route (clip-internal keys, shifted by the event's offset in the cluster).
        List<float> mtimes = null;
        List<Vector2> mvalues = null;
        float moveRel = 0f;
        if (moveEvt != null)
        {
            var pts = ResolveRoute(moveEvt, wpById);
            if (pts.Count == 0)
            {
                Debug.LogWarning("[LayoutEditor] anim group: event " + moveEvt.triggerName +
                    " has no resolvable waypoints — cluster clip skips its route");
                moveEvt = null;
            }
            else
            {
                BuildTimeline(pts, Mathf.Max(0.05f, moveEvt.intervalSeconds > 0f ? moveEvt.intervalSeconds : 2f),
                    out mtimes, out mvalues);
                moveRel = moveEvt.startTime - cluster.start;
            }
        }
        // lift/drop Y profile (takes precedence over the move's own Y in the cluster).
        // 语义：yTo 为相对位移量 Δy（lift=上升量默认 1；drop=下降量，0=落回原高度）。
        float yRel = 0f;
        bool hasY = yEvts.Count > 0;
        bool integratedY = false;
        if (!hasY && moveEvt != null && moveEvt.liftHeight > 0f)
        {
            // Integrated lift on the move: rise → move → lower (shifted).
            hasY = true;
            integratedY = true;
            yRel = moveRel;
        }
        else if (!hasY && seqLiftY > 0f)
        {
            hasY = true; // constant fly height (inherited Δy)
        }

        var clip = new AnimationClip();
        clip.name = key + "_" + suffix;
        clip.legacy = false;
        clip.frameRate = 60f;
        foreach (var go in members)
        {
            var id = "u:" + go.GetInstanceID();
            var path = GetChildPath(go.transform, groupRoot);
            if (string.IsNullOrEmpty(path)) continue;
            var curPos = go.transform.localPosition;
            if (staticSet.Contains(id))
            {
                SetConstantCurves(clip, path, curPos.x, curPos.y, curPos.z, clipLen);
                // Path-set parity with rotate clips: constant rotation for statics.
                if (rotates.Count > 0)
                    WriteRotationCurves(clip, path, go.transform.localRotation, 0f, 0f, 0f, clipLen, clipLen);
                continue;
            }
            Vector2 off;
            memberOffsets.TryGetValue(id, out off);

            // ---- XZ
            var cx = new AnimationCurve();
            var cz = new AnimationCurve();
            Vector2 followPos;
            if (memberFollowPos.TryGetValue(id, out followPos))
            {
                cx.AddKey(new Keyframe(0f, followPos.x + off.x));
                cx.AddKey(new Keyframe(clipLen, followPos.x + off.x));
                cz.AddKey(new Keyframe(0f, followPos.y + off.y));
                cz.AddKey(new Keyframe(clipLen, followPos.y + off.y));
            }
            else if (moveEvt != null)
            {
                if (moveRel > 0.0001f)
                {
                    // Hold the current position until the move starts in the cluster.
                    float hx = lastRouteEnd.HasValue ? lastRouteEnd.Value.x + off.x : curPos.x;
                    float hz = lastRouteEnd.HasValue ? lastRouteEnd.Value.y + off.y : curPos.z;
                    cx.AddKey(new Keyframe(0f, hx));
                    cz.AddKey(new Keyframe(0f, hz));
                }
                for (int i = 0; i < mtimes.Count; i++)
                {
                    cx.AddKey(new Keyframe(moveRel + mtimes[i], mvalues[i].x + off.x));
                    cz.AddKey(new Keyframe(moveRel + mtimes[i], mvalues[i].y + off.y));
                }
                float lastT = moveRel + mtimes[mtimes.Count - 1];
                if (clipLen > lastT + 0.001f)
                {
                    var last = mvalues[mvalues.Count - 1];
                    cx.AddKey(new Keyframe(clipLen, last.x + off.x));
                    cz.AddKey(new Keyframe(clipLen, last.y + off.y));
                }
            }
            else
            {
                // No move in the cluster: hold where the previous route ended.
                float hx = curPos.x;
                float hz = curPos.z;
                if (lastRouteEnd.HasValue)
                {
                    hx = lastRouteEnd.Value.x + off.x;
                    hz = lastRouteEnd.Value.y + off.y;
                }
                cx.AddKey(new Keyframe(0f, hx));
                cx.AddKey(new Keyframe(clipLen, hx));
                cz.AddKey(new Keyframe(0f, hz));
                cz.AddKey(new Keyframe(clipLen, hz));
            }
            SetLinear(cx);
            SetLinear(cz);
            SetCycleWrap(cx);
            SetCycleWrap(cz);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.x"), cx);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.z"), cz);

            // ---- Y
            if (hasY)
            {
                var cy = new AnimationCurve();
                if (yEvts.Count > 0)
                {
                    // 簇内多个 lift/drop 按 startTime 串接为分段线性 Δy 曲线，
                    // 每个成员从自身基准高度（curPos.y）起算；accum 为悬空高度
                    // 累积（与事件折叠的 seqLiftY 同一规则：lift 累加、drop 减、
                    // 下限 0）。
                    float accum = seqLiftY;
                    float prevT = -1f;
                    float prevV = 0f;
                    // 簇首到首个 Y 事件之间：保持继承高度。
                    float firstRel = Mathf.Max(0f, yEvts[0].startTime - cluster.start);
                    if (firstRel > 0.0001f)
                    {
                        cy.AddKey(new Keyframe(0f, curPos.y + accum));
                        prevT = 0f;
                        prevV = curPos.y + accum;
                    }
                    foreach (var ye in yEvts)
                    {
                        float rel = Mathf.Min(Mathf.Max(0f, ye.startTime - cluster.start), clipLen);
                        float dur = Mathf.Max(0.1f, ye.liftSeconds > 0f ? ye.liftSeconds : 1f);
                        float endT = Mathf.Min(rel + dur, clipLen);
                        float v0 = curPos.y + accum;
                        float d = ye.type == "lift"
                            ? (ye.yTo != 0f ? ye.yTo : 1f)
                            : (ye.yTo != 0f ? ye.yTo : accum);
                        accum = ye.type == "lift" ? accum + d : Mathf.Max(0f, accum - d);
                        float v1 = curPos.y + accum;
                        // 跳过与上一关键帧同时间同值的重复键（back-to-back 事件）。
                        if (prevT < 0f || Mathf.Abs(rel - prevT) > 0.0001f || Mathf.Abs(v0 - prevV) > 0.0001f)
                            cy.AddKey(new Keyframe(rel, v0));
                        cy.AddKey(new Keyframe(endT, v1));
                        prevT = endT;
                        prevV = v1;
                    }
                    if (clipLen > prevT + 0.001f)
                        cy.AddKey(new Keyframe(clipLen, prevV));
                }
                else if (integratedY && moveEvt != null)
                {
                    // Integrated lift profile on the move (rise → hold → lower).
                    float h = moveEvt.liftHeight;
                    float up = Mathf.Max(0f, moveEvt.liftSeconds);
                    float down = Mathf.Max(0f, moveEvt.dropSeconds);
                    float total = Mathf.Max(clipDurationOf(mtimes), up + down);
                    if (up > 0f)
                    {
                        cy.AddKey(new Keyframe(yRel, curPos.y));
                        cy.AddKey(new Keyframe(yRel + up, curPos.y + h));
                    }
                    else
                    {
                        cy.AddKey(new Keyframe(yRel, curPos.y + h));
                    }
                    if (yRel + total - down > yRel + up + 0.001f)
                        cy.AddKey(new Keyframe(yRel + total - down, curPos.y + h));
                    if (down > 0f)
                        cy.AddKey(new Keyframe(Mathf.Min(yRel + total, clipLen), curPos.y));
                }
                else
                {
                    // 继承悬空高度（Δy）：叠加在各成员自身基准高度之上。
                    cy.AddKey(new Keyframe(0f, curPos.y + seqLiftY));
                    cy.AddKey(new Keyframe(clipLen, curPos.y + seqLiftY));
                }
                SetLinear(cy);
                SetCycleWrap(cy);
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.y"), cy);
            }

            // ---- Rotation (all rotate events in the cluster accumulate).
            if (rotates.Count > 0)
            {
                // Key times: cluster bounds + each rotate's start/end + ≤90° substeps.
                var keyTimes = new List<float> { 0f, clipLen };
                foreach (var re in rotates)
                {
                    float rs = re.startTime - cluster.start;
                    float rd = re.rotateDegrees > 0f ? Mathf.Min(360f, re.rotateDegrees) : 180f;
                    float rdur = Mathf.Max(0.1f, re.rotateSeconds > 0f ? re.rotateSeconds : 2f);
                    int steps = Mathf.Max(1, Mathf.CeilToInt(rd / 90f));
                    for (int i = 0; i <= steps; i++)
                        keyTimes.Add(Mathf.Min(rs + rdur * i / steps, clipLen));
                }
                keyTimes.Sort();
                var cxr = new AnimationCurve();
                var cyr = new AnimationCurve();
                var czr = new AnimationCurve();
                var cwr = new AnimationCurve();
                float prevT = -1f;
                foreach (var kt in keyTimes)
                {
                    if (prevT >= 0f && kt - prevT < 0.0001f) continue;
                    prevT = kt;
                    float ang = rotAccum;
                    foreach (var re in rotates)
                    {
                        float rs = re.startTime - cluster.start;
                        if (kt < rs) continue;
                        float rd = re.rotateDegrees > 0f ? Mathf.Min(360f, re.rotateDegrees) : 180f;
                        float rdir = re.rotateDirection == "ccw" ? -1f : 1f;
                        float rdur = Mathf.Max(0.1f, re.rotateSeconds > 0f ? re.rotateSeconds : 2f);
                        ang += rdir * rd * Mathf.Clamp01((kt - rs) / rdur);
                    }
                    var q = go.transform.localRotation * Quaternion.AngleAxis(ang, Vector3.up);
                    cxr.AddKey(new Keyframe(kt, q.x));
                    cyr.AddKey(new Keyframe(kt, q.y));
                    czr.AddKey(new Keyframe(kt, q.z));
                    cwr.AddKey(new Keyframe(kt, q.w));
                }
                SetLinear(cxr);
                SetLinear(cyr);
                SetLinear(czr);
                SetLinear(cwr);
                SetCycleWrap(cxr);
                SetCycleWrap(cyr);
                SetCycleWrap(czr);
                SetCycleWrap(cwr);
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.x"), cxr);
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.y"), cyr);
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.z"), czr);
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.w"), cwr);
            }
        }

        var path2 = animDir + "/" + key + "_" + suffix + ".anim";
        AssetDatabase.CreateAsset(clip, path2);
        if (!string.IsNullOrEmpty(finishedTrigger))
        {
            var animEvent = new AnimationEvent();
            animEvent.functionName = "OnTrigger";
            animEvent.stringParameter = finishedTrigger;
            animEvent.time = clipLen;
            AnimationUtility.SetAnimationEvents(clip, new AnimationEvent[] { animEvent });
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
        }
        usedAssets.Add(path2);
        return clip;
    }

    private static float clipDurationOf(List<float> times)
    {
        return times != null && times.Count > 0 ? times[times.Count - 1] : 0f;
    }


    /// <summary>One clip per animation event (move / lift / drop). Each group member
    /// gets position curves: XZ follows the route (or holds still for pure-Y events)
    /// and Y follows the vertical profile (lift/drop), so workstations can rise and
    /// then move. Static members get constant curves at their current position.
    /// Key times come from the waypoints when present (imported levels), otherwise
    /// from the unified timeline (interval / segmentSeconds / waits).</summary>
    private static AnimationClip BuildMoveClip(string key, string suffix,
        List<GameObject> members, Transform groupRoot, List<AnimGroupWaypointDto> pts, float interval,
        bool loop, bool pingpong, string finishedTrigger, Dictionary<string, Vector2> memberOffsets,
        Dictionary<string, float> memberPhases, HashSet<string> staticSet, string animDir,
        HashSet<string> usedAssets, ClipProfile yp, Dictionary<string, Vector2> memberFollowPos)
    {
        var clip = new AnimationClip();
        clip.name = key + "_" + suffix;
        clip.legacy = false;
        clip.frameRate = 60f;

        List<float> times;
        List<Vector2> values;
        BuildTimeline(pts, interval, out times, out values);

        // XZ keys shift by the rise time so the group stays put while lifting.
        float up = yp != null && !yp.pureY ? Mathf.Max(0f, yp.liftSeconds) : 0f;
        for (int i = 0; i < times.Count; i++) times[i] += up;

        // Clip length: the XZ route (shifted) vs the vertical profile, whichever is longer.
        float xzLast = times.Count > 0 ? times[times.Count - 1] : 0f;
        float clipLen = xzLast;
        if (yp != null && yp.pureY)
            clipLen = Mathf.Max(0.05f, yp.ySeconds);
        else if (yp != null && yp.liftHeight > 0f)
            clipLen = Mathf.Max(xzLast, up + Mathf.Max(0f, yp.dropSeconds));

        // Ping-pong: Mecanim has no native back-and-forth wrap, so the route is
        // mirrored into the clip (A..C..A) and looped forward — the runtime round
        // trip is then just a regular loop.
        if (pingpong && times.Count > 1)
        {
            int n = times.Count;
            for (int i = n - 2; i >= 0; i--)
            {
                times.Add(2f * xzLast - times[i]);
                values.Add(values[i]);
            }
            clipLen = 2f * xzLast;
        }

        // Cycle length for phase wrapping = full clip length (loop clips only).
        float cycleLen = loop && clipLen > 0f ? clipLen : 0f;
        if (cycleLen <= 0f && loop)
            cycleLen = Mathf.Max(0.05f, times.Count > 0 ? times[times.Count - 1] : 1f);

        foreach (var go in members)
        {
            var id = "u:" + go.GetInstanceID();
            var path = GetChildPath(go.transform, groupRoot);
            if (string.IsNullOrEmpty(path)) continue;

            var curPos = go.transform.localPosition;

            // Static members get constant curves at their current local position so
            // the scene round-trips them back as "static" on the next import. They
            // do not participate in vertical motion.
            if (staticSet.Contains(id))
            {
                SetConstantCurves(clip, path, curPos.x, curPos.y, curPos.z);
                continue;
            }

            float phase = 0f;
            bool isPhase = loop && memberPhases.TryGetValue(id, out phase) && phase != 0f;
            var cx = new AnimationCurve();
            var cz = new AnimationCurve();

            // Follow members (pinned to a waypoint): constant XZ at the followed
            // point + member offset — they never ride the route, but still rise
            // and fall with the group's vertical profile below.
            Vector2 followPos;
            if (memberFollowPos.TryGetValue(id, out followPos))
            {
                Vector2 offF;
                memberOffsets.TryGetValue(id, out offF);
                var fcx = new AnimationCurve(
                    new Keyframe(0f, followPos.x + offF.x),
                    new Keyframe(1f, followPos.x + offF.x));
                var fcz = new AnimationCurve(
                    new Keyframe(0f, followPos.y + offF.y),
                    new Keyframe(1f, followPos.y + offF.y));
                SetLinear(fcx);
                SetLinear(fcz);
                SetCycleWrap(fcx);
                SetCycleWrap(fcz);
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.x"), fcx);
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.z"), fcz);
            }
            else if (yp != null && yp.pureY)
            {
                // Pure-Y event: XZ holds still. Hold the route end of the previous
                // move event (holdPos + member offset) when present — using the
                // bake-time position instead would snap the member back to the
                // first-waypoint anchor on the move -> drop transition ("闪回").
                float hx = curPos.x;
                float hz = curPos.z;
                if (yp.holdPos.HasValue)
                {
                    Vector2 offH;
                    memberOffsets.TryGetValue(id, out offH);
                    hx = yp.holdPos.Value.x + offH.x;
                    hz = yp.holdPos.Value.y + offH.y;
                }
                cx.AddKey(new Keyframe(0f, hx));
                cz.AddKey(new Keyframe(0f, hz));
                if (clipLen > 0.05f)
                {
                    cx.AddKey(new Keyframe(clipLen, hx));
                    cz.AddKey(new Keyframe(clipLen, hz));
                }
            }
            else
            {
                for (int i = 0; i < times.Count; i++)
                {
                    float t = isPhase ? WrapTime(times[i] + phase, cycleLen) : times[i];
                    var v = values[i];
                    Vector2 off;
                    memberOffsets.TryGetValue(id, out off);
                    cx.AddKey(new Keyframe(t, v.x + off.x));
                    cz.AddKey(new Keyframe(t, v.y + off.y));
                }
                if (times.Count > 0 && clipLen > times[times.Count - 1] + 0.001f)
                {
                    // Clip extended by the drop phase: hold the final route position.
                    var last = values[times.Count - 1];
                    Vector2 off2;
                    memberOffsets.TryGetValue(id, out off2);
                    cx.AddKey(new Keyframe(clipLen, last.x + off2.x));
                    cz.AddKey(new Keyframe(clipLen, last.y + off2.y));
                }
            }
            SetLinear(cx);
            SetLinear(cz);
            SetCycleWrap(cx);
            SetCycleWrap(cz);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.x"), cx);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.z"), cz);

            // Vertical profile — all non-static members rise/fall together.
            if (yp != null && (yp.pureY || yp.liftHeight > 0f || yp.constantY > 0f))
            {
                var cy = new AnimationCurve();
                if (yp.pureY)
                {
                    // 相对 Δy + 高度累积：从继承的悬空高度（yp.dropFrom）继续
                    // 起升 / 起降 —— 台上叠放物品（Y≈1）与工作台（Y=0）同组都
                    // 有可见动画；连续抬起累加高度；落下默认回到自身基准高度。
                    float yFrom = curPos.y + yp.dropFrom;
                    float fall = yp.isDrop ? (yp.yTo != 0f ? yp.yTo : yp.dropFrom) : 0f;
                    float yTarget = yp.isDrop
                        ? curPos.y + Mathf.Max(0f, yp.dropFrom - fall)
                        : yFrom + yp.yTo;
                    cy.AddKey(new Keyframe(0f, yFrom));
                    cy.AddKey(new Keyframe(clipLen, yTarget));
                }
                else if (yp.constantY > 0f)
                {
                    // Flying after a lift: hold the inherited height delta above each
                    // member's own base for the whole move — the group glides in the
                    // air instead of dropping to 0 (per-member base：台上物品保持
                    // 台面高度 + 位移量，不再被拽回 y=位移量的绝对高度)。
                    cy.AddKey(new Keyframe(0f, curPos.y + yp.constantY));
                    cy.AddKey(new Keyframe(clipLen, curPos.y + yp.constantY));
                }
                else
                {
                    float h = yp.liftHeight;
                    float down = Mathf.Max(0f, yp.dropSeconds);
                    if (up > 0f)
                    {
                        cy.AddKey(new Keyframe(0f, curPos.y));
                        cy.AddKey(new Keyframe(up, curPos.y + h));
                    }
                    else
                    {
                        cy.AddKey(new Keyframe(0f, curPos.y + h));
                    }
                    if (clipLen - down > up + 0.001f)
                        cy.AddKey(new Keyframe(clipLen - down, curPos.y + h));
                    if (down > 0f)
                        cy.AddKey(new Keyframe(clipLen, curPos.y));
                }
                SetLinear(cy);
                SetCycleWrap(cy);
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.y"), cy);
            }
            else
            {
                // Plain XZ move: still bake a constant Y so Play mode does not
                // snap members (e.g. decor at y=-0.8) back to 0 when the clip
                // only animates X/Z.
                SetConstantY(clip, path, curPos.y, clipLen);
            }
        }

        var path2 = animDir + "/" + key + "_" + suffix + ".anim";
        AssetDatabase.CreateAsset(clip, path2);
        // Non-loop clips announce their completion via an animation event so a
        // queue with waitForFinished can advance (original-level pattern: the
        // event calls OnTrigger(finishedTrigger) on the group root's TriggerQueue).
        // Loops do not announce completion — they are replayed by the controller
        // via a self-transition (BuildController), so clip.loopTime is intentionally
        // left off (it does not reliably persist to disk on Unity 2017 builds).
        if (!loop && !string.IsNullOrEmpty(finishedTrigger) && clipLen > 0f)
        {
            var animEvent = new AnimationEvent();
            animEvent.functionName = "OnTrigger";
            animEvent.stringParameter = finishedTrigger;
            animEvent.time = clipLen;
            AnimationUtility.SetAnimationEvents(clip, new AnimationEvent[] { animEvent });
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
        }
        usedAssets.Add(path2);
        return clip;
    }

    /// <summary>Wait event clip: every member holds its position (constant curves,
    /// `seconds` long) so the queue can enter a state and the event survives
    /// re-import. When a move event precedes the wait, members hold the route end
    /// (holdPos + offset) instead of the bake anchor, keeping the hand-off seamless;
    /// members pinned to a waypoint hold their followed position.</summary>
    private static AnimationClip BuildWaitClip(string key, string suffix,
        List<GameObject> members, Transform groupRoot, float seconds, string finishedTrigger,
        string animDir, HashSet<string> usedAssets,
        Dictionary<string, Vector2> memberOffsets, Dictionary<string, Vector2> memberFollowPos,
        Vector2? holdPos)
    {
        seconds = Mathf.Max(0.1f, seconds);
        var clip = new AnimationClip();
        clip.name = key + "_" + suffix;
        clip.legacy = false;
        clip.frameRate = 60f;
        foreach (var go in members)
        {
            var path = GetChildPath(go.transform, groupRoot);
            if (string.IsNullOrEmpty(path)) continue;
            var p = go.transform.localPosition;
            var id = "u:" + go.GetInstanceID();
            Vector2 fpos;
            if (memberFollowPos.TryGetValue(id, out fpos))
            {
                Vector2 off;
                memberOffsets.TryGetValue(id, out off);
                p.x = fpos.x + off.x;
                p.z = fpos.y + off.y;
            }
            else if (holdPos.HasValue)
            {
                Vector2 off;
                memberOffsets.TryGetValue(id, out off);
                p.x = holdPos.Value.x + off.x;
                p.z = holdPos.Value.y + off.y;
            }
            SetConstantCurves(clip, path, p.x, p.y, p.z, seconds);
        }
        var path2 = animDir + "/" + key + "_" + suffix + ".anim";
        AssetDatabase.CreateAsset(clip, path2);
        if (!string.IsNullOrEmpty(finishedTrigger))
        {
            var animEvent = new AnimationEvent();
            animEvent.functionName = "OnTrigger";
            animEvent.stringParameter = finishedTrigger;
            animEvent.time = seconds;
            AnimationUtility.SetAnimationEvents(clip, new AnimationEvent[] { animEvent });
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
        }
        usedAssets.Add(path2);
        return clip;
    }

    private static float WrapTime(float t, float cycleLen)
    {
        if (cycleLen <= 0f) return t;
        t = t % cycleLen;
        if (t < 0f) t += cycleLen;
        return t;
    }

    private static void SetConstantCurves(AnimationClip clip, string path, float x, float y, float z, float duration = 1f)
    {
        duration = Mathf.Max(0.1f, duration);
        var sx = new AnimationCurve(new Keyframe(0f, x), new Keyframe(duration, x));
        var sy = new AnimationCurve(new Keyframe(0f, y), new Keyframe(duration, y));
        var sz = new AnimationCurve(new Keyframe(0f, z), new Keyframe(duration, z));
        SetLinear(sx);
        SetLinear(sy);
        SetLinear(sz);
        SetCycleWrap(sx);
        SetCycleWrap(sy);
        SetCycleWrap(sz);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.x"), sx);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.y"), sy);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.z"), sz);
    }

    private static void SetConstantY(AnimationClip clip, string path, float y, float duration)
    {
        float end = duration > 0.05f ? duration : 1f;
        var cy = new AnimationCurve(
            new Keyframe(0f, y),
            new Keyframe(end, y));
        SetLinear(cy);
        SetCycleWrap(cy);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.y"), cy);
    }

    private static void SetLinear(AnimationCurve curve)
    {
        for (int i = 0; i < curve.length; i++)
        {
            var k = curve[i];
            float inSlope = 0f;
            float outSlope = 0f;
            if (i > 0)
            {
                var p = curve[i - 1];
                float dt = k.time - p.time;
                if (dt > 0.0001f) inSlope = (k.value - p.value) / dt;
            }
            if (i < curve.length - 1)
            {
                var n = curve[i + 1];
                float dt = n.time - k.time;
                if (dt > 0.0001f) outSlope = (n.value - k.value) / dt;
            }
            k.inTangent = inSlope;
            k.outTangent = outSlope;
            curve.MoveKey(i, k);
        }
    }

    private static void SetCycleWrap(AnimationCurve curve)
    {
        // WrapMode.Cycle only exists on Unity 2018.3+; value 2 == Cycle/Loop on all versions.
        curve.preWrapMode = (WrapMode)2;
        curve.postWrapMode = (WrapMode)2;
    }

    // ---------------------------------------------------------------- controller

    private static AnimatorController BuildController(string controllerPath, string key,
        AnimGroupDto group, List<AnimGroupEventDto> animEvents,
        Dictionary<string, AnimationClip> clips)
    {
        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        if (controller == null) return null;
        controller.name = key;

        // Trigger parameters (one per event; waits have no state but keep the param
        // so the queue's SetTrigger is harmless).
        foreach (var evt in group.events)
        {
            if (evt == null || string.IsNullOrEmpty(evt.triggerName)) continue;
            if (controller.parameters.All(p => p.name != evt.triggerName))
                controller.AddParameter(evt.triggerName, AnimatorControllerParameterType.Trigger);
        }

        var sm = controller.layers[0].stateMachine;
        var idleStates = new List<AnimatorState>();
        int idleCount = animEvents.Count + (group.loop ? 0 : 1);
        for (int i = 0; i < idleCount; i++)
        {
            var st = sm.AddState("Idle_" + i);
            st.writeDefaultValues = false;
            idleStates.Add(st);
        }
        if (idleStates.Count > 0)
            sm.defaultState = idleStates[0];

        // Pass 1: create every animation state (clips may be missing -> skipped).
        var moveStates = new List<AnimatorState>();
        var moveEvtIdx = new List<int>(); // animEvents index of each created state
        for (int i = 0; i < animEvents.Count; i++)
        {
            var evt = animEvents[i];
            AnimationClip clip;
            if (!clips.TryGetValue(evt.triggerName, out clip)) continue;
            var move = sm.AddState(evt.triggerName);
            move.motion = clip;
            // Keep non-animated axes (e.g. Y while a pure-XZ move plays after a
            // lift event) at their current value — with writeDefaultValues=true the
            // state would reset them to the scene default height and the group
            // would "fall back" to table height instead of gliding in the air.
            move.writeDefaultValues = false;
            moveStates.Add(move);
            moveEvtIdx.Add(i);
        }

        // Pass 2: transitions. Entry = trigger from the matching idle. Exit rules:
        //  - loop events: self-transition (replay).
        //  - next cluster starts the moment this clip ends (touching time ranges):
        //    chain DIRECTLY to it — the queue fires the next trigger while this
        //    state is still active and Animator trigger-competition would skip
        //    states. Direct chaining plays every clip in order.
        //  - otherwise: exit to the idle so the queue's delay paces the next trigger.
        for (int si = 0; si < moveStates.Count; si++)
        {
            var evt = animEvents[moveEvtIdx[si]];
            var move = moveStates[si];
            var from = idleStates[moveEvtIdx[si]];
            // Ring topology when looping: the last move returns to Idle_0 so the
            // queue's re-fired first trigger finds its transition (original 1_3
            // pattern). Otherwise it lands on a terminal idle with no exits.
            var into = group.loop && moveEvtIdx[si] + 1 >= idleStates.Count
                ? idleStates[0]
                : (moveEvtIdx[si] + 1 < idleStates.Count ? idleStates[moveEvtIdx[si] + 1] : idleStates[moveEvtIdx[si]]);
            var tIn = from.AddTransition(move);
            tIn.hasExitTime = false;
            tIn.exitTime = 0.75f;
            tIn.duration = 0.25f;
            tIn.hasFixedDuration = true;
            tIn.AddCondition(AnimatorConditionMode.If, 0f, evt.triggerName);

            // Looping clips (scrolling patterns like 3_4 islands / 持续旋转的装饰 /
            // 持续地震抖动 / 闪电频闪) stay in their state; only the LAST single-event
            // cluster may loop (并行时间簇内的 loop 已在烘焙时忽略并警告）。The loop
            // is implemented as a self-transition (replay the state at the end of the
            // clip) — reliable even when clip.loopTime does not persist to disk on the
            // game build (Unity 2017 SetAnimationClipSettings gap).
            bool canLoop = evt.type == "move" || evt.type == "rotate" ||
                evt.type == "shake" || evt.type == "flash";
            bool isLoop = (evt.loop || (evt.pingpong && evt.type == "move")) && canLoop &&
                si == moveStates.Count - 1;
            if ((evt.loop || evt.pingpong) && !isLoop)
                Debug.LogWarning("[LayoutEditor] anim group: event " + evt.triggerName +
                    " loop/pingpong 仅在最后一个时间簇（单事件）上生效");
            if (isLoop)
            {
                var tSelf = move.AddTransition(move);
                tSelf.hasExitTime = true;
                tSelf.exitTime = 1f;
                tSelf.duration = 0f;
                tSelf.hasFixedDuration = true;
            }
            else if (si + 1 < moveStates.Count &&
                     animEvents[moveEvtIdx[si + 1]].startTime <= evt.startTime + clips[evt.triggerName].length + 0.0001f)
            {
                // Immediate hand-off to the next cluster (touching time ranges) —
                // plays every clip in sequence without relying on trigger timing.
                var tChain = move.AddTransition(moveStates[si + 1]);
                tChain.hasExitTime = true;
                tChain.exitTime = 1f;
                tChain.duration = 0f;
                tChain.hasFixedDuration = true;
            }
            else
            {
                var tOut = move.AddTransition(into);
                tOut.hasExitTime = true;
                tOut.exitTime = 1f;
                tOut.duration = 0f;
                tOut.hasFixedDuration = true;
            }
        }

        return controller;
    }

    // ---------------------------------------------------------------- components

    private static void AttachComponents(Transform groupRoot, AnimGroupDto group,
        List<string> queueTriggers, List<float> queueDelays, AnimatorController controller,
        bool fxHost)
    {
        var go = groupRoot.gameObject;

        // ObjectContainer（IParentable）：实体（玩家/食材的 _Rigidbody 容器）脚下
        // 碰撞体经 DynamicLandscapeParenting 向上递归找挂载点，找到才 SetParent
        // 随组走。组根挂上后覆盖全部成员（成员子 Col_Floor、组内 Col_AirFloor
        // 都在其子树）；纯装饰组无碰撞体，挂着也无作用。幂等添加。
        // 特效宿主（相机/灯）不在 parenting 链上，跳过。
        if (!fxHost && go.GetComponent<ObjectContainer>() == null)
            Undo.AddComponent<ObjectContainer>(go);

        var anim = go.GetComponent<Animator>();
        if (anim == null)
            anim = Undo.AddComponent<Animator>(go);
        else
            Undo.RecordObject(anim, "Move Control");
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = group.applyRootMotion;
        EditorUtility.SetDirty(anim);

        // TriggerQueue — mirrors the reference levels (1_3/4_4/4_3: waitForFinished=false).
        var queue = go.GetComponent<TriggerQueue>();
        if (queue == null)
            queue = Undo.AddComponent<TriggerQueue>(go);
        else
            Undo.RecordObject(queue, "Move Control");
        queue.m_targetType = TriggerQueue.TriggerType.Animator;
        queue.m_animator = anim;
        // "Start" 是本烘焙器的内部延迟启动哨兵（QueueStartTrigger），不是真正的
        // 外部触发器 —— 用户在 web 端选它表示「开局自动 / 延迟启动」。若把它当
        // 外部触发器写入 m_startTrigger，没有任何物体会广播 "Start"，组永不启动。
        bool externalStart = !string.IsNullOrEmpty(group.startTrigger) &&
            group.startTrigger != QueueStartTrigger;
        queue.m_startTrigger = externalStart
            ? group.startTrigger
            : (group.startDelay > 0f ? QueueStartTrigger : null);
        queue.m_cancelTrigger = string.IsNullOrEmpty(group.cancelTrigger) ? null : group.cancelTrigger;
        queue.m_endTrigger = string.IsNullOrEmpty(group.endTrigger) ? null : group.endTrigger;
        queue.m_endTriggerTarget = null;
        queue.m_startOnAwake = string.IsNullOrEmpty(queue.m_startTrigger) && group.startDelay <= 0f;
        queue.m_loopWhenFinished = group.loop;
        queue.m_loopDelay = Mathf.Max(0f, group.loopDelay);
        queue.m_waitForFinished = group.waitForFinished;
        queue.m_finishedTrigger = string.IsNullOrEmpty(group.finishedTrigger)
            ? "AnimationFinished"
            : group.finishedTrigger;
        queue.m_queue.m_triggers = queueTriggers.ToArray();
        queue.m_queue.m_delays = queueDelays.ToArray();
        EditorUtility.SetDirty(queue);

        // TriggerTimer — delayed start (4_1 / lost_morsel 1_1 pattern). The timer
        // fires the queue's start trigger when a custom external start is configured.
        var timer = go.GetComponent<TriggerTimer>();
        if (group.startDelay > 0f)
        {
            if (timer == null)
                timer = Undo.AddComponent<TriggerTimer>(go);
            else
                Undo.RecordObject(timer, "Move Control");
            timer.m_startTrigger = null;
            timer.m_completeTrigger = externalStart ? group.startTrigger : QueueStartTrigger;
            timer.m_time = Mathf.Max(0f, group.startDelay);
            timer.m_startTiming = true;
            timer.m_triggerAtStart = false;
            EditorUtility.SetDirty(timer);
        }
        else if (timer != null)
        {
            Undo.DestroyObjectImmediate(timer);
        }
    }

    // ------------------------------------------------------------------- cleanup

    private static void CleanupStale(Scene scene, string animDir, string sceneName, HashSet<string> usedAssets)
    {
        var prefix = animDir + "/" + sceneName + "_";
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var anim in root.GetComponentsInChildren<Animator>(true))
            {
                var rc = anim.runtimeAnimatorController;
                if (rc == null) continue;
                var path = AssetDatabase.GetAssetPath(rc);
                if (string.IsNullOrEmpty(path)) continue;
                var norm = path.Replace('\\', '/');
                // Button-link logic controllers (BtnLogic_/BtnPair_) are managed by
                // ButtonLinkBakery — never treat them as stale move-control assets.
                if (ButtonLinkBakery.IsButtonLogicAsset(norm)) continue;
                bool ours = norm.StartsWith(prefix, StringComparison.Ordinal) ||
                            norm.Contains("/" + OldMoveAnimsFolderName + "/");
                if (!ours) continue;
                if (usedAssets.Contains(norm)) continue;

                // The group no longer exists: drop its Animator AND the queue/timer
                // that reference it, otherwise the runtime fires TriggerQueue with a
                // dangling m_animator (UnassignedReferenceException).
                var go = anim.gameObject;
                if (PrefabHasOwnAnimator(go))
                {
                    Undo.RecordObject(anim, "Move Control Cleanup");
                    anim.runtimeAnimatorController = null;
                    EditorUtility.SetDirty(anim);
                }
                else
                {
                    Undo.DestroyObjectImmediate(anim);
                }
                DestroyMoveComponents(go);
            }
        }

        // Orphan TriggerQueues (group roots whose Animator is already gone or was
        // never attached) would fire with a null m_animator at runtime — remove them.
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var q in root.GetComponentsInChildren<TriggerQueue>(true))
            {
                if (q.m_animator != null) continue;
                if (q.GetComponent<Animator>() != null) continue;
                LayoutEditorLog.LogWarning("anim group: cleanup orphan TriggerQueue on " +
                    LayoutEditorHierarchy.GetHierarchyPath(q.transform) + " (no Animator)");
                var go = q.gameObject;
                Undo.RecordObject(q, "Move Control Cleanup");
                Undo.DestroyObjectImmediate(q);
                DestroyMoveComponents(go);
            }
        }

        // FX 闪电灯：宿主 Animator 已被上一段清掉（特效组不存在了）时，移除烘焙
        // 拥有的专用灯。还有 Animator 挂着（本轮烘焙 / 外来动画）则不动。
        var fxLight = LayoutEditorHierarchy.FindByPath(FxLightPath);
        if (fxLight != null && fxLight.GetComponent<Animator>() == null)
        {
            LayoutEditorLog.Log("anim group: cleanup orphan FX light " + FxLightPath);
            Undo.DestroyObjectImmediate(fxLight.gameObject);
        }

        // FX 抖动 rig：宿主 Animator 已被摘除（特效组不存在了）时解包 —— 相机回到
        // rig 原父级（世界位姿保持），销毁空 rig。还有 Animator（本轮烘焙）不动。
        var staleRigs = new List<Transform>();
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var tr in root.GetComponentsInChildren<Transform>(true))
            {
                if (tr.name != FxShakeRigName) continue;
                if (tr.GetComponent<Animator>() != null) continue;
                staleRigs.Add(tr);
            }
        }
        foreach (var rig in staleRigs)
        {
            LayoutEditorLog.Log("anim group: unwrap orphan FX shake rig " +
                LayoutEditorHierarchy.GetHierarchyPath(rig));
            var rigParent = rig.parent;
            var children = new List<Transform>();
            for (int i = 0; i < rig.childCount; i++) children.Add(rig.GetChild(i));
            foreach (var child in children)
                Undo.SetTransformParent(child, rigParent, "Move Control Cleanup");
            Undo.DestroyObjectImmediate(rig.gameObject);
        }

        if (!AssetDatabase.IsValidFolder(animDir)) return;
        foreach (var guid in AssetDatabase.FindAssets("t:Object", new[] { animDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
            if (ButtonLinkBakery.IsButtonLogicAsset(path)) continue;
            if (!path.StartsWith(prefix, StringComparison.Ordinal)) continue;
            if (usedAssets.Contains(path)) continue;
            AssetDatabase.DeleteAsset(path);
        }

        // One-time migration: drop the old move_anims/<scene> folder tree.
        var sceneDir = Path.GetDirectoryName(scene.path);
        var oldFolder = (sceneDir ?? "").Replace('\\', '/') + "/" + OldMoveAnimsFolderName + "/" + sceneName;
        if (AssetDatabase.IsValidFolder(oldFolder))
            AssetDatabase.DeleteAsset(oldFolder);
        var oldRoot = (sceneDir ?? "").Replace('\\', '/') + "/" + OldMoveAnimsFolderName;
        if (AssetDatabase.IsValidFolder(oldRoot) &&
            AssetDatabase.FindAssets("t:Object", new[] { oldRoot }).Length == 0)
            AssetDatabase.DeleteAsset(oldRoot);
    }

    /// <summary>Remove the TriggerQueue/TriggerTimer attached to a stale move-control
    /// group root so they cannot fire with a dangling m_animator at runtime.</summary>
    private static void DestroyMoveComponents(GameObject go)
    {
        var queue = go.GetComponent<TriggerQueue>();
        if (queue != null)
        {
            Undo.RecordObject(queue, "Move Control Cleanup");
            Undo.DestroyObjectImmediate(queue);
        }
        var timer = go.GetComponent<TriggerTimer>();
        if (timer != null)
        {
            Undo.RecordObject(timer, "Move Control Cleanup");
            Undo.DestroyObjectImmediate(timer);
        }
    }

    private static bool PrefabHasOwnAnimator(GameObject go)
    {
        var prefab = PrefabUtility.GetPrefabParent(go) as GameObject;
        return prefab != null && prefab.GetComponent<Animator>() != null;
    }

    // ------------------------------------------------------------------- utils

    public static string GetAnimationsFolder(string sceneAssetPath)
    {
        var parts = (sceneAssetPath ?? "").Replace('\\', '/').Split('/');
        // Assets/LevelSets/<set>/scenes/<scene>.unity -> Assets/LevelSets/<set>
        string levelSet = null;
        for (int i = 0; i < parts.Length - 2; i++)
        {
            if (parts[i] == "LevelSets")
            {
                levelSet = string.Join("/", parts, 0, i + 2);
                break;
            }
        }
        if (string.IsNullOrEmpty(levelSet))
            levelSet = Path.GetDirectoryName(sceneAssetPath).Replace('\\', '/');
        return levelSet + "/animations";
    }

    /// <summary>Drop direct children of the group root that are walk colliders left
    /// over from a previous bake but are no longer listed as members.</summary>
    private static void RemoveStaleFloorColliderChildren(Transform groupRoot, List<GameObject> members)
    {
        if (groupRoot == null || members == null)
            return;
        var memberSet = new HashSet<GameObject>(members);
        var doomed = new List<GameObject>();
        for (int i = 0; i < groupRoot.childCount; i++)
        {
            var child = groupRoot.GetChild(i);
            if (child == null || memberSet.Contains(child.gameObject))
                continue;
            if (!AirFloorRig.IsMoveGroupWalkCollider(child.gameObject))
                continue;
            doomed.Add(child.gameObject);
        }
        foreach (var go in doomed)
            Undo.DestroyObjectImmediate(go);
    }

    private static string GetChildPath(Transform child, Transform root)
    {
        if (child == root) return "";
        var names = new Stack<string>();
        var cur = child;
        while (cur != null && cur != root)
        {
            names.Push(cur.name);
            cur = cur.parent;
        }
        if (cur == null) return "";
        return string.Join("/", names.ToArray());
    }

    internal static void EnsureFolder(string animDir)
    {
        var parent = Path.GetDirectoryName(animDir).Replace('\\', '/');
        var leaf = Path.GetFileName(animDir);
        if (!AssetDatabase.IsValidFolder(parent))
        {
            var grand = Path.GetDirectoryName(parent).Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(grand))
                AssetDatabase.CreateFolder(grand, Path.GetFileName(parent));
        }
        if (!AssetDatabase.IsValidFolder(animDir))
            AssetDatabase.CreateFolder(parent, leaf);
    }

    internal static bool DeleteAssetIfExists(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) == null)
            return false;
        AssetDatabase.DeleteAsset(path);
        return true;
    }

    internal static string BuildAssetKey(string sceneName, AnimGroupDto group)
    {
        var namePart = DeriveNamePart(group.displayName);
        var idPart = DeriveIdPart(group);
        return SanitizeFileName(sceneName + "_" + namePart + "_" + idPart);
    }

    private static string DeriveNamePart(string displayName)
    {
        var name = displayName ?? "group";
        foreach (var c in name)
        {
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                return SanitizeFileName(name.Replace(" ", "_"));
        }
        return "group";
    }

    /// <summary>Stable short suffix for animation asset filenames. Scene-imported
    /// groups must not use id.Substring(0, 8) — every "scene:Design/..." id shares
    /// the same prefix and would collide for Chinese display names.</summary>
    private static string DeriveIdPart(AnimGroupDto group)
    {
        if (string.IsNullOrEmpty(group.id))
            return "g";

        const string scenePrefix = "scene:";
        if (group.id.StartsWith(scenePrefix, StringComparison.Ordinal))
        {
            var path = !string.IsNullOrEmpty(group.groupHierarchyPath)
                ? group.groupHierarchyPath
                : group.id.Substring(scenePrefix.Length);
            return ShortHash(path);
        }

        var compact = group.id.Replace("-", "");
        if (compact.Length > 8)
            compact = compact.Substring(0, 8);
        return SanitizeFileName(compact);
    }

    internal static string ShortHash(string input)
    {
        using (var md5 = MD5.Create())
        {
            var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input ?? ""));
            var sb = new StringBuilder(8);
            for (int i = 0; i < 4; i++)
                sb.Append(bytes[i].ToString("x2"));
            return sb.ToString();
        }
    }

    internal static string SanitizeFileName(string s)
    {
        if (string.IsNullOrEmpty(s)) return "group";
        var chars = s.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                      (c >= '0' && c <= '9') || c == '_' || c == '-';
            if (!ok) chars[i] = '_';
        }
        if (chars.Length > 80)
            return new string(chars, 0, 80);
        return new string(chars);
    }
}
