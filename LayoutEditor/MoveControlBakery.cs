using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
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
public static class MoveControlBakery
{
    private const string AnimatedObjectsRootName = "Animated Objects";
    private const string OldMoveAnimsFolderName = "move_anims";
    private const string QueueStartTrigger = "Start";

    /// <summary>Bakes every group in <paramref name="data"/> into the scene, assigning
    /// auto-generated trigger names / group hierarchy paths back into the DTO.
    /// Returns an error string (non-null) when groups could not be baked.</summary>
    public static string Sync(Scene scene, MoveControlDataDto data)
    {
        try
        {
            return SyncInner(scene, data);
        }
        catch (Exception e)
        {
            // Never let a bake exception abort the apply pass — the caller must
            // still save the scene ("write-back did not persist" symptom).
            LayoutEditorLog.LogWarning("move control: bake exception: " + e);
            return "移动控制写回异常：" + e.Message;
        }
    }

    private static string SyncInner(Scene scene, MoveControlDataDto data)
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
            var assetKeys = new Dictionary<string, string>();
            foreach (var g in data.groups)
            {
                if (g == null) continue;
                var key = BuildAssetKey(sceneName, g);
                string otherName;
                if (assetKeys.TryGetValue(key, out otherName))
                {
                    LayoutEditorLog.LogWarning("move control: asset key collision — \"" +
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
            LayoutEditorLog.Log("move control: bake with no groups — cleanup pass only");
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
                LayoutEditorLog.Log("move control: removed obsolete " + jsonPath);
            }
        }
        catch (Exception e)
        {
            LayoutEditorLog.LogWarning("move control: failed to remove old json: " + e.Message);
        }

        if (errors.Count > 0)
            LayoutEditorLog.LogWarning("move control: bake errors: " + string.Join("; ", errors.ToArray()));
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

    private static string BakeGroup(Scene scene, MoveGroupDto group, string animDir,
        string sceneName, HashSet<string> usedAssets)
    {
        LayoutEditorLog.Log("move control: baking group \"" + (group.displayName ?? "?") +
            "\" id=" + (group.id ?? "?") + " path=" + (group.groupHierarchyPath ?? "?") +
            " items:" + (group.itemInstanceIds == null ? 0 : group.itemInstanceIds.Length) +
            " floors:" + (group.floorInstanceIds == null ? 0 : group.floorInstanceIds.Length) +
            " objects:" + (group.objectInstanceIds == null ? 0 : group.objectInstanceIds.Length) +
            " events:" + (group.events == null ? 0 : group.events.Length) +
            " waypoints:" + (group.waypoints == null ? 0 : group.waypoints.Length));

        if (group.events == null || group.events.Length == 0)
            return "移动组「" + (group.displayName ?? "?") + "」没有事件";

        // Cross-session fallback: instance ids go stale after a Unity restart /
        // domain reload, which would make every member unresolvable ("没有可解析
        // 的物品") and abort the bake. The importer stamps each member's
        // hierarchyPath into memberOffsets/memberStatic, so resolve by path too.
        var pathById = new Dictionary<string, string>();
        foreach (var o in group.memberOffsets ?? new MoveGroupMemberOffsetDto[0])
        {
            if (o == null || string.IsNullOrEmpty(o.instanceId) || string.IsNullOrEmpty(o.hierarchyPath)) continue;
            pathById[o.instanceId] = o.hierarchyPath;
        }
        foreach (var m in group.memberStatic ?? new MoveGroupMemberDto[0])
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
                LayoutEditorLog.LogWarning("move control: scene object not found for item " +
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
                LayoutEditorLog.LogWarning("move control: scene object not found for floor " +
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
                LayoutEditorLog.LogWarning("move control: scene object not found for member " +
                    (group.displayName ?? "?") + " (" + id + ")");
                continue;
            }
            members.Add(go);
        }
        if (members.Count == 0)
            return "移动组「" + (group.displayName ?? "?") + "」没有可解析的物品或地板";

        LayoutEditorLog.Log("move control: resolved " + members.Count + " member(s) for \"" +
            (group.displayName ?? "?") + "\": " +
            string.Join(", ", members.ConvertAll(m => m.name).ToArray()));

        // Ensure Design/Animated Objects (exact name; host ResetChild relies on it).
        var animatedRoot = LayoutEditorHierarchy.FindOrCreatePath("Design/" + AnimatedObjectsRootName);
        if (animatedRoot == null)
            return "移动组「" + (group.displayName ?? "?") + "」：无法创建 Design/" + AnimatedObjectsRootName;

        var groupRoot = ResolveOrCreateGroupRoot(group, animatedRoot);
        if (groupRoot == null)
            return "移动组「" + (group.displayName ?? "?") + "」：无法创建组根物体";
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
        foreach (var mg in group.memberGroups ?? new MoveGroupMemberGroupDto[0])
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
        var wpById = new Dictionary<string, MoveGroupWaypointDto>();
        foreach (var w in group.waypoints ?? new MoveGroupWaypointDto[0])
            if (w != null && !string.IsNullOrEmpty(w.id)) wpById[w.id] = w;

        int moveIdx = 0, waitIdx = 0, liftIdx = 0, dropIdx = 0;
        var animEvents = new List<MoveGroupEventDto>();
        var queueTriggers = new List<string>();
        var queueDelays = new List<float>();
        foreach (var e in group.events)
        {
            if (e == null) continue;
            if (string.IsNullOrEmpty(e.triggerName))
            {
                if (e.type == "move") e.triggerName = "Move" + (++moveIdx);
                else if (e.type == "lift") e.triggerName = "Lift" + (++liftIdx);
                else if (e.type == "drop") e.triggerName = "Drop" + (++dropIdx);
                else e.triggerName = "Wait" + (++waitIdx);
            }
            queueTriggers.Add(e.triggerName);
            queueDelays.Add(Mathf.Max(0f, e.delay));
            // Wait events also get a clip (constant position) so the scene round-trip
            // can restore them — without a state/clip they would silently vanish on
            // re-import ("events swallowed").
            if (e.type == "move" || e.type == "lift" || e.type == "drop" || e.type == "wait") animEvents.Add(e);
        }
        if (animEvents.Count == 0)
            return "移动组「" + (group.displayName ?? "?") + "」没有可烘焙事件";
        if (queueTriggers.Count != group.events.Length)
            return "移动组「" + (group.displayName ?? "?") + "」事件为空";

        // First waypoint of the first move event anchors every member's start pose;
        // members follow parallel tracks via their stored (or captured) offsets, or
        // time-shifted copies of the route (phase, looping scroll patterns).
        MoveGroupEventDto firstMoveEvent = null;
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
        var origOffsetDtos = new Dictionary<string, MoveGroupMemberOffsetDto>();
        // Path-keyed secondaries: after a domain reload the doc's u: ids are stale,
        // so lookups fall back to the member's current hierarchyPath. Hits are
        // re-keyed under the fresh id below so the clip builders see fresh keys.
        var offsetByPath = new Dictionary<string, MoveGroupMemberOffsetDto>();
        var phaseByPath = new Dictionary<string, float>();
        var followByPath = new Dictionary<string, string>();
        foreach (var o in group.memberOffsets ?? new MoveGroupMemberOffsetDto[0])
        {
            if (o == null || string.IsNullOrEmpty(o.instanceId)) continue;
            origOffsetDtos[o.instanceId] = o;
            memberOffsets[o.instanceId] = new Vector2(o.x, o.z);
            if (o.t != 0f) memberPhases[o.instanceId] = o.t;
            if (!string.IsNullOrEmpty(o.followWaypointId))
            {
                MoveGroupWaypointDto fw;
                if (wpById.TryGetValue(o.followWaypointId, out fw))
                {
                    memberFollowPos[o.instanceId] = new Vector2(fw.x, fw.z);
                    if (!string.IsNullOrEmpty(o.hierarchyPath))
                        followByPath[o.hierarchyPath] = o.followWaypointId;
                }
                else
                {
                    LayoutEditorLog.LogWarning("move control: member " + o.instanceId +
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
        foreach (var m in group.memberStatic ?? new MoveGroupMemberDto[0])
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
                        MoveGroupWaypointDto fw;
                        if (wpById.TryGetValue(followWpId, out fw))
                            memberFollowPos[id] = new Vector2(fw.x, fw.z);
                    }
                }
                if (!memberOffsets.ContainsKey(id))
                {
                    MoveGroupMemberOffsetDto byPath;
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
                MoveGroupMemberOffsetDto od;
                if (origOffsetDtos.TryGetValue(id2, out od) && !string.IsNullOrEmpty(od.followWaypointId))
                    return od.followWaypointId;
                string p;
                string fw;
                if (memberPaths.TryGetValue(id2, out p) && followByPath.TryGetValue(p, out fw))
                    return fw;
                return null;
            };
            group.memberOffsets = memberOffsets
                .Select(kv => new MoveGroupMemberOffsetDto
                {
                    instanceId = kv.Key,
                    x = kv.Value.x,
                    z = kv.Value.y,
                    followWaypointId = followOf(kv.Key),
                    displayName = memberNames.ContainsKey(kv.Key) ? memberNames[kv.Key] : null,
                    hierarchyPath = memberPaths.ContainsKey(kv.Key) ? memberPaths[kv.Key] : null
                })
                .Concat(memberPhases.Select(kv => new MoveGroupMemberOffsetDto
                {
                    instanceId = kv.Key,
                    t = kv.Value,
                    followWaypointId = followOf(kv.Key),
                    displayName = memberNames.ContainsKey(kv.Key) ? memberNames[kv.Key] : null,
                    hierarchyPath = memberPaths.ContainsKey(kv.Key) ? memberPaths[kv.Key] : null
                }))
                .ToArray();
            group.memberStatic = staticSet
                .Select(id => new MoveGroupMemberDto
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
        float seqLiftY = 0f;
        // Position where the previous move event's route ends. Pure-Y (lift/drop)
        // and wait clips hold XZ there (not at the bake anchor = first waypoint),
        // otherwise the move -> drop hand-off "flashes back" to the route start.
        Vector2? lastRouteEnd = null;
        foreach (var evt in animEvents)
        {
            if (evt.type == "wait")
            {
                // Pure queue delay: a constant-position clip so the event survives
                // the scene round-trip.
                var wclip = BuildWaitClip(key, "s" + clipIndex++, members, groupRoot,
                    finishedTrigger, animDir, usedAssets, memberOffsets, memberFollowPos, lastRouteEnd);
                if (wclip != null) clips[evt.triggerName] = wclip;
                continue;
            }
            var yp = new ClipProfile();
            if (evt.type == "lift" || evt.type == "drop")
            {
                // Pure-Y clip: XZ holds still, members rise/fall to yTo.
                // A lift with yTo == 0 would bake a constant 0->0 curve and come
                // back as a wait event — default the rise height to 1 (the web UI
                // shows the same default).
                yp.pureY = true;
                yp.yTo = evt.type == "lift" && evt.yTo == 0f ? 1f : evt.yTo;
                yp.ySeconds = Mathf.Max(0.05f, evt.liftSeconds > 0f ? evt.liftSeconds : 1f);
                if (evt.type == "drop") yp.dropFrom = seqLiftY;
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
            if (evt.type == "lift") seqLiftY = evt.yTo != 0f ? evt.yTo : 1f;
            else if (evt.type == "drop") seqLiftY = 0f;
            else if (evt.liftHeight > 0f && !evt.loop && !evt.pingpong)
            {
                // Integrated lift: rise at the start, move, lower at the end.
                yp.liftHeight = evt.liftHeight;
                yp.liftSeconds = Mathf.Max(0f, evt.liftSeconds);
                yp.dropSeconds = Mathf.Max(0f, evt.dropSeconds);
            }
            List<MoveGroupWaypointDto> pts = evt.type == "move"
                ? ResolveRoute(evt, wpById)
                : new List<MoveGroupWaypointDto>();
            if (evt.type == "move" && (pts == null || pts.Count == 0))
            {
                Debug.LogWarning("[LayoutEditor] move control: event " + evt.triggerName +
                    " has no resolvable waypoints in group 「" + (group.displayName ?? "?") + "」");
                continue;
            }
            var clip = BuildMoveClip(key, "s" + clipIndex++, members, groupRoot, pts,
                Mathf.Max(0.05f, evt.intervalSeconds > 0f ? evt.intervalSeconds : 2f),
                (evt.loop || evt.pingpong) && evt.type == "move", evt.pingpong && evt.type == "move",
                finishedTrigger, memberOffsets, memberPhases, staticSet, animDir, usedAssets, yp, memberFollowPos);
            if (clip != null) clips[evt.triggerName] = clip;
            // Remember where this route ends so the next lift/drop/wait clip can
            // hold XZ there instead of snapping back to the bake anchor.
            if (evt.type == "move" && clip != null && pts.Count > 0)
                lastRouteEnd = new Vector2(pts[pts.Count - 1].x, pts[pts.Count - 1].z);
        }
        if (clips.Count == 0)
            return "移动组「" + (group.displayName ?? "?") + "」：所有移动事件都没有有效路线";

        var controller = BuildController(controllerPath, key, group, animEvents, clips);
        if (controller == null)
            return "移动组「" + (group.displayName ?? "?") + "」：AnimatorController 创建失败";
        usedAssets.Add(controllerPath);

        AttachComponents(groupRoot, group, queueTriggers, queueDelays, controller);
        return null;
    }

    private static Transform ResolveOrCreateGroupRoot(MoveGroupDto group, Transform animatedRoot)
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

    private static Vector2? FirstWaypoint(MoveGroupEventDto evt, Dictionary<string, MoveGroupWaypointDto> wpById)
    {
        foreach (var wpId in evt.waypointIds ?? new string[0])
        {
            MoveGroupWaypointDto w;
            if (wpId != null && wpById.TryGetValue(wpId, out w))
                return new Vector2(w.x, w.z);
        }
        return null;
    }

    private static List<MoveGroupWaypointDto> ResolveRoute(MoveGroupEventDto evt, Dictionary<string, MoveGroupWaypointDto> wpById)
    {
        var pts = new List<MoveGroupWaypointDto>();
        foreach (var wpId in evt.waypointIds ?? new string[0])
        {
            MoveGroupWaypointDto w;
            if (wpId != null && wpById.TryGetValue(wpId, out w))
                pts.Add(w);
        }
        return pts;
    }

    // ------------------------------------------------------------------- clips

    /// <summary>Vertical motion profile of a clip: either a pure-Y event (lift/drop,
    /// XZ holds still) or an integrated lift on a move clip (rise → move → lower).</summary>
    private class ClipProfile
    {
        public bool pureY;
        public float yTo;
        public float ySeconds;
        /** Pure-Y drop: start height (the group's last lift target) — without this
         *  the drop clip would be constant 0->0 and the fall never animates. */
        public float dropFrom;
        /** Move events following a lift fly at the inherited height: bake a constant
         *  Y curve so the group glides in the air regardless of state transitions. */
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
    private static void BuildTimeline(List<MoveGroupWaypointDto> pts, float interval,
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

    /// <summary>One clip per animation event (move / lift / drop). Each group member
    /// gets position curves: XZ follows the route (or holds still for pure-Y events)
    /// and Y follows the vertical profile (lift/drop), so workstations can rise and
    /// then move. Static members get constant curves at their current position.
    /// Key times come from the waypoints when present (imported levels), otherwise
    /// from the unified timeline (interval / segmentSeconds / waits).</summary>
    private static AnimationClip BuildMoveClip(string key, string suffix,
        List<GameObject> members, Transform groupRoot, List<MoveGroupWaypointDto> pts, float interval,
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
                    // Drop events fall from the last lift height (dropFrom), not the
                    // scene-default height — otherwise the fall never animates.
                    float yFrom = yp.dropFrom != 0f ? yp.dropFrom : curPos.y;
                    cy.AddKey(new Keyframe(0f, yFrom));
                    cy.AddKey(new Keyframe(clipLen, yp.yTo));
                }
                else if (yp.constantY > 0f)
                {
                    // Flying after a lift: hold the inherited height for the whole
                    // move — the group glides in the air instead of dropping to 0.
                    cy.AddKey(new Keyframe(0f, yp.constantY));
                    cy.AddKey(new Keyframe(clipLen, yp.constantY));
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
    /// 1s) so the queue can enter a state and the event survives re-import. When a
    /// move event precedes the wait, members hold the route end (holdPos + offset)
    /// instead of the bake anchor, keeping the hand-off seamless; members pinned to
    /// a waypoint hold their followed position.</summary>
    private static AnimationClip BuildWaitClip(string key, string suffix,
        List<GameObject> members, Transform groupRoot, string finishedTrigger,
        string animDir, HashSet<string> usedAssets,
        Dictionary<string, Vector2> memberOffsets, Dictionary<string, Vector2> memberFollowPos,
        Vector2? holdPos)
    {
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
            SetConstantCurves(clip, path, p.x, p.y, p.z);
        }
        var path2 = animDir + "/" + key + "_" + suffix + ".anim";
        AssetDatabase.CreateAsset(clip, path2);
        if (!string.IsNullOrEmpty(finishedTrigger))
        {
            var animEvent = new AnimationEvent();
            animEvent.functionName = "OnTrigger";
            animEvent.stringParameter = finishedTrigger;
            animEvent.time = 1f;
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

    private static void SetConstantCurves(AnimationClip clip, string path, float x, float y, float z)
    {
        var sx = new AnimationCurve(new Keyframe(0f, x), new Keyframe(1f, x));
        var sy = new AnimationCurve(new Keyframe(0f, y), new Keyframe(1f, y));
        var sz = new AnimationCurve(new Keyframe(0f, z), new Keyframe(1f, z));
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
        MoveGroupDto group, List<MoveGroupEventDto> animEvents,
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
        //  - next animation event with delay <= 0: chain DIRECTLY to it — the queue
        //    fires triggers back-to-back and Animator trigger-competition would skip
        //    states (lift/drop swallowed). Direct chaining plays every clip in order.
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

            // Looping clips (scrolling patterns like 3_4 islands) stay in their
            // state; only the last move event may loop (lift/drop are one-shot).
            // The loop is implemented as a self-transition (replay the state at the
            // end of the clip) — reliable even when clip.loopTime does not persist
            // to disk on the game build (Unity 2017 SetAnimationClipSettings gap).
            bool isLoop = (evt.loop || evt.pingpong) && evt.type == "move" && si == moveStates.Count - 1;
            if (evt.loop && !isLoop)
                Debug.LogWarning("[LayoutEditor] move control: event " + evt.triggerName +
                    " loop only takes effect on the last move event of the group");
            if (isLoop)
            {
                var tSelf = move.AddTransition(move);
                tSelf.hasExitTime = true;
                tSelf.exitTime = 1f;
                tSelf.duration = 0f;
                tSelf.hasFixedDuration = true;
            }
            else if (si + 1 < moveStates.Count &&
                     animEvents[moveEvtIdx[si + 1]].delay <= 0f)
            {
                // Immediate hand-off to the next animation event — plays every clip
                // in sequence without relying on trigger timing.
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

    private static void AttachComponents(Transform groupRoot, MoveGroupDto group,
        List<string> queueTriggers, List<float> queueDelays, AnimatorController controller)
    {
        var go = groupRoot.gameObject;

        // ObjectContainer（IParentable）：实体（玩家/食材的 _Rigidbody 容器）脚下
        // 碰撞体经 DynamicLandscapeParenting 向上递归找挂载点，找到才 SetParent
        // 随组走。组根挂上后覆盖全部成员（成员子 Col_Floor、组内 Col_AirFloor
        // 都在其子树）；纯装饰组无碰撞体，挂着也无作用。幂等添加。
        if (go.GetComponent<ObjectContainer>() == null)
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
        bool externalStart = !string.IsNullOrEmpty(group.startTrigger);
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
                LayoutEditorLog.LogWarning("move control: cleanup orphan TriggerQueue on " +
                    LayoutEditorHierarchy.GetHierarchyPath(q.transform) + " (no Animator)");
                var go = q.gameObject;
                Undo.RecordObject(q, "Move Control Cleanup");
                Undo.DestroyObjectImmediate(q);
                DestroyMoveComponents(go);
            }
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

    internal static string BuildAssetKey(string sceneName, MoveGroupDto group)
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
    private static string DeriveIdPart(MoveGroupDto group)
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
