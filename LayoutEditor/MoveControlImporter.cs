using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Imports animated groups authored directly in the scene (original game levels:
/// Design/Animated Objects counters &amp; islands, Design platforms, ...) into the
/// move-control model so the web editor can display and edit them. Detection rule:
/// a GameObject with TriggerQueue or TriggerTimer AND an Animator whose controller
/// contains clips with Transform position curves, whose controller asset is not one
/// of ours. Re-bake fidelity is guaranteed by only importing groups whose member
/// curves are value-parallel to the reference route (offsets) or constant (static).
/// </summary>
public static class MoveControlImporter
{
    private const float Epsilon = 0.01f;
    /// <summary>Internal sentinel the bakery uses for delayed starts.</summary>
    private const string QueueStartTriggerName = "Start";

    private class ClipData
    {
        public AnimationClip clip;
        public string stateName;
        public string triggerName;
        public bool loop;
        public WrapMode wrapMode;
        public List<string> paths = new List<string>();       // in first-seen order
        public Dictionary<string, AnimationCurve> x = new Dictionary<string, AnimationCurve>();
        public Dictionary<string, AnimationCurve> z = new Dictionary<string, AnimationCurve>();
        public Dictionary<string, AnimationCurve> y = new Dictionary<string, AnimationCurve>();
    }

    public static List<MoveGroupDto> ImportFromScene(Scene scene, string sceneName, string animDir)
    {
        var result = new List<MoveGroupDto>();
        if (!scene.IsValid()) return result;

        int scanned = 0;
        foreach (var root in scene.GetRootGameObjects())
            Collect(root.transform, result, ref scanned);
        LayoutEditorLog.Log("move control: scene import scan (" + sceneName + "): " +
            scanned + " candidate(s), " + result.Count + " group(s) imported");
        return result;
    }

    private static void Collect(Transform t, List<MoveGroupDto> result, ref int scanned)
    {
        for (int i = 0; i < t.childCount; i++)
            Collect(t.GetChild(i), result, ref scanned);

        var go = t.gameObject;
        var queue = go.GetComponent<TriggerQueue>();
        var timer = go.GetComponent<TriggerTimer>();
        if (queue == null && timer == null) return;

        var anim = go.GetComponent<Animator>();
        if (anim == null || anim.runtimeAnimatorController == null) return;
        var ctrlPath = AssetDatabase.GetAssetPath(anim.runtimeAnimatorController);
        if (string.IsNullOrEmpty(ctrlPath)) return;

        scanned++;
        var group = BuildGroup(go, anim, queue, timer);
        if (group == null)
        {
            LayoutEditorLog.Log("move control: skipped " + go.name + " (" +
                LayoutEditorHierarchy.GetHierarchyPath(t) + ") — not representable as a move group");
            return;
        }

        var hierarchyPath = LayoutEditorHierarchy.GetHierarchyPath(t);
        group.id = "scene:" + hierarchyPath;
        group.groupHierarchyPath = hierarchyPath;
        result.Add(group);
        LayoutEditorLog.Log("move control: imported animated group " + go.name +
            " (" + hierarchyPath + ") events:" + group.events.Length +
            " waypoints:" + group.waypoints.Length +
            " items:" + group.itemInstanceIds.Length +
            " floors:" + group.floorInstanceIds.Length +
            " objects:" + group.objectInstanceIds.Length +
            " offsets:" + group.memberOffsets.Length +
            " static:" + group.memberStatic.Length);
    }

    private static MoveGroupDto BuildGroup(GameObject go, Animator anim,
        TriggerQueue queue, TriggerTimer timer)
    {
        var ctrl = anim.runtimeAnimatorController as AnimatorController;
        if (ctrl == null || ctrl.layers.Length == 0)
        {
            LayoutEditorLog.LogWarning("move control: BuildGroup(" + go.name + ") controller is null/empty — " +
                (anim.runtimeAnimatorController != null
                    ? "type=" + anim.runtimeAnimatorController.GetType().Name +
                      " path=" + AssetDatabase.GetAssetPath(anim.runtimeAnimatorController)
                    : "runtimeAnimatorController is null"));
            return null;
        }
        var sm = ctrl.layers[0].stateMachine;
        if (sm == null)
        {
            LayoutEditorLog.LogWarning("move control: BuildGroup(" + go.name + ") state machine is null");
            return null;
        }

        // Group root must sit at the world origin: the model bakes waypoint values
        // directly as local positions (same convention as the original levels).
        var rootPos = go.transform.position;
        if (Mathf.Abs(rootPos.x) > Epsilon || Mathf.Abs(rootPos.z) > Epsilon)
        {
            Debug.LogWarning("[LayoutEditor] move control: skip import of " + go.name +
                " — group root is not at the world origin (" + rootPos + ")");
            return null;
        }

        var states = new List<AnimatorState>();
        CollectStates(sm, states);
        LayoutEditorLog.Log("move control: BuildGroup(" + go.name + ") controller=" + ctrl.name +
            " states=" + states.Count);

        // State -> trigger name (first incoming condition), in state-machine order.
        // Unity 2017 has no public dstState/anyStateTransitions APIs on
        // AnimatorStateTransition/AnimatorState; read them via SerializedObject.
        var triggerOf = new Dictionary<AnimatorState, string>();
        foreach (var st in states)
        {
            foreach (var tr in st.transitions ?? new AnimatorStateTransition[0])
            {
                var dst = GetDstState(tr);
                if (dst == null) continue;
                if (tr.conditions != null && tr.conditions.Length > 0)
                    triggerOf[dst] = tr.conditions[0].parameter;
            }
        }
        foreach (var tr in sm.anyStateTransitions ?? new AnimatorStateTransition[0])
        {
            var dst = GetDstState(tr);
            if (dst == null) continue;
            if (tr.conditions != null && tr.conditions.Length > 0)
                triggerOf[dst] = tr.conditions[0].parameter;
        }

        // Move states: clips with Transform position curves on x/z (y-only clips
        // like lift platforms are not representable in the 2D route model).
        var clipDatas = new List<ClipData>();
        foreach (var st in states)
        {
            var clip = st.motion as AnimationClip;
            if (clip == null)
            {
                LayoutEditorLog.Log("move control: BuildGroup(" + go.name + ") state \"" + st.name +
                    "\" has no clip (motion=" + (st.motion == null ? "null" : st.motion.GetType().Name) + ")");
                continue;
            }
            var bindings = AnimationUtility.GetCurveBindings(clip);
            var data = InspectClip(clip, bindings);
            if (data == null)
            {
                LayoutEditorLog.Log("move control: BuildGroup(" + go.name + ") state \"" + st.name +
                    "\" clip \"" + clip.name + "\" has " + bindings.Length + " binding(s), none usable " +
                    "(" + string.Join(", ", Array.ConvertAll(bindings, b => b.type.Name + "." + b.propertyName + "@" + b.path).ToArray()) + ")");
                continue;
            }
            data.stateName = st.name;
            string trig;
            data.triggerName = triggerOf.TryGetValue(st, out trig) ? trig : st.name;
            // Loop detection: original clips loop via clip.loopTime; web-baked clips
            // loop via a self-transition (replay at the end of the clip).
            data.loop = AnimationUtility.GetAnimationClipSettings(clip).loopTime || HasSelfTransition(st);
            clipDatas.Add(data);
        }
        if (clipDatas.Count == 0)
        {
            LayoutEditorLog.LogWarning("move control: BuildGroup(" + go.name + ") no usable position-curve clip");
            return null;
        }

        // Resolve member objects from clip paths ("" = the group root itself).
        var memberOfPath = new Dictionary<string, GameObject>();
        foreach (var data in clipDatas)
        {
            foreach (var path in data.paths)
            {
                if (memberOfPath.ContainsKey(path)) continue;
                GameObject member;
                if (string.IsNullOrEmpty(path))
                {
                    member = go;
                }
                else
                {
                    var tf = go.transform.Find(path);
                    member = tf != null ? tf.gameObject : null;
                }
                if (member == null)
                {
                    Debug.LogWarning("[LayoutEditor] move control: skip import of " + go.name +
                        " — clip path not found: " + path);
                    return null;
                }
                memberOfPath[path] = member;
            }
        }
        if (memberOfPath.Count == 0)
        {
            LayoutEditorLog.LogWarning("move control: BuildGroup(" + go.name + ") no members resolved from clip paths");
            return null;
        }

        // All clips must animate the same path set (the model drives every member in
        // every event); groups whose events animate different subsets (6_1 Move_Right
        // vs Move_Up) are skipped.
        var refClip = clipDatas[0];
        var refPathSet = new HashSet<string>(refClip.paths);
        foreach (var data in clipDatas)
        {
            if (!refPathSet.SetEquals(data.paths))
            {
                Debug.LogWarning("[LayoutEditor] move control: skip import of " + go.name +
                    " — events animate different object subsets");
                return null;
            }
        }
        // Reference route: prefer the root path, else the first path of the first clip.
        string refPath = null;
        foreach (var p in refClip.paths)
        {
            if (string.IsNullOrEmpty(p)) { refPath = p; break; }
            if (refPath == null) refPath = p;
        }
        if (refPath == null)
        {
            LayoutEditorLog.LogWarning("move control: BuildGroup(" + go.name + ") no reference path in clip");
            return null;
        }

        // Model reference: the first clip animating XZ on the reference path (move
        // model), else the first clip with moving Y only (pure lift/drop model).
        // Constant XZ curves (baked lift/drop clips) do not count as movement.
        bool refHasXZ;
        ClipData modelRef = FindModelRef(clipDatas, refPath, out refHasXZ);
        if (modelRef == null)
        {
            LayoutEditorLog.LogWarning("move control: BuildGroup(" + go.name +
                ") reference path has no moving position curves");
            return null;
        }
        var refMember = memberOfPath[refPath];

        // Build the waypoint pool: one XZ route per clip that has a real route.
        var waypoints = new List<MoveGroupWaypointDto>();
        var routes = new Dictionary<string, List<string>>(); // clip index -> wp ids
        var mirroredClips = new Dictionary<string, bool>();  // clip index -> ping-pong
        var refFirst = Vector2.zero;
        for (int ci = 0; ci < clipDatas.Count; ci++)
        {
            var data = clipDatas[ci];
            if (!HasMovingXZ(data, refPath)) continue;
            var route = new List<MoveGroupWaypointDto>();
            var ids = new List<string>();
            bool mirrored;
            if (!BuildRoute(data, refPath, refMember.transform.localPosition,
                "wp" + waypoints.Count + "_", route, ids, out mirrored))
            {
                LayoutEditorLog.LogWarning("move control: BuildGroup(" + go.name + ") BuildRoute failed for clip \"" +
                    data.clip.name + "\"");
                return null;
            }
            waypoints.AddRange(route);
            routes[data.clip.name + "#" + ci] = ids;
            mirroredClips[data.clip.name + "#" + ci] = mirrored;
            if (refFirst == Vector2.zero && route.Count > 0)
                refFirst = new Vector2(route[0].x, route[0].z);
        }
        if (refHasXZ && waypoints.Count == 0)
        {
            LayoutEditorLog.LogWarning("move control: BuildGroup(" + go.name + ") no XZ route built");
            return null;
        }

        // Members with their offsets / static flags; verify parallelism per model.
        var offsets = new List<MoveGroupMemberOffsetDto>();
        var staticIds = new List<MoveGroupMemberDto>();
        var itemIds = new List<string>();
        var floorIds = new List<string>();
        var objectIds = new List<string>();

        foreach (var path in memberOfPath.Keys.OrderBy(p => string.IsNullOrEmpty(p) ? 0 : 1))
        {
            var member = memberOfPath[path];
            AnimationCurve cx;
            AnimationCurve cz;
            bool hasX = modelRef.x.TryGetValue(path, out cx);
            bool hasZ = modelRef.z.TryGetValue(path, out cz);
            AnimationCurve cy2;
            bool hasY = modelRef.y.TryGetValue(path, out cy2);

            var id = "u:" + member.GetInstanceID();
            if (refHasXZ)
            {
                // XZ model (move groups): constant XZ => static; else parallel/phase.
                bool isStatic = (hasX && IsConstant(cx)) && (hasZ && IsConstant(cz));
                Vector2 first = new Vector2(
                    hasX && cx.length > 0 ? cx[0].value : member.transform.localPosition.x,
                    hasZ && cz.length > 0 ? cz[0].value : member.transform.localPosition.z);
                Vector2 offset = first - refFirst;
                if (isStatic)
                {
                    staticIds.Add(new MoveGroupMemberDto
                    {
                        instanceId = id,
                        displayName = member.name,
                        hierarchyPath = LayoutEditorHierarchy.GetHierarchyPath(member.transform)
                    });
                }
                else
                {
                    var offsetOk = true;
                    foreach (var data in clipDatas)
                    {
                        if (!IsParallelToRoute(data, path, refPath, offset))
                        {
                            offsetOk = false;
                            break;
                        }
                    }
                    if (offsetOk)
                    {
                        offsets.Add(new MoveGroupMemberOffsetDto
                        {
                            instanceId = id,
                            x = offset.x,
                            z = offset.y,
                            displayName = member.name,
                            hierarchyPath = LayoutEditorHierarchy.GetHierarchyPath(member.transform)
                        });
                    }
                    else
                    {
                        float phase;
                        if (modelRef.loop && TryPhaseMatch(modelRef, path, refPath, out phase))
                        {
                            offsets.Add(new MoveGroupMemberOffsetDto
                            {
                                instanceId = id,
                                t = phase,
                                displayName = member.name,
                                hierarchyPath = LayoutEditorHierarchy.GetHierarchyPath(member.transform)
                            });
                        }
                        else
                        {
                            Debug.LogWarning("[LayoutEditor] move control: skip import of " + go.name +
                                " — member " + path + " moves non-parallel to the route and " +
                                "is not a time-shift of it");
                            return null;
                        }
                    }
                }
            }
            else
            {
                // Pure-Y model (lift/drop groups): XZ must hold still, Y must follow
                // the reference rise/fall (parallel, zero offset).
                bool xzConst = (hasX && IsConstant(cx)) && (hasZ && IsConstant(cz));
                if (!xzConst)
                {
                    Debug.LogWarning("[LayoutEditor] move control: skip import of " + go.name +
                        " — member " + path + " moves XZ inside a pure-Y event");
                    return null;
                }
                if (!hasY || !IsYParallel(modelRef, path, refPath))
                {
                    Debug.LogWarning("[LayoutEditor] move control: skip import of " + go.name +
                        " — member " + path + " Y curve is not parallel to the reference lift");
                    return null;
                }
                if (IsConstant(cy2))
                {
                    staticIds.Add(new MoveGroupMemberDto
                    {
                        instanceId = id,
                        displayName = member.name,
                        hierarchyPath = LayoutEditorHierarchy.GetHierarchyPath(member.transform)
                    });
                }
                else
                {
                    offsets.Add(new MoveGroupMemberOffsetDto
                    {
                        instanceId = id,
                        x = 0f,
                        z = 0f,
                        displayName = member.name,
                        hierarchyPath = LayoutEditorHierarchy.GetHierarchyPath(member.transform)
                    });
                }
            }

            if (IsPrefabInstance(member)) itemIds.Add(id);
            else if (LooksLikeFloor(member)) floorIds.Add(id);
            else objectIds.Add(id);
        }

        // Reconstruct user member groups: baked sub-roots appear as nested clip
        // paths ("GroupName/Member"). Only treat the first segment as a group when
        // that transform is a direct child of the animator root and is not itself
        // a member (otherwise it is just a parent member of the original level).
        var memberGroups = new List<MoveGroupMemberGroupDto>();
        var memberSet = new HashSet<string>();
        foreach (var path in memberOfPath.Keys)
        {
            if (string.IsNullOrEmpty(path)) continue;
            memberSet.Add("u:" + memberOfPath[path].GetInstanceID());
        }
        var groupByName = new Dictionary<string, List<string>>();
        foreach (var path in memberOfPath.Keys)
        {
            if (string.IsNullOrEmpty(path)) continue;
            var slash = path.IndexOf('/');
            if (slash <= 0) continue;
            var seg = path.Substring(0, slash);
            var sub = go.transform.Find(seg);
            if (sub == null || sub.parent != go.transform) continue;
            if (memberSet.Contains("u:" + sub.gameObject.GetInstanceID())) continue;
            List<string> ids;
            if (!groupByName.TryGetValue(seg, out ids))
            {
                ids = new List<string>();
                groupByName[seg] = ids;
            }
            ids.Add("u:" + memberOfPath[path].GetInstanceID());
        }
        foreach (var kv in groupByName)
        {
            memberGroups.Add(new MoveGroupMemberGroupDto
            {
                id = "mg:" + kv.Key,
                name = kv.Key,
                memberInstanceIds = kv.Value.ToArray()
            });
        }

        if (offsets.Count == 0 && staticIds.Count == 0)
        {
            LayoutEditorLog.LogWarning("move control: BuildGroup(" + go.name + ") no usable members (all static/empty)");
            return null;
        }

        // Events: one per clip, ordered by the queue's trigger order when present.
        var events = new List<MoveGroupEventDto>();
        var queueOrder = new Dictionary<string, int>();
        if (queue != null && queue.m_queue != null && queue.m_queue.m_triggers != null)
        {
            for (int i = 0; i < queue.m_queue.m_triggers.Length; i++)
                if (!string.IsNullOrEmpty(queue.m_queue.m_triggers[i]) && !queueOrder.ContainsKey(queue.m_queue.m_triggers[i]))
                    queueOrder[queue.m_queue.m_triggers[i]] = i;
        }

        var orderedClips = clipDatas
            .Select((d, idx) => new { d, idx, q = queueOrder.ContainsKey(d.triggerName) ? queueOrder[d.triggerName] : 1000 })
            .OrderBy(p => p.q)
            .ThenBy(p => p.idx)
            .ToList();
        foreach (var item in orderedClips)
        {
            var data = item.d;
            var name = data.triggerName;
            float delay = 0f;
            if (queue != null && queue.m_queue != null && queue.m_queue.m_triggers != null)
            {
                for (int i = 0; i < queue.m_queue.m_triggers.Length; i++)
                {
                    if (queue.m_queue.m_triggers[i] == name && i < queue.m_queue.m_delays.Length)
                    {
                        delay = queue.m_queue.m_delays[i];
                        break;
                    }
                }
            }

            if (HasMovingXZ(data, refPath))
            {
                // XZ clip: a move event (optionally with an integrated lift).
                List<string> ids;
                if (!routes.TryGetValue(data.clip.name + "#" + item.idx, out ids)) continue;
                bool mirrored = false;
                mirroredClips.TryGetValue(data.clip.name + "#" + item.idx, out mirrored);
                var evt = new MoveGroupEventDto
                {
                    id = "ev" + events.Count,
                    type = "move",
                    triggerName = name,
                    delay = Mathf.Max(0f, delay),
                    intervalSeconds = 2f,
                    waypointIds = ids.ToArray(),
                    loop = data.loop && !mirrored,
                    pingpong = data.loop && mirrored
                };
                AnimationCurve cy;
                float h, upS, downS;
                if (TryGetYCurve(data, refPath, out cy) &&
                    ParseLiftProfile(cy, out h, out upS, out downS) && h > Epsilon)
                {
                    evt.liftHeight = h;
                    evt.liftSeconds = upS;
                    evt.dropSeconds = downS;
                }
                events.Add(evt);
            }
            else if (HasMovingY(data, refPath))
            {
                // Pure-Y clip: lift (rises) or drop (falls).
                AnimationCurve cy;
                if (!TryGetYCurve(data, refPath, out cy) || cy.length < 2) continue;
                float start = cy[0].value;
                float end = cy[cy.length - 1].value;
                if (Mathf.Abs(end - start) <= Epsilon) continue;
                events.Add(new MoveGroupEventDto
                {
                    id = "ev" + events.Count,
                    type = end > start ? "lift" : "drop",
                    triggerName = name,
                    delay = Mathf.Max(0f, delay),
                    yTo = end,
                    liftSeconds = Mathf.Max(0.05f, cy[cy.length - 1].time - cy[0].time)
                });
            }
            else
            {
                // No movement at all (XZ and Y constant): a wait event baked as a
                // constant clip so the queue sequence round-trips intact.
                events.Add(new MoveGroupEventDto
                {
                    id = "ev" + events.Count,
                    type = "wait",
                    triggerName = name,
                    delay = Mathf.Max(0f, delay)
                });
            }
        }
        if (events.Count == 0)
        {
            LayoutEditorLog.LogWarning("move control: BuildGroup(" + go.name + ") no events built from clips");
            return null;
        }

        // Average interval for the UI (key times are preserved per waypoint).
        foreach (var evt in events)
        {
            if (evt.waypointIds == null || evt.waypointIds.Length < 2) continue;
            var first = evt.waypointIds[0];
            var last = evt.waypointIds[evt.waypointIds.Length - 1];
            MoveGroupWaypointDto f = null, l = null;
            foreach (var wp in waypoints)
            {
                if (wp.id == first) f = wp;
                if (wp.id == last) l = wp;
            }
            if (f != null && l != null && f.hasTime && l.hasTime && l.t - f.t > 0.001f)
                evt.intervalSeconds = (l.t - f.t) / (evt.waypointIds.Length - 1);
        }

        var group = new MoveGroupDto
        {
            displayName = go.name,
            itemInstanceIds = itemIds.ToArray(),
            floorInstanceIds = floorIds.ToArray(),
            objectInstanceIds = objectIds.ToArray(),
            memberOffsets = offsets.ToArray(),
            memberStatic = staticIds.ToArray(),
            memberGroups = memberGroups.ToArray(),
            startDelay = timer != null && timer.m_startTiming ? Mathf.Max(0f, timer.m_time) : 0f,
            loop = queue != null && queue.m_loopWhenFinished,
            loopDelay = queue != null ? Mathf.Max(0f, queue.m_loopDelay) : 0f,
            waitForFinished = queue != null && queue.m_waitForFinished,
            // Internal delayed-start convention (bakery fires "Start"): surface a
            // custom trigger name only when it is not our own sentinel.
            startTrigger = queue != null && !string.IsNullOrEmpty(queue.m_startTrigger)
                && queue.m_startTrigger != QueueStartTriggerName
                ? queue.m_startTrigger : null,
            cancelTrigger = queue != null && !string.IsNullOrEmpty(queue.m_cancelTrigger)
                ? queue.m_cancelTrigger : null,
            endTrigger = queue != null && !string.IsNullOrEmpty(queue.m_endTrigger)
                ? queue.m_endTrigger : null,
            finishedTrigger = queue != null && !string.IsNullOrEmpty(queue.m_finishedTrigger)
                && queue.m_finishedTrigger != "AnimationFinished"
                ? queue.m_finishedTrigger : null,
            applyRootMotion = anim.applyRootMotion,
            waypoints = waypoints.ToArray(),
            events = events.ToArray()
        };
        return group;
    }

    // ------------------------------------------------------------------- helpers

    /// <summary>Unity 2017 does not expose AnimatorStateTransition.dstState; read the
    /// serialized m_DstState reference instead.</summary>
    private static AnimatorState GetDstState(AnimatorStateTransition tr)
    {
        if (tr == null) return null;
        var so = new SerializedObject(tr);
        var prop = so.FindProperty("m_DstState");
        if (prop == null) return null;
        return prop.objectReferenceValue as AnimatorState;
    }

    /// <summary>Web-baked loop states carry a transition back to themselves
    /// (replay at clip end) instead of relying on clip.loopTime.</summary>
    private static bool HasSelfTransition(AnimatorState st)
    {
        foreach (var tr in st.transitions ?? new AnimatorStateTransition[0])
        {
            if (GetDstState(tr) == st) return true;
        }
        return false;
    }

    private static void CollectStates(AnimatorStateMachine sm, List<AnimatorState> states)
    {
        foreach (var st in sm.states ?? new ChildAnimatorState[0])
        {
            if (st.state != null) states.Add(st.state);
        }
        foreach (var sub in sm.stateMachines ?? new ChildAnimatorStateMachine[0])
        {
            if (sub.stateMachine != null) CollectStates(sub.stateMachine, states);
        }
    }

    /// <summary>True when the reference path has a non-constant XZ curve (a real
    /// route). Baked lift/drop clips carry constant XZ keys, so they must not be
    /// mistaken for move events.</summary>
    private static bool HasMovingXZ(ClipData data, string refPath)
    {
        AnimationCurve c;
        if (data.x.TryGetValue(refPath, out c) && !IsConstant(c)) return true;
        if (data.z.TryGetValue(refPath, out c) && !IsConstant(c)) return true;
        return false;
    }

    /// <summary>Y curve on the reference path, falling back to any path that has
    /// one — the reference member may be static (baked clips skip Y for static
    /// members) while other members carry the lift curve.</summary>
    private static bool TryGetYCurve(ClipData data, string refPath, out AnimationCurve cy)
    {
        if (data.y.TryGetValue(refPath, out cy)) return true;
        foreach (var kv in data.y)
        {
            cy = kv.Value;
            return true;
        }
        cy = null;
        return false;
    }

    private static bool HasMovingY(ClipData data, string refPath)
    {
        AnimationCurve c;
        return TryGetYCurve(data, refPath, out c) && !IsConstant(c);
    }

    private static ClipData FindModelRef(List<ClipData> clipDatas, string refPath, out bool hasXZ)
    {
        hasXZ = false;
        foreach (var data in clipDatas)
        {
            if (HasMovingXZ(data, refPath))
            {
                hasXZ = true;
                return data;
            }
        }
        foreach (var data in clipDatas)
        {
            if (HasMovingY(data, refPath)) return data;
        }
        return null;
    }

    private static ClipData InspectClip(AnimationClip clip, EditorCurveBinding[] bindings)
    {
        var data = new ClipData { clip = clip, wrapMode = clip.wrapMode };
        foreach (var b in bindings)
        {
            if (b.type != typeof(Transform)) continue;
            if (b.propertyName == "m_LocalPosition.x")
            {
                var c = AnimationUtility.GetEditorCurve(clip, b);
                if (c == null || c.length == 0) continue;
                data.x[b.path] = c;
                if (!data.paths.Contains(b.path)) data.paths.Add(b.path);
            }
            else if (b.propertyName == "m_LocalPosition.z")
            {
                var c = AnimationUtility.GetEditorCurve(clip, b);
                if (c == null || c.length == 0) continue;
                data.z[b.path] = c;
                if (!data.paths.Contains(b.path)) data.paths.Add(b.path);
            }
            else if (b.propertyName == "m_LocalPosition.y")
            {
                var c = AnimationUtility.GetEditorCurve(clip, b);
                if (c == null || c.length == 0) continue;
                data.y[b.path] = c;
                if (!data.paths.Contains(b.path)) data.paths.Add(b.path);
            }
        }
        if (data.paths.Count == 0) return null;
        return data;
    }

    private static bool BuildRoute(ClipData data, string refPath, Vector3 refLocal,
        string idPrefix, List<MoveGroupWaypointDto> waypoints, List<string> wpIds, out bool mirrored)
    {
        mirrored = false;
        AnimationCurve cx;
        AnimationCurve cz;
        bool hasX = data.x.TryGetValue(refPath, out cx);
        bool hasZ = data.z.TryGetValue(refPath, out cz);
        if (!hasX && !hasZ) return false;

        var n = hasX ? cx.length : cz.length;
        if (n == 0) return false;

        // Ping-pong detection: a baked round-trip route is mirror-symmetric around
        // its middle (key i mirrors key n-1-i in both time and value). Keep only
        // the outbound half as the route and mark the event as ping-pong.
        if (n >= 4)
        {
            AnimationCurve rc = hasX ? cx : cz;
            float sum = rc[0].time + rc[n - 1].time;
            bool sym = true;
            for (int i = 0; i < n; i++)
            {
                if (Mathf.Abs(rc[i].time + rc[n - 1 - i].time - sum) > 0.01f)
                {
                    sym = false;
                    break;
                }
                float vA = hasX ? cx[i].value : cz[i].value;
                float vB = hasX ? cx[n - 1 - i].value : cz[n - 1 - i].value;
                if (Mathf.Abs(vA - vB) > 0.01f)
                {
                    sym = false;
                    break;
                }
            }
            if (sym)
            {
                mirrored = true;
                n = (n + 1) / 2;
            }
        }

        // Consecutive keyframes with the same position are dwell (hold) segments —
        // fold them into the previous waypoint's wait instead of duplicating nodes.
        MoveGroupWaypointDto prev = null;
        float prevTime = 0f;
        for (int i = 0; i < n; i++)
        {
            var time = hasX ? cx[i].time : cz[i].time;
            var x = hasX ? cx[i].value : refLocal.x;
            var z = hasZ ? cz[i].value : refLocal.z;
            if (prev != null && Mathf.Abs(prev.x - x) < 0.001f && Mathf.Abs(prev.z - z) < 0.001f)
            {
                prev.wait += time - prevTime;
                continue;
            }
            var wp = new MoveGroupWaypointDto
            {
                id = idPrefix + i,
                x = x,
                z = z,
                hasTime = true,
                t = time
            };
            waypoints.Add(wp);
            wpIds.Add(wp.id);
            prev = wp;
            prevTime = time;
        }
        return true;
    }

    private static bool IsConstant(AnimationCurve c)
    {
        if (c == null || c.length == 0) return true;
        for (int i = 1; i < c.length; i++)
            if (Mathf.Abs(c[i].value - c[0].value) > Epsilon) return false;
        return true;
    }

    /// <summary>Member's Y curve is the reference Y curve (same keys, values parallel
    /// with zero offset) — all members of a lift/drop group rise/fall together.</summary>
    private static bool IsYParallel(ClipData clip, string path, string refPath)
    {
        AnimationCurve cy;
        AnimationCurve refCy;
        if (!clip.y.TryGetValue(path, out cy)) return false;
        if (!TryGetYCurve(clip, refPath, out refCy)) return false;
        if (cy.length != refCy.length) return false;
        for (int i = 0; i < cy.length; i++)
        {
            if (Mathf.Abs(cy[i].value - refCy[i].value) > Epsilon) return false;
            if (Mathf.Abs(cy[i].time - refCy[i].time) > Epsilon) return false;
        }
        return true;
    }

    /// <summary>Extract an integrated lift profile from a Y curve: rise to a maximum
    /// plateau, optionally lower back. Returns false when the curve is flat or
    /// falling-first (no lift).</summary>
    private static bool ParseLiftProfile(AnimationCurve cy, out float height, out float upSec, out float downSec)
    {
        height = 0f;
        upSec = 0f;
        downSec = 0f;
        if (cy == null || cy.length < 2) return false;
        float y0 = cy[0].value;
        float maxV = float.MinValue;
        for (int i = 0; i < cy.length; i++) maxV = Mathf.Max(maxV, cy[i].value);
        float h = maxV - y0;
        if (h <= Epsilon) return false;
        int firstMax = -1;
        int lastMax = -1;
        for (int i = 0; i < cy.length; i++)
        {
            if (Mathf.Abs(cy[i].value - maxV) <= Epsilon)
            {
                if (firstMax < 0) firstMax = i;
                lastMax = i;
            }
        }
        if (firstMax < 0) return false;
        upSec = Mathf.Max(0f, cy[firstMax].time - cy[0].time);
        downSec = Mathf.Max(0f, cy[cy.length - 1].time - cy[lastMax].time);
        height = h;
        return true;
    }

    private static bool IsParallelToRoute(ClipData clip, string path, string refPath, Vector2 offset)
    {
        AnimationCurve cx;
        AnimationCurve cz;
        bool hasX = clip.x.TryGetValue(path, out cx);
        bool hasZ = clip.z.TryGetValue(path, out cz);
        AnimationCurve refCx;
        AnimationCurve refCz;
        bool hasRefX = clip.x.TryGetValue(refPath, out refCx);
        bool hasRefZ = clip.z.TryGetValue(refPath, out refCz);

        // The member holds still in this event (e.g. a lift/drop clip where every
        // member keeps its XZ) — compatible with any route.
        if ((hasX && IsConstant(cx)) && (hasZ && IsConstant(cz))) return true;

        if (hasX)
        {
            if (!hasRefX || cx.length != refCx.length) return false;
            for (int i = 0; i < cx.length; i++)
            {
                var dRef = refCx[i].value - refCx[0].value;
                var dMem = cx[i].value - cx[0].value;
                if (Mathf.Abs(dMem - dRef) > Epsilon) return false;
            }
            // Consistent first-key offset against the reference clip.
            if (Mathf.Abs((cx[0].value - refCx[0].value) - offset.x) > Epsilon) return false;
        }
        if (hasZ)
        {
            if (!hasRefZ || cz.length != refCz.length) return false;
            for (int i = 0; i < cz.length; i++)
            {
                var dRef = refCz[i].value - refCz[0].value;
                var dMem = cz[i].value - cz[0].value;
                if (Mathf.Abs(dMem - dRef) > Epsilon) return false;
            }
            if (Mathf.Abs((cz[0].value - refCz[0].value) - offset.y) > Epsilon) return false;
        }
        return true;
    }

    /// <summary>Phase-shift model for looping scroll patterns: the member curve is the
    /// route cycle shifted in time, member(t) = route((t + phase) mod L). Verifies by
    /// evaluating the member curve at every route key time shifted by the candidate.</summary>
    private static bool TryPhaseMatch(ClipData clip, string path, string refPath, out float phase)
    {
        phase = 0f;
        AnimationCurve cx;
        AnimationCurve cz;
        bool hasX = clip.x.TryGetValue(path, out cx);
        bool hasZ = clip.z.TryGetValue(path, out cz);
        AnimationCurve refCx;
        AnimationCurve refCz;
        bool hasRefX = clip.x.TryGetValue(refPath, out refCx);
        bool hasRefZ = clip.z.TryGetValue(refPath, out refCz);

        // The reference cycle must be closed (last key value == first) for the shift
        // to be well-defined; the cycle length is the last key time.
        if (hasRefZ && refCz.length > 1 && Mathf.Abs(refCz[refCz.length - 1].value - refCz[0].value) > Epsilon)
            return false;
        if (hasRefX && refCx.length > 1 && Mathf.Abs(refCx[refCx.length - 1].value - refCx[0].value) > Epsilon)
            return false;

        float cycleLen = 0f;
        if (hasRefZ && refCz.length > 1) cycleLen = refCz[refCz.length - 1].time;
        else if (hasRefX && refCx.length > 1) cycleLen = refCx[refCx.length - 1].time;
        if (cycleLen <= 0.001f) return false;

        // Candidate phases: route key times whose value matches the member's first
        // key value. member(t) = route((t - φ) mod L) so φ = (firstTime - t*) mod L.
        var firstTime = hasZ && cz.length > 0 ? cz[0].time : (hasX && cx.length > 0 ? cx[0].time : 0f);
        var firstValue = hasZ && cz.length > 0 ? cz[0].value : (hasX && cx.length > 0 ? cx[0].value : 0f);
        var candidates = new List<float>();
        if (hasRefZ)
        {
            for (int i = 0; i < refCz.length; i++)
                if (Mathf.Abs(refCz[i].value - firstValue) <= Epsilon)
                    candidates.Add(WrapTime(firstTime - refCz[i].time, cycleLen));
        }
        else if (hasRefX)
        {
            for (int i = 0; i < refCx.length; i++)
                if (Mathf.Abs(refCx[i].value - firstValue) <= Epsilon)
                    candidates.Add(WrapTime(firstTime - refCx[i].time, cycleLen));
        }

        foreach (var cand in candidates)
        {
            if (VerifyPhaseShift(cz, refCz, cand, cycleLen) &&
                VerifyPhaseShift(cx, refCx, cand, cycleLen))
            {
                phase = cand;
                return true;
            }
        }
        return false;
    }

    private static bool VerifyPhaseShift(AnimationCurve member, AnimationCurve route, float phase, float cycleLen)
    {
        if (member == null || route == null || route.length == 0) return true;
        if (member.length == 0) return true;
        for (int i = 0; i < route.length; i++)
        {
            var t = WrapTime(route[i].time + phase, cycleLen);
            if (Mathf.Abs(member.Evaluate(t) - route[i].value) > Epsilon)
                return false;
        }
        return true;
    }

    private static float WrapTime(float t, float cycleLen)
    {
        t = t % cycleLen;
        if (t < 0f) t += cycleLen;
        return t;
    }

    private static bool IsPrefabInstance(GameObject go)
    {
        var type = PrefabUtility.GetPrefabType(go);
        return type == PrefabType.PrefabInstance || type == PrefabType.DisconnectedPrefabInstance;
    }

    private static bool LooksLikeFloor(GameObject go)
    {
        var mf = go.GetComponent<MeshFilter>();
        var mr = go.GetComponent<MeshRenderer>();
        if (mf == null || mr == null) return false;
        var mesh = mf.sharedMesh;
        if (mesh == null) return false;
        return mesh.name == "Plane" || mesh.name == "Quad";
    }
}
