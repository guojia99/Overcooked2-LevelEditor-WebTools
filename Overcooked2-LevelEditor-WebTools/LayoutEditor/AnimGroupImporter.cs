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
/// move-control model so the web editor can display and edit them. Two paths:
/// 1) groups baked by this toolchain carry an embedded AnimGroupSource TextAsset
///    sub-asset in their .controller (the original web-authored model) — imported
///    back losslessly (TryImportFromSource); 2) legacy/original groups fall back
///    to clip analysis (BuildGroup): a GameObject with TriggerQueue or TriggerTimer
///    AND an Animator whose controller contains clips with Transform position
///    curves. Clip-analysis re-bake fidelity is guaranteed by only importing
///    groups whose member curves are shape-parallel to the reference route
///    (offsets, constant Y offsets allowed) or constant (static).
/// </summary>
public static class AnimGroupImporter
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
        // Rotation curves (rotate events bake m_LocalRotation on each member).
        public Dictionary<string, AnimationCurve> rx = new Dictionary<string, AnimationCurve>();
        public Dictionary<string, AnimationCurve> ry = new Dictionary<string, AnimationCurve>();
        public Dictionary<string, AnimationCurve> rz = new Dictionary<string, AnimationCurve>();
        public Dictionary<string, AnimationCurve> rw = new Dictionary<string, AnimationCurve>();
    }

    public static List<AnimGroupDto> ImportFromScene(Scene scene, string sceneName, string animDir)
    {
        var result = new List<AnimGroupDto>();
        if (!scene.IsValid()) return result;

        int scanned = 0;
        foreach (var root in scene.GetRootGameObjects())
            Collect(root.transform, result, ref scanned);
        LayoutEditorLog.Log("anim group: scene import scan (" + sceneName + "): " +
            scanned + " candidate(s), " + result.Count + " group(s) imported");
        return result;
    }

    private static void Collect(Transform t, List<AnimGroupDto> result, ref int scanned)
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
        // 优先读烘焙时嵌入 controller 的原始编排数据（AnimGroupSource 子资产）：
        // 片段分析是有损重建（并行簇合并、镜像路线、触发器丢失），源数据零退化。
        var group = TryImportFromSource(go, anim, ctrlPath);
        if (group == null)
            group = BuildGroup(go, anim, queue, timer);
        if (group == null)
        {
            LayoutEditorLog.Log("anim group: skipped " + go.name + " (" +
                LayoutEditorHierarchy.GetHierarchyPath(t) + ") — not representable as a move group");
            return;
        }

        var hierarchyPath = LayoutEditorHierarchy.GetHierarchyPath(t);
        // 源数据带来 web 编排期的稳定 id（uuid）；片段分析的导入组用场景路径 id。
        if (string.IsNullOrEmpty(group.id)) group.id = "scene:" + hierarchyPath;
        group.groupHierarchyPath = hierarchyPath;
        result.Add(group);
        LayoutEditorLog.Log("anim group: imported animated group " + go.name +
            " (" + hierarchyPath + ") events:" + group.events.Length +
            " waypoints:" + group.waypoints.Length +
            " items:" + group.itemInstanceIds.Length +
            " floors:" + group.floorInstanceIds.Length +
            " objects:" + group.objectInstanceIds.Length +
            " offsets:" + group.memberOffsets.Length +
            " static:" + group.memberStatic.Length);
    }

    private static AnimGroupDto BuildGroup(GameObject go, Animator anim,
        TriggerQueue queue, TriggerTimer timer)
    {
        var ctrl = anim.runtimeAnimatorController as AnimatorController;
        if (ctrl == null || ctrl.layers.Length == 0)
        {
            LayoutEditorLog.LogWarning("anim group: BuildGroup(" + go.name + ") controller is null/empty — " +
                (anim.runtimeAnimatorController != null
                    ? "type=" + anim.runtimeAnimatorController.GetType().Name +
                      " path=" + AssetDatabase.GetAssetPath(anim.runtimeAnimatorController)
                    : "runtimeAnimatorController is null"));
            return null;
        }
        var sm = ctrl.layers[0].stateMachine;
        if (sm == null)
        {
            LayoutEditorLog.LogWarning("anim group: BuildGroup(" + go.name + ") state machine is null");
            return null;
        }

        // Group root must sit at the world origin: the model bakes waypoint values
        // directly as local positions (same convention as the original levels).
        var rootPos = go.transform.position;
        if (Mathf.Abs(rootPos.x) > Epsilon || Mathf.Abs(rootPos.z) > Epsilon)
        {
            Debug.LogWarning("[LayoutEditor] anim group: skip import of " + go.name +
                " — group root is not at the world origin (" + rootPos + ")");
            return null;
        }

        var states = new List<AnimatorState>();
        CollectStates(sm, states);
        LayoutEditorLog.Log("anim group: BuildGroup(" + go.name + ") controller=" + ctrl.name +
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
                LayoutEditorLog.Log("anim group: BuildGroup(" + go.name + ") state \"" + st.name +
                    "\" has no clip (motion=" + (st.motion == null ? "null" : st.motion.GetType().Name) + ")");
                continue;
            }
            var bindings = AnimationUtility.GetCurveBindings(clip);
            var data = InspectClip(clip, bindings);
            if (data == null)
            {
                LayoutEditorLog.Log("anim group: BuildGroup(" + go.name + ") state \"" + st.name +
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
            LayoutEditorLog.LogWarning("anim group: BuildGroup(" + go.name + ") no usable position-curve clip");
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
                    Debug.LogWarning("[LayoutEditor] anim group: skip import of " + go.name +
                        " — clip path not found: " + path);
                    return null;
                }
                memberOfPath[path] = member;
            }
        }
        if (memberOfPath.Count == 0)
        {
            LayoutEditorLog.LogWarning("anim group: BuildGroup(" + go.name + ") no members resolved from clip paths");
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
                Debug.LogWarning("[LayoutEditor] anim group: skip import of " + go.name +
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
            LayoutEditorLog.LogWarning("anim group: BuildGroup(" + go.name + ") no reference path in clip");
            return null;
        }

        // Model reference: the first clip animating XZ on the reference path (move
        // model), else the first clip with moving Y only (pure lift/drop model).
        // Constant XZ curves (baked lift/drop clips) do not count as movement.
        bool refHasXZ;
        ClipData modelRef = FindModelRef(clipDatas, refPath, out refHasXZ);
        if (modelRef == null)
        {
            LayoutEditorLog.LogWarning("anim group: BuildGroup(" + go.name +
                ") reference path has no moving position curves");
            return null;
        }
        var refMember = memberOfPath[refPath];

        // Build the waypoint pool: one XZ route per clip that has a real route.
        var waypoints = new List<AnimGroupWaypointDto>();
        var routes = new Dictionary<string, List<string>>(); // clip index -> wp ids
        var mirroredClips = new Dictionary<string, bool>();  // clip index -> ping-pong
        var refFirst = Vector2.zero;
        for (int ci = 0; ci < clipDatas.Count; ci++)
        {
            var data = clipDatas[ci];
            if (!HasMovingXZ(data, refPath)) continue;
            var route = new List<AnimGroupWaypointDto>();
            var ids = new List<string>();
            bool mirrored;
            if (!BuildRoute(data, refPath, refMember.transform.localPosition,
                "wp" + waypoints.Count + "_", route, ids, out mirrored))
            {
                LayoutEditorLog.LogWarning("anim group: BuildGroup(" + go.name + ") BuildRoute failed for clip \"" +
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
            LayoutEditorLog.LogWarning("anim group: BuildGroup(" + go.name + ") no XZ route built");
            return null;
        }

        // Members with their offsets / static flags; verify parallelism per model.
        var offsets = new List<AnimGroupMemberOffsetDto>();
        var staticIds = new List<AnimGroupMemberDto>();
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
                    staticIds.Add(new AnimGroupMemberDto
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
                        offsets.Add(new AnimGroupMemberOffsetDto
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
                            offsets.Add(new AnimGroupMemberOffsetDto
                            {
                                instanceId = id,
                                t = phase,
                                displayName = member.name,
                                hierarchyPath = LayoutEditorHierarchy.GetHierarchyPath(member.transform)
                            });
                        }
                        else
                        {
                            Debug.LogWarning("[LayoutEditor] anim group: skip import of " + go.name +
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
                // the reference rise/fall shape (允许恒定值偏移 —— 相对 Δy 烘焙)。
                bool xzConst = (hasX && IsConstant(cx)) && (hasZ && IsConstant(cz));
                if (!xzConst)
                {
                    Debug.LogWarning("[LayoutEditor] anim group: skip import of " + go.name +
                        " — member " + path + " moves XZ inside a pure-Y event");
                    return null;
                }
                bool yParallel = hasY && IsYParallel(modelRef, path, refPath);
                if (!yParallel || IsConstant(cy2))
                {
                    // Y 曲线与基准不平行（旧绝对高度烘焙的混合高度组等）：降级为
                    // 静止成员保住整组，不再整组丢弃（旧行为：BuildGroup 返回 null，
                    // 写回重载后整个动画组从 web 消失）。
                    if (hasY && !yParallel)
                        LayoutEditorLog.LogWarning("anim group: " + go.name + " member " + path +
                            " Y 曲线与基准升降不平行 —— 按静止成员导入（整组保留）");
                    staticIds.Add(new AnimGroupMemberDto
                    {
                        instanceId = id,
                        displayName = member.name,
                        hierarchyPath = LayoutEditorHierarchy.GetHierarchyPath(member.transform)
                    });
                }
                else
                {
                    offsets.Add(new AnimGroupMemberOffsetDto
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
            else if (LooksLikeFloor(member))
            {
                var colGo = AirFloorRig.GetColliderObject(member);
                floorIds.Add("u:" + (colGo != null ? colGo.GetInstanceID() : member.GetInstanceID()));
            }
            else objectIds.Add(id);
        }

        // Reconstruct user member groups: baked sub-roots appear as nested clip
        // paths ("GroupName/Member"). Only treat the first segment as a group when
        // that transform is a direct child of the animator root and is not itself
        // a member (otherwise it is just a parent member of the original level).
        var memberGroups = new List<AnimGroupMemberGroupDto>();
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
            memberGroups.Add(new AnimGroupMemberGroupDto
            {
                id = "mg:" + kv.Key,
                name = kv.Key,
                memberInstanceIds = kv.Value.ToArray()
            });
        }

        if (offsets.Count == 0 && staticIds.Count == 0)
        {
            LayoutEditorLog.LogWarning("anim group: BuildGroup(" + go.name + ") no usable members (all static/empty)");
            return null;
        }

        // Events: one per clip, ordered by the queue's trigger order when present.
        var events = new List<AnimGroupEventDto>();
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
        // 时间轴 startTime：TriggerQueue delays[i] 相对上一触发 firing 时刻，
        // 累计即得绝对开始时间（与烘焙端 ΔstartTime 对应）。
        float cumStart = 0f;
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
            cumStart += Mathf.Max(0f, delay);
            float startTime = SnapImport(cumStart);

            // 旋转检测：localRotation 曲线变化 → rotate 事件（可与移动/升降同簇
            // 并行 —— 一个组合 clip 拆回多个同 startTime 事件）。
            AnimGroupEventDto rotEvt = null;
            float rotDeg, rotSec;
            bool rotCcw;
            if (TryExtractRotation(data, refPath, out rotDeg, out rotSec, out rotCcw))
            {
                rotEvt = new AnimGroupEventDto
                {
                    id = "ev" + events.Count + 900,
                    type = "rotate",
                    triggerName = name,
                    delay = Mathf.Max(0f, delay),
                    startTime = startTime,
                    rotateDegrees = rotDeg,
                    rotateDirection = rotCcw ? "ccw" : "cw",
                    rotateSeconds = rotSec,
                    loop = data.loop && !HasMovingXZ(data, refPath)
                };
            }

            if (HasMovingXZ(data, refPath))
            {
                // XZ clip: a move event (optionally with an integrated lift).
                List<string> ids;
                if (!routes.TryGetValue(data.clip.name + "#" + item.idx, out ids)) continue;
                bool mirrored = false;
                mirroredClips.TryGetValue(data.clip.name + "#" + item.idx, out mirrored);
                var evt = new AnimGroupEventDto
                {
                    id = "ev" + events.Count,
                    type = "move",
                    triggerName = name,
                    delay = Mathf.Max(0f, delay),
                    startTime = startTime,
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
                if (rotEvt != null) events.Add(rotEvt);
            }
            else if (HasMovingY(data, refPath))
            {
                // Pure-Y clip: lift (rises) or drop (falls). yTo 为相对位移量
                // Δy（与烘焙端新语义一致），不再是绝对目标高度。
                AnimationCurve cy;
                if (!TryGetYCurve(data, refPath, out cy) || cy.length < 2) continue;
                float start = cy[0].value;
                float end = cy[cy.length - 1].value;
                if (Mathf.Abs(end - start) <= Epsilon) continue;
                events.Add(new AnimGroupEventDto
                {
                    id = "ev" + events.Count,
                    type = end > start ? "lift" : "drop",
                    triggerName = name,
                    delay = Mathf.Max(0f, delay),
                    startTime = startTime,
                    yTo = Mathf.Abs(end - start),
                    liftSeconds = Mathf.Max(0.05f, cy[cy.length - 1].time - cy[0].time)
                });
                if (rotEvt != null) events.Add(rotEvt);
            }
            else if (rotEvt != null)
            {
                // Rotation-only clip: a rotate event.
                events.Add(rotEvt);
            }
            else
            {
                // No movement at all (XZ/Y/rotation constant): a wait event baked as
                // a constant clip so the queue sequence round-trips intact.
                events.Add(new AnimGroupEventDto
                {
                    id = "ev" + events.Count,
                    type = "wait",
                    triggerName = name,
                    delay = Mathf.Max(0f, delay),
                    startTime = startTime,
                    duration = Mathf.Max(0.1f, SnapImport(data.clip.length))
                });
            }
        }
        if (events.Count == 0)
        {
            LayoutEditorLog.LogWarning("anim group: BuildGroup(" + go.name + ") no events built from clips");
            return null;
        }

        // Average interval for the UI, computed from PURE segment times: the bake
        // lays keys as arrival t[i+1] = t[i] + wait[i] + segment[i], so each
        // waypoint's dwell must be subtracted. The old (l.t - f.t)/(n-1) folded
        // waits into travel time — a 12s step with a 30s dwell came back as 27s,
        // and every save/reload round-trip inflated the interval by wait/(n-1)
        // more (12 -> 27 -> 42 ...).
        foreach (var evt in events)
        {
            if (evt.waypointIds == null || evt.waypointIds.Length < 2) continue;
            var wps = new List<AnimGroupWaypointDto>();
            bool missing = false;
            for (int i = 0; i < evt.waypointIds.Length; i++)
            {
                AnimGroupWaypointDto found = null;
                foreach (var wp in waypoints)
                {
                    if (wp.id == evt.waypointIds[i])
                    {
                        found = wp;
                        break;
                    }
                }
                if (found == null)
                {
                    missing = true;
                    break;
                }
                wps.Add(found);
            }
            if (missing) continue;
            float sum = 0f;
            int count = 0;
            for (int i = 0; i < wps.Count - 1; i++)
            {
                if (!wps[i].hasTime || !wps[i + 1].hasTime) continue;
                float seg = wps[i + 1].t - wps[i].t - Mathf.Max(0f, wps[i].wait);
                if (seg <= 0.001f) continue;
                sum += seg;
                count++;
            }
            if (count > 0)
                evt.intervalSeconds = sum / count;
        }

        var group = new AnimGroupDto
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

    // ------------------------------------------------------- source re-import

    /// <summary>回读优先路径：烘焙时嵌入 .controller 的 AnimGroupSource 子资产
    /// 保存了 web 编排的原始 AnimGroupDto（事件/路点/触发器/循环标记无损）。
    /// 成员按 hierarchyPath（跨会话稳定）或存活 u: id 重解析为当前会话 id；
    /// 成员数组（items/floors/objects）与成员组按场景现状重建。无子资产或数据
    /// 无效时返回 null，走片段分析降级路径（BuildGroup）。</summary>
    private static AnimGroupDto TryImportFromSource(GameObject go, Animator anim, string ctrlPath)
    {
        TextAsset src = null;
        foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(ctrlPath))
        {
            var ta = sub as TextAsset;
            if (ta != null && ta.name == AnimGroupBakery.SourceAssetName)
            {
                src = ta;
                break;
            }
        }
        if (src == null || string.IsNullOrEmpty(src.text)) return null;

        AnimGroupDto g = null;
        try
        {
            g = JsonUtility.FromJson<AnimGroupDto>(src.text);
        }
        catch (Exception e)
        {
            LayoutEditorLog.LogWarning("anim group: " + go.name +
                " source json parse failed: " + e.Message + " — 降级为片段分析");
            return null;
        }
        if (g == null || g.events == null || g.events.Length == 0) return null;

        // 全屏特效组：没有成员可解析（宿主是相机 rig / 专用灯，不在物品列表），
        // 源 JSON 本身就是完整模型 —— 直接无损返回，不进成员重盖章，也绝不
        // 降级为片段分析（特效 clip 是 Light/相机曲线，片段分析必拒）。
        if (g.groupKind == "fx")
        {
            if (string.IsNullOrEmpty(g.displayName)) g.displayName = go.name;
            g.applyRootMotion = anim.applyRootMotion;
            LayoutEditorLog.Log("anim group: " + go.name + " imported fx group from embedded source (" +
                g.events.Length + " event(s), lossless)");
            return g;
        }

        // Re-resolve members: hierarchyPath first (survives Unity restarts), then
        // the live instance id (same-session). Unresolvable members are dropped
        // (web 端 cleanOrphanedAnimControls 同步清理）。
        var offsets = new List<AnimGroupMemberOffsetDto>();
        var statics = new List<AnimGroupMemberDto>();
        var memberGos = new List<GameObject>();
        foreach (var o in g.memberOffsets ?? new AnimGroupMemberOffsetDto[0])
        {
            if (o == null) continue;
            var m = ResolveSourceMember(o.instanceId, o.hierarchyPath);
            if (m == null)
            {
                LayoutEditorLog.LogWarning("anim group: " + go.name +
                    " source member unresolvable (" + (o.instanceId ?? "?") + " / " +
                    (o.hierarchyPath ?? "?") + ") — dropped");
                continue;
            }
            o.instanceId = "u:" + m.GetInstanceID();
            o.hierarchyPath = LayoutEditorHierarchy.GetHierarchyPath(m.transform);
            if (string.IsNullOrEmpty(o.displayName)) o.displayName = m.name;
            offsets.Add(o);
            memberGos.Add(m);
        }
        foreach (var ms in g.memberStatic ?? new AnimGroupMemberDto[0])
        {
            if (ms == null) continue;
            var m = ResolveSourceMember(ms.instanceId, ms.hierarchyPath);
            if (m == null)
            {
                LayoutEditorLog.LogWarning("anim group: " + go.name +
                    " source static member unresolvable (" + (ms.instanceId ?? "?") + " / " +
                    (ms.hierarchyPath ?? "?") + ") — dropped");
                continue;
            }
            ms.instanceId = "u:" + m.GetInstanceID();
            ms.hierarchyPath = LayoutEditorHierarchy.GetHierarchyPath(m.transform);
            if (string.IsNullOrEmpty(ms.displayName)) ms.displayName = m.name;
            statics.Add(ms);
            memberGos.Add(m);
        }
        if (memberGos.Count == 0)
        {
            LayoutEditorLog.LogWarning("anim group: " + go.name +
                " source has no resolvable members — 降级为片段分析");
            return null;
        }

        // Rebuild the flat member arrays with the same classification the clip
        // importer uses (prefab instance → item; AirFloor/Plane/Quad → floor
        // collider id; else plain object).
        var itemIds = new List<string>();
        var floorIds = new List<string>();
        var objectIds = new List<string>();
        foreach (var m in memberGos)
        {
            var id = "u:" + m.GetInstanceID();
            if (IsPrefabInstance(m)) itemIds.Add(id);
            else if (LooksLikeFloor(m))
            {
                var colGo = AirFloorRig.GetColliderObject(m);
                floorIds.Add("u:" + (colGo != null ? colGo.GetInstanceID() : m.GetInstanceID()));
            }
            else objectIds.Add(id);
        }

        // Member groups: baked sub-roots are direct children of the group root
        // whose children are members (same rule as the clip importer).
        var memberSet = new HashSet<GameObject>(memberGos);
        var memberGroups = new List<AnimGroupMemberGroupDto>();
        for (int i = 0; i < go.transform.childCount; i++)
        {
            var child = go.transform.GetChild(i);
            if (memberSet.Contains(child.gameObject)) continue;
            var ids = new List<string>();
            foreach (var m in memberGos)
            {
                if (m.transform.parent == child)
                    ids.Add("u:" + m.GetInstanceID());
            }
            if (ids.Count > 0)
            {
                memberGroups.Add(new AnimGroupMemberGroupDto
                {
                    id = "mg:" + child.name,
                    name = child.name,
                    memberInstanceIds = ids.ToArray()
                });
            }
        }

        g.memberOffsets = offsets.ToArray();
        g.memberStatic = statics.ToArray();
        g.itemInstanceIds = itemIds.ToArray();
        g.floorInstanceIds = floorIds.ToArray();
        g.objectInstanceIds = objectIds.ToArray();
        g.memberGroups = memberGroups.ToArray();
        if (string.IsNullOrEmpty(g.displayName)) g.displayName = go.name;
        // applyRootMotion 以场景当前值为准（用户可能在 Unity 里直接改过）。
        g.applyRootMotion = anim.applyRootMotion;
        LayoutEditorLog.Log("anim group: " + go.name + " imported from embedded source (" +
            g.events.Length + " event(s), " + (g.waypoints == null ? 0 : g.waypoints.Length) +
            " waypoint(s), lossless)");
        return g;
    }

    /// <summary>Resolve a source-json member: hierarchyPath (cross-session) first,
    /// then the live "u:" instance id (same session).</summary>
    private static GameObject ResolveSourceMember(string instanceId, string hierarchyPath)
    {
        if (!string.IsNullOrEmpty(hierarchyPath))
        {
            var t = LayoutEditorHierarchy.FindByPath(hierarchyPath);
            if (t != null) return t.gameObject;
        }
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

    /// <summary>Detect rotation curves that actually change (rotate events bake
    /// m_LocalRotation on every member). Extracts the total Y-axis angle (unwrapped
    /// across keys — baked keys are ≤90° apart), the spinning duration (last key
    /// where the angle still changes) and the direction from the reference path.</summary>
    private static bool TryExtractRotation(ClipData data, string refPath,
        out float degrees, out float seconds, out bool ccw)
    {
        degrees = 0f;
        seconds = 0f;
        ccw = false;
        AnimationCurve qy, qw;
        if (!data.ry.TryGetValue(refPath, out qy) || !data.rw.TryGetValue(refPath, out qw)) return false;
        if (qy.length < 2 || qw.length < 2) return false;
        float total = 0f;
        float prevAng = 0f;
        bool first = true;
        int n = Mathf.Min(qy.length, qw.length);
        for (int i = 0; i < n; i++)
        {
            // Y-axis self-rotation: angleY = 2·atan2(qy, qw), unwrapped by picking
            // the 360°-offset closest to the running total.
            float ang = 2f * Mathf.Atan2(qy[i].value, qw[i].value) * Mathf.Rad2Deg;
            if (first)
            {
                prevAng = ang;
                first = false;
                continue;
            }
            float d = ang - prevAng;
            while (d > 180f) d -= 360f;
            while (d < -180f) d += 360f;
            total += d;
            prevAng = ang;
            if (Mathf.Abs(d) > 0.001f) seconds = qy[i].time;
        }
        if (Mathf.Abs(total) < 1f) return false;
        degrees = Mathf.Clamp(SnapImport(Mathf.Abs(total)), 1f, 360f);
        ccw = total < 0f;
        seconds = Mathf.Max(0.1f, SnapImport(seconds));
        return true;
    }

    private static float SnapImport(float v)
    {
        return Mathf.Max(0f, Mathf.Round(v * 10f) / 10f);
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
            else if (b.propertyName == "m_LocalRotation.x" ||
                     b.propertyName == "m_LocalRotation.y" ||
                     b.propertyName == "m_LocalRotation.z" ||
                     b.propertyName == "m_LocalRotation.w")
            {
                var c = AnimationUtility.GetEditorCurve(clip, b);
                if (c == null || c.length == 0) continue;
                if (b.propertyName == "m_LocalRotation.x") data.rx[b.path] = c;
                else if (b.propertyName == "m_LocalRotation.y") data.ry[b.path] = c;
                else if (b.propertyName == "m_LocalRotation.z") data.rz[b.path] = c;
                else data.rw[b.path] = c;
                if (!data.paths.Contains(b.path)) data.paths.Add(b.path);
            }
        }
        if (data.paths.Count == 0) return null;
        return data;
    }

    private static bool BuildRoute(ClipData data, string refPath, Vector3 refLocal,
        string idPrefix, List<AnimGroupWaypointDto> waypoints, List<string> wpIds, out bool mirrored)
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
        AnimGroupWaypointDto prev = null;
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
            var wp = new AnimGroupWaypointDto
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

    /// <summary>Member's Y curve matches the reference lift/fall shape with a
    /// constant value offset — 相对 Δy 烘焙下各成员从自身基准高度起升（曲线形状
    /// 相同、值带恒定偏移，如台上物品 1→2 对工作台 0→1）。</summary>
    private static bool IsYParallel(ClipData clip, string path, string refPath)
    {
        AnimationCurve cy;
        AnimationCurve refCy;
        if (!clip.y.TryGetValue(path, out cy)) return false;
        if (!TryGetYCurve(clip, refPath, out refCy)) return false;
        if (cy.length != refCy.length) return false;
        float off = cy[0].value - refCy[0].value;
        for (int i = 0; i < cy.length; i++)
        {
            if (Mathf.Abs(cy[i].value - refCy[i].value - off) > Epsilon) return false;
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
        if (AirFloorRig.IsColliderObject(go))
            return true;
        if (AirFloorRig.IsWrapperName(go.name))
            return true;
        var mf = go.GetComponent<MeshFilter>();
        var mr = go.GetComponent<MeshRenderer>();
        if (mf == null || mr == null) return false;
        var mesh = mf.sharedMesh;
        if (mesh == null) return false;
        return mesh.name == "Plane" || mesh.name == "Quad";
    }
}
