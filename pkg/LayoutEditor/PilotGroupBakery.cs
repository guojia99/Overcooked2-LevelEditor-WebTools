using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using LevelEditor;
using LevelEditorStub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 摇杆操控组（AnimGroupDto.groupKind == "pilot"）烘焙器。
///
/// 与 AnimGroupBakery（烘 Animator 曲线、Trigger 驱动）互补：摇杆组把
/// 「地板 + 核心层物品 + 装饰」整组成员烘成一个**原版可驾驶刚体**——
/// Design/Pilot Objects/&lt;组名&gt; 根物体挂 Rigidbody + RigidbodyMotion +
/// PilotMovement + ObjectContainer，玩家在控制终端（摇杆，Terminal）上按
/// 交互键后由原版 ServerTerminal 会话把 ControlScheme 交给组根的
/// ServerPilotMovement：读摇杆轴 → SetVelocity 移动 → 松杆吸附最近网格
/// 中心；位置经原版 RigidbodyMotion → ServerWorldObjectSynchroniser 网络同步；
/// 站在组上的厨师/食材经 DynamicLandscapeParenting → ObjectContainer 骑乘。
/// 全链路原版组件，零自研同步代码。
///
/// 行走面 = 组根下的 "PilotFloor" 子 BoxCollider（Ground 层，组合包围盒，
/// 空洞被填平——非矩形组由前端/烘焙告警）。探测碰撞体（probe）放根物体
/// Y=-1.35 深层小盒：ServerPilotMovement.m_collider =
/// RequestComponentRecursive&lt;Collider&gt;() 深度优先命中根自身组件，深层小盒使
/// 网格占位只落在 Y=-1 波段（成员/家具占 Y=0 波段，互不冲突——成员桌台
/// 必须保有自己的网格占格，否则 Chef 交互扫描找不到台面）；同时保证
/// m_collider 非空（null 会让原版吸附路径反复 Deoccupy 网格原点单元格，
/// 误清无辜家具占格）。
///
/// 终端绑定权威在组的 terminalInstanceId（组 → 终端）；终端条目级
/// terminal.pilotableObjectInstanceId 保持为空，由本烘焙器直写
/// PseudoPrefabTerminalStub.pilotableObject = 组根（StubIO 的 Terminal
/// 校验/导出已对 pilot 绑定放行/跳过）。
/// </summary>
public static class PilotGroupBakery
{
    internal const string PilotObjectsRootName = "Pilot Objects";
    internal const string WalkBoxName = "PilotFloor";
    private const float DefaultMoveSpeed = 2.5f;
    private const float WalkBoxThickness = 0.4f;
    private const float SnapStep = 0.01f;

    /// <summary>本轮 Apply 中被摇杆组绑定的终端 item instanceId（BeginApply
    /// 收集；StubIO Terminal 空绑定分支据此跳过「降级为普通道具」——绑定
    /// 由 PilotGroupBakery 在烘焙期直写，晚于物品首轮 Apply）。</summary>
    private static HashSet<string> _pilotBoundTerminalIds;

    public static bool IsPilotGroup(AnimGroupDto group)
    {
        return group != null && group.groupKind == "pilot";
    }

    /// <summary>写回开始时收集摇杆组绑定的终端 id（须早于物品首轮 Apply）。</summary>
    public static void BeginApply(LayoutDocumentDto document)
    {
        _pilotBoundTerminalIds = new HashSet<string>();
        if (document == null || document.AnimControls == null || document.AnimControls.groups == null)
            return;
        foreach (var g in document.AnimControls.groups)
        {
            if (!IsPilotGroup(g) || string.IsNullOrEmpty(g.terminalInstanceId))
                continue;
            _pilotBoundTerminalIds.Add(g.terminalInstanceId);
        }
    }

    public static bool IsTerminalPilotBound(string instanceId)
    {
        return !string.IsNullOrEmpty(instanceId)
            && _pilotBoundTerminalIds != null
            && _pilotBoundTerminalIds.Contains(instanceId);
    }

    /// <summary>物体是否位于某个摇杆组根（Design/Pilot Objects/...）之下。
    /// Terminal 导出时用于跳过 pilotableObjectInstanceId（组根每次烘焙可重建，
    /// "u:" id 会过期成死引用 → 下次写回被误降级；权威引用在组的
    /// terminalInstanceId，由 AnimGroupImporter 反查回填）。</summary>
    public static bool IsUnderPilotRoot(Transform t)
    {
        while (t != null)
        {
            if (t.name == PilotObjectsRootName)
                return true;
            t = t.parent;
        }
        return false;
    }

    // ------------------------------------------------------------------- sync

    /// <summary>Bakes every pilot group in the document. Never throws — returns an
    /// error string (non-null) when groups could not be (fully) baked.</summary>
    public static string Sync(Scene scene, LayoutDocumentDto document,
        Dictionary<string, GameObject> createdObjects)
    {
        try
        {
            return SyncInner(scene, document, createdObjects);
        }
        catch (Exception e)
        {
            LayoutEditorLog.LogWarning("pilot group: bake exception: " + e);
            return "摇杆组写回异常：" + e.Message;
        }
    }

    private static string SyncInner(Scene scene, LayoutDocumentDto document,
        Dictionary<string, GameObject> createdObjects)
    {
        var errors = new List<string>();
        var groups = document != null && document.AnimControls != null
            ? document.AnimControls.groups : null;

        if (groups == null || groups.Length == 0)
        {
            CleanupStale(scene, new List<GameObject>());
            return null;
        }

        // 成员冲突消解：同一成员不允许同时进动画组与摇杆组（双烘焙会互相
        // re-parent 抢人）。摇杆组优先，动画组里的重叠 id 就地移除 + 告警。
        var pilotMemberIds = new HashSet<string>();
        foreach (var g in groups)
        {
            if (!IsPilotGroup(g)) continue;
            CollectIds(g, pilotMemberIds);
        }
        if (pilotMemberIds.Count > 0)
        {
            foreach (var g in groups)
            {
                if (g == null || IsPilotGroup(g) || g.groupKind == "fx") continue;
                int removed = RemoveIds(g, pilotMemberIds);
                if (removed > 0)
                    LayoutEditorLog.RecordApplyWarning("动画组「" + (g.displayName ?? "?") + "」有 " + removed +
                        " 个成员同时属于摇杆组，已从动画组移除（摇杆组优先；一个成员只能属于一个组）");
            }
        }

        // 重生锚点提醒：本关全部非背景地板都在摇杆组里 → 场景没有静态可行走
        /// 面（PlayerRespawnBehaviour 需要静态 Col_Floor 锚点，D8 决策）。
        WarnIfNoStaticFloor(document, pilotMemberIds);

        var keepRoots = new List<GameObject>();
        foreach (var g in groups)
        {
            if (!IsPilotGroup(g)) continue;

            // 分 P：摇杆组 v1 不支持（slot 停放/转场与运行时驾驶语义冲突）。
            if (!string.IsNullOrEmpty(g.partId) && g.partId != "base")
            {
                errors.Add("摇杆组「" + (g.displayName ?? "?") + "」不支持分 P（partId=" + g.partId + "），请在普通布局使用");
                continue;
            }

            try
            {
                var err = BakeGroup(g, document, createdObjects, keepRoots);
                if (!string.IsNullOrEmpty(err))
                    errors.Add(err);
            }
            catch (Exception bakeEx)
            {
                LayoutEditorLog.LogWarning("pilot group: bake group \"" + (g.displayName ?? "?")
                    + "\" threw, skipped: " + bakeEx);
                errors.Add("摇杆组「" + (g.displayName ?? "?") + "」烘焙异常（其余组已继续）: " + bakeEx.Message);
            }
        }

        CleanupStale(scene, keepRoots);
        return errors.Count > 0 ? string.Join("; ", errors.ToArray()) : null;
    }

    private static void CollectIds(AnimGroupDto g, HashSet<string> into)
    {
        foreach (var id in g.itemInstanceIds ?? new string[0])
            if (!string.IsNullOrEmpty(id)) into.Add(id);
        foreach (var id in g.floorInstanceIds ?? new string[0])
            if (!string.IsNullOrEmpty(id)) into.Add(id);
        foreach (var id in g.objectInstanceIds ?? new string[0])
            if (!string.IsNullOrEmpty(id)) into.Add(id);
    }

    private static int RemoveIds(AnimGroupDto g, HashSet<string> ids)
    {
        int removed = 0;
        removed += StripIds(ref g.itemInstanceIds, ids);
        removed += StripIds(ref g.floorInstanceIds, ids);
        removed += StripIds(ref g.objectInstanceIds, ids);
        return removed;
    }

    /// <summary>就地移除数组中属于 ids 的元素（Array.Resize 经 ref 写回字段）。</summary>
    private static int StripIds(ref string[] arr, HashSet<string> ids)
    {
        if (arr == null || arr.Length == 0) return 0;
        var kept = new List<string>(arr.Length);
        int removed = 0;
        foreach (var id in arr)
        {
            if (id != null && ids.Contains(id)) { removed++; continue; }
            kept.Add(id);
        }
        if (removed == 0) return 0;
        arr = kept.ToArray();
        return removed;
    }

    private static void WarnIfNoStaticFloor(LayoutDocumentDto document, HashSet<string> pilotMemberIds)
    {
        if (document == null || document.floors == null) return;
        int walkFloors = 0;
        int pilotFloors = 0;
        foreach (var f in document.floors)
        {
            if (f == null || f.surfaceKind == "background") continue;
            walkFloors++;
            if (!string.IsNullOrEmpty(f.instanceId) && pilotMemberIds.Contains(f.instanceId))
                pilotFloors++;
        }
        if (walkFloors > 0 && walkFloors == pilotFloors)
            LayoutEditorLog.RecordApplyWarning(
                "本关全部可行走地板都在摇杆组里——没有静态地面可供出生/重生锚定（出生点请放在可移动组外的静态面上，或保留至少一块静态地板）");
    }

    // ------------------------------------------------------------------- bake

    private static string BakeGroup(AnimGroupDto group, LayoutDocumentDto document,
        Dictionary<string, GameObject> createdObjects, List<GameObject> keepRoots)
    {
        LayoutEditorLog.Log("pilot group: baking group \"" + (group.displayName ?? "?") +
            "\" id=" + (group.id ?? "?") +
            " items:" + (group.itemInstanceIds == null ? 0 : group.itemInstanceIds.Length) +
            " floors:" + (group.floorInstanceIds == null ? 0 : group.floorInstanceIds.Length) +
            " objects:" + (group.objectInstanceIds == null ? 0 : group.objectInstanceIds.Length));

        // 跨会话成员解析：instance id 失效时按 importer 盖章的 hierarchyPath 兜底。
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
            var go = ResolveMember(id, pathById, createdObjects);
            if (go == null)
            {
                LayoutEditorLog.LogWarning("pilot group: scene object not found for item " +
                    (group.displayName ?? "?") + " (" + id + ")");
                continue;
            }
            members.Add(go);
        }
        foreach (var id in group.floorInstanceIds ?? new string[0])
        {
            var go = ResolveMember(id, pathById, createdObjects);
            if (go == null)
            {
                if (!RaftFloorGroupMembers.IsRaftFloorMember(id, document))
                {
                    LayoutEditorLog.LogWarning("pilot group: scene object not found for floor " +
                        (group.displayName ?? "?") + " (" + id + ")");
                }
                continue;
            }
            members.Add(go);
        }
        foreach (var id in group.objectInstanceIds ?? new string[0])
        {
            var go = ResolveMember(id, pathById, createdObjects);
            if (go == null)
            {
                LayoutEditorLog.LogWarning("pilot group: scene object not found for member " +
                    (group.displayName ?? "?") + " (" + id + ")");
                continue;
            }
            members.Add(go);
        }
        members.RemoveAll(IsDead);
        RaftFloorGroupMembers.AppendRaftPlankMembers(group, document, createdObjects, members);
        members.RemoveAll(IsDead);
        if (members.Count == 0)
        {
            LayoutEditorLog.RecordApplyWarning("摇杆组「" + (group.displayName ?? "?") +
                "」没有可解析的成员——本轮未烘焙（请在编辑器配置成员或删除该组）");
            return null;
        }

        // 组根：优先按 groupHierarchyPath 复用（跨写回幂等），否则按名新建。
        var container = LayoutEditorHierarchy.FindOrCreatePath("Design/" + PilotObjectsRootName);
        if (container == null)
            return "摇杆组「" + (group.displayName ?? "?") + "」：无法创建 Design/" + PilotObjectsRootName;
        var groupRoot = ResolveOrCreateGroupRoot(group, container);
        if (groupRoot == null)
            return "摇杆组「" + (group.displayName ?? "?") + "」：无法创建组根物体";
        group.groupHierarchyPath = LayoutEditorHierarchy.GetHierarchyPath(groupRoot);
        keepRoots.Add(groupRoot.gameObject);

        // 行走面包围盒（地板成员矩形 + walkable 物品成员矩形）。
        float minX, maxX, minZ, maxZ, walkY;
        bool hasWalk;
        int holeCells;
        ComputeWalkBounds(group, document, out minX, out maxX, out minZ, out maxZ,
            out walkY, out hasWalk, out holeCells);
        if (!hasWalk)
        {
            LayoutEditorLog.RecordApplyWarning("摇杆组「" + (group.displayName ?? "?") +
                "」没有地板成员——行走面包围盒按成员包围盒推算，建议至少包含一块地板");
            ComputeFallbackBounds(members, out minX, out maxX, out minZ, out maxZ);
            walkY = 0f;
        }
        if (holeCells > 0)
            LayoutEditorLog.RecordApplyWarning("摇杆组「" + (group.displayName ?? "?") +
                "」行走面包围盒内有约 " + holeCells + " 个空洞格将被填平为可行走（v1 为单一矩形碰撞盒）");
        float cx = 0.5f * (minX + maxX);
        float cz = 0.5f * (minZ + maxZ);
        float w = Mathf.Max(1.2f, maxX - minX);
        float d = Mathf.Max(1.2f, maxZ - minZ);

        // 根落位：包围盒中心的最近网格单元中心（原版松杆吸附按根坐标对齐
        // 网格——初始即单元中心保证组内地板与静态网格恒对齐）。
        // 先捕获成员世界坐标：成员可能已是根的子物体（上次烘焙），移动根
        // 会连带平移它们，捕获-复挂在根移动之后恢复。
        var memberWorld = new List<Vector3>(members.Count);
        foreach (var go in members)
            memberWorld.Add(go != null ? go.transform.position : Vector3.zero);

        var rootPos = NearestGridCellCenter(cx, cz, walkY);
        Undo.RecordObject(groupRoot, "Layout Editor Pilot Group");
        groupRoot.position = rootPos;

        // 淘汰不再属于本组的直系子物体：移出移动根（world-stays，不再随组
        // 移动；下次全量写回按普通物品/地板处理）。WalkBox 是 rig 部件豁免。
        ReparentStaleChildren(groupRoot, members);

        // 重挂成员（world-stays）+ 恢复捕获的世界坐标 + 唯一直系子名 +
        // ObjectContainer（骑乘锚点）+ AnimGridMemberSync（StaticGridLocation
        /// 成员换装随动）。memberGroups 在摇杆组强制扁平（v1）。
        foreach (var go in members)
        {
            if (go == null) continue;
            var cleaned = Regex.Replace(go.name, @"(\(\d+\))( \1)+$", "$1");
            if (cleaned != go.name)
            {
                Undo.RecordObject(go, "Layout Editor Pilot Group");
                go.name = cleaned;
            }
        }
        var childNames = new HashSet<string>();
        foreach (var child in groupRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child.parent != groupRoot) continue;
            if (child.name == WalkBoxName) continue;
            if (members.Contains(child.gameObject)) continue;
            childNames.Add(child.name);
        }
        for (int i = 0; i < members.Count; i++)
        {
            var go = members[i];
            if (go == null) continue;
            if (go.transform.parent != groupRoot)
                Undo.SetTransformParent(go.transform, groupRoot, "Layout Editor Pilot Group");
            if ((go.transform.position - memberWorld[i]).sqrMagnitude > 0.000001f)
            {
                Undo.RecordObject(go.transform, "Layout Editor Pilot Group");
                go.transform.position = memberWorld[i];
            }
            if (!childNames.Add(go.name))
            {
                var newName = go.name;
                int n = 1;
                while (!childNames.Add(newName = go.name + " (" + n + ")")) n++;
                go.name = newName;
            }
            if (!AirFloorRig.IsColliderObject(go) && go.GetComponent<ObjectContainer>() == null)
                Undo.AddComponent<ObjectContainer>(go);
            AnimGroupBakery.AttachAnimGridMemberSync(go);
        }

        // 地板成员位置规范化：文档世界坐标为权威（ApplyFloors 写的是
        // localPosition，根不在世界原点时与画布世界坐标可能漂移）。
        NormalizeFloorMemberPositions(group, document, createdObjects);

        EnsureRig(groupRoot, group, cx, cz, w, d, walkY, rootPos);
        BindTerminal(group, document, createdObjects, groupRoot);

        LayoutEditorLog.Log("pilot group: baked \"" + (group.displayName ?? "?") + "\" root=" +
            group.groupHierarchyPath + " members=" + members.Count +
            " walkBox=" + w.ToString("0.0") + "x" + d.ToString("0.0") +
            " speed=" + (group.moveSpeed > 0f ? group.moveSpeed : DefaultMoveSpeed));
        return null;
    }

    private static bool IsDead(GameObject go)
    {
        return go == null;
    }

    private static GameObject ResolveMember(string instanceId, Dictionary<string, string> pathById,
        Dictionary<string, GameObject> createdObjects)
    {
        if (string.IsNullOrEmpty(instanceId))
            return null;
        GameObject created;
        if (createdObjects != null && createdObjects.TryGetValue(instanceId, out created) && created != null)
            return created;
        if (instanceId.StartsWith("u:", StringComparison.Ordinal))
        {
            int id;
            if (int.TryParse(instanceId.Substring(2), out id))
            {
                var obj = EditorUtility.InstanceIDToObject(id) as GameObject;
                if (obj != null) return obj;
            }
        }
        string hp;
        if (pathById.TryGetValue(instanceId, out hp))
        {
            var t = LayoutEditorHierarchy.FindByPath(hp);
            if (t != null) return t.gameObject;
        }
        return null;
    }

    private static Transform ResolveOrCreateGroupRoot(AnimGroupDto group, Transform container)
    {
        if (!string.IsNullOrEmpty(group.groupHierarchyPath))
        {
            var existing = LayoutEditorHierarchy.FindByPath(group.groupHierarchyPath);
            if (existing != null) return existing;
        }
        var name = string.IsNullOrEmpty(group.displayName) ? "PilotGroup" : group.displayName;
        name = name.Replace('/', '_').Replace('\\', '_');
        return LayoutEditorHierarchy.FindOrCreatePath(
            "Design/" + PilotObjectsRootName + "/" + name);
    }

    // ------------------------------------------------------------- walk bounds

    /// <summary>行走面包围盒 = 地板成员矩形（背景板除外）∪ walkable 物品成员
    /// 矩形。walkY 取成员最低行走面（高度差 &gt; 0.1 告警——v1 单一矩形行走面）。
    /// holeCells = 包围盒内未被任何矩形覆盖的格数估算（v1 会被填平）。</summary>
    private static void ComputeWalkBounds(AnimGroupDto group, LayoutDocumentDto document,
        out float minX, out float maxX, out float minZ, out float maxZ,
        out float walkY, out bool hasWalk, out int holeCells)
    {
        minX = float.MaxValue; maxX = float.MinValue;
        minZ = float.MaxValue; maxZ = float.MinValue;
        walkY = float.MaxValue;
        hasWalk = false;
        var rects = new List<Vector4>(); // x, z, w, d
        float? firstY = null;
        bool heightWarned = false;

        if (document != null && document.floors != null)
        {
            var floorIds = new HashSet<string>(group.floorInstanceIds ?? new string[0]);
            foreach (var f in document.floors)
            {
                if (f == null || string.IsNullOrEmpty(f.instanceId) || !floorIds.Contains(f.instanceId)) continue;
                if (f.surfaceKind == "background") continue;
                float fcx = f.worldPosition != null ? f.worldPosition.x : (f.localPosition != null ? f.localPosition.x : 0f);
                float fcz = f.worldPosition != null ? f.worldPosition.z : (f.localPosition != null ? f.localPosition.z : 0f);
                float fw = f.widthUnits > 0f ? f.widthUnits : (f.widthCells > 0 ? f.widthCells * LayoutEditorCatalogLookup.GridCellSize : 1.2f);
                float fd = f.depthUnits > 0f ? f.depthUnits : (f.depthCells > 0 ? f.depthCells * LayoutEditorCatalogLookup.GridCellSize : 1.2f);
                float fy = FloorWalkY(f.localPosition != null ? f.localPosition.y : 0f);
                EncapsulateRect(fcx, fcz, fw, fd, fy, ref minX, ref maxX, ref minZ, ref maxZ, ref walkY, ref hasWalk);
                rects.Add(new Vector4(fcx, fcz, fw, fd));
                if (!firstY.HasValue) firstY = fy;
                else if (!heightWarned && Mathf.Abs(fy - firstY.Value) > 0.1f)
                {
                    heightWarned = true;
                    LayoutEditorLog.RecordApplyWarning("摇杆组「" + (group.displayName ?? "?") +
                        "」内地板高度不一致——v1 行走面按最低层生成，高出的地板不可行走");
                }
            }
        }

        // walkable 物品成员（walkway 地砖等；压力开关自带碰撞不计入——同
        // SyncWalkableToFloors 的口径）。
        if (document != null && document.items != null)
        {
            var itemIds = new HashSet<string>(group.itemInstanceIds ?? new string[0]);
            foreach (var it in document.items)
            {
                if (it == null || !it.walkable || string.IsNullOrEmpty(it.instanceId) || !itemIds.Contains(it.instanceId))
                    continue;
                var fileId = !string.IsNullOrEmpty(it.prefabAssetPath)
                    ? System.IO.Path.GetFileNameWithoutExtension(it.prefabAssetPath) : string.Empty;
                if (fileId.IndexOf("pressureswitch", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                float icx = it.worldPosition != null ? it.worldPosition.x : (it.localPosition != null ? it.localPosition.x : 0f);
                float icz = it.worldPosition != null ? it.worldPosition.z : (it.localPosition != null ? it.localPosition.z : 0f);
                float scx = it.localScale != null ? it.localScale.x : 1f;
                float scz = it.localScale != null ? it.localScale.z : 1f;
                if (scx <= 0f) scx = 1f;
                if (scz <= 0f) scz = 1f;
                float iw = (it.footprint != null && it.footprint.cellsX > 0 ? it.footprint.cellsX : 1) * LayoutEditorCatalogLookup.GridCellSize * scx;
                float idp = (it.footprint != null && it.footprint.cellsZ > 0 ? it.footprint.cellsZ : 1) * LayoutEditorCatalogLookup.GridCellSize * scz;
                float iy = FloorWalkY(it.localPosition != null ? it.localPosition.y : 0f);
                EncapsulateRect(icx, icz, iw, idp, iy, ref minX, ref maxX, ref minZ, ref maxZ, ref walkY, ref hasWalk);
                rects.Add(new Vector4(icx, icz, iw, idp));
            }
        }

        if (!hasWalk)
        {
            holeCells = 0;
            return;
        }
        if (walkY == float.MaxValue) walkY = 0f;
        holeCells = CountHoleCells(rects, minX, maxX, minZ, maxZ);
    }

    private static void EncapsulateRect(float cx, float cz, float w, float d, float y,
        ref float minX, ref float maxX, ref float minZ, ref float maxZ, ref float walkY, ref bool hasWalk)
    {
        hasWalk = true;
        if (cx - w * 0.5f < minX) minX = cx - w * 0.5f;
        if (cx + w * 0.5f > maxX) maxX = cx + w * 0.5f;
        if (cz - d * 0.5f < minZ) minZ = cz - d * 0.5f;
        if (cz + d * 0.5f > maxZ) maxZ = cz + d * 0.5f;
        if (y < walkY) walkY = y;
    }

    /// <summary>包围盒内未被任何成员矩形覆盖的格数估算（按 1.2 格步进采样
    /// 格中心，中心不被覆盖即算洞）。</summary>
    private static int CountHoleCells(List<Vector4> rects, float minX, float maxX, float minZ, float maxZ)
    {
        int holes = 0;
        for (float px = minX + 0.6f; px < maxX; px += 1.2f)
        {
            for (float pz = minZ + 0.6f; pz < maxZ; pz += 1.2f)
            {
                bool covered = false;
                foreach (var r in rects)
                {
                    if (Mathf.Abs(px - r.x) <= r.z * 0.5f + 0.05f && Mathf.Abs(pz - r.y) <= r.w * 0.5f + 0.05f)
                    {
                        covered = true;
                        break;
                    }
                }
                if (!covered) holes++;
            }
        }
        return holes;
    }

    private static void ComputeFallbackBounds(List<GameObject> members,
        out float minX, out float maxX, out float minZ, out float maxZ)
    {
        minX = float.MaxValue; maxX = float.MinValue;
        minZ = float.MaxValue; maxZ = float.MinValue;
        foreach (var go in members)
        {
            if (go == null) continue;
            var p = go.transform.position;
            if (p.x - 0.6f < minX) minX = p.x - 0.6f;
            if (p.x + 0.6f > maxX) maxX = p.x + 0.6f;
            if (p.z - 0.6f < minZ) minZ = p.z - 0.6f;
            if (p.z + 0.6f > maxZ) maxZ = p.z + 0.6f;
        }
        if (minX > maxX) { minX = -0.6f; maxX = 0.6f; }
        if (minZ > maxZ) { minZ = -0.6f; maxZ = 0.6f; }
    }

    private static float FloorWalkY(float y)
    {
        return (y >= -0.051f && y <= 0.05f) ? 0f : y;
    }

    // ------------------------------------------------------------------- rig

    private static Vector3 NearestGridCellCenter(float cx, float cz, float walkY)
    {
        try
        {
            var grids = UnityEngine.Object.FindObjectsOfType<GridManager>();
            if (grids != null && grids.Length > 0)
            {
                var gm = grids[0];
                var idx = gm.GetGridLocationFromPos(new Vector3(cx, walkY, cz));
                var p = gm.GetPosFromGridLocation(idx);
                return new Vector3(p.x, 0f, p.z);
            }
        }
        catch (Exception e)
        {
            LayoutEditorLog.LogWarning("pilot group: grid lookup failed, fallback 1.2 lattice: " + e.Message);
        }
        return new Vector3(Mathf.Round(cx / 1.2f) * 1.2f, 0f, Mathf.Round(cz / 1.2f) * 1.2f);
    }

    private static void EnsureRig(Transform groupRoot, AnimGroupDto group,
        float cx, float cz, float w, float d, float walkY, Vector3 rootPos)
    {
        var go = groupRoot.gameObject;

        // 行走面：Ground 层组合包围盒（v1 填平空洞）。子物体而非根组件——
        // ServerPilotMovement.m_collider = RequestComponentRecursive<Collider>()
        // 深度优先命中根自身组件，根上只保留探针，行走面放子物体不干扰。
        var walk = groupRoot.Find(WalkBoxName);
        if (walk == null)
        {
            var walkGo = new GameObject(WalkBoxName);
            Undo.RegisterCreatedObjectUndo(walkGo, "Layout Editor Pilot Group");
            walk = walkGo.transform;
            walk.SetParent(groupRoot, false);
        }
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer < 0) groundLayer = 9;
        Undo.RecordObject(walk.gameObject, "Layout Editor Pilot Group");
        walk.gameObject.layer = groundLayer;
        walk.localPosition = new Vector3(cx - rootPos.x, walkY - WalkBoxThickness * 0.5f - rootPos.y, cz - rootPos.z);
        walk.localRotation = Quaternion.identity;
        walk.localScale = Vector3.one;
        var walkCol = walk.GetComponent<BoxCollider>();
        if (walkCol == null) walkCol = Undo.AddComponent<BoxCollider>(walk.gameObject);
        else Undo.RecordObject(walkCol, "Layout Editor Pilot Group");
        walkCol.size = new Vector3(w, WalkBoxThickness, d);
        walkCol.center = Vector3.zero;

        // 探针碰撞体（Y=-1.35 深层小盒）：网格占位只落 Y=-1 波段（成员桌台
        // 保有 Y=0 占格供 Chef 交互扫描）；同时 m_collider 非空避免原版吸附
        // 路径反复 Deoccupy 网格原点单元格。
        var probe = go.GetComponent<BoxCollider>();
        if (probe == null)
            probe = Undo.AddComponent<BoxCollider>(go);
        else
            Undo.RecordObject(probe, "Layout Editor Pilot Group");
        probe.center = new Vector3(0f, -1.35f, 0f);
        probe.size = new Vector3(0.3f, 0.3f, 0.3f);
        probe.isTrigger = false;

        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = Undo.AddComponent<Rigidbody>(go);
        else Undo.RecordObject(rb, "Layout Editor Pilot Group");
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;

        var motion = go.GetComponent<RigidbodyMotion>();
        if (motion == null) motion = Undo.AddComponent<RigidbodyMotion>(go);

        var pilot = go.GetComponent<PilotMovement>();
        if (pilot == null) pilot = Undo.AddComponent<PilotMovement>(go);
        else Undo.RecordObject(pilot, "Layout Editor Pilot Group");
        pilot.MoveSpeed = group.moveSpeed > 0f ? group.moveSpeed : DefaultMoveSpeed;
        // [AssignComponent] 只是游戏编辑器抽屉属性，反编译宿主不会自动注入：
        // 必须烘焙期显式赋值，否则 PilotMovement.Start()/ServerPilotMovement NRE。
        if (pilot.RigidbodyMotion == null)
        {
            Undo.RecordObject(pilot, "Layout Editor Pilot Group");
            pilot.RigidbodyMotion = motion;
        }

        if (go.GetComponent<ObjectContainer>() == null)
            Undo.AddComponent<ObjectContainer>(go);

        EditorUtility.SetDirty(go);
    }

    // --------------------------------------------------------------- terminal

    private static void BindTerminal(AnimGroupDto group, LayoutDocumentDto document,
        Dictionary<string, GameObject> createdObjects, Transform groupRoot)
    {
        // 绑定权威 = 组的 terminalInstanceId；其它指向本根的终端绑定全部清空
        // （终端被移出本组 / 组换绑终端后不留残绑）。
        var boundGo = ResolveTerminalGo(group.terminalInstanceId, createdObjects);
        foreach (var stub in AllTerminalStubs())
        {
            if (stub == null || stub.pilotableObject == null) continue;
            if (stub.pilotableObject != groupRoot.gameObject) continue;
            if (boundGo != null && stub.gameObject == boundGo) continue;
            Undo.RecordObject(stub, "Layout Editor Pilot Group");
            stub.pilotableObject = null;
            EditorUtility.SetDirty(stub);
        }

        if (string.IsNullOrEmpty(group.terminalInstanceId))
        {
            LayoutEditorLog.RecordApplyWarning("摇杆组「" + (group.displayName ?? "?") +
                "」未绑定控制终端（摇杆）——玩家无法驾驶该组；在组设置或终端右键「控制目标」里绑定");
            return;
        }
        if (boundGo == null)
        {
            LayoutEditorLog.RecordApplyWarning("摇杆组「" + (group.displayName ?? "?") +
                "」绑定的控制终端（" + group.terminalInstanceId + "）在场景中未找到——绑定已忽略");
            return;
        }
        var boundStub = EnsureTerminalStub(boundGo);
        if (boundStub == null)
        {
            LayoutEditorLog.RecordApplyWarning("摇杆组「" + (group.displayName ?? "?") +
                "」绑定的目标无法恢复为控制终端（Terminal）物品：" + boundGo.name);
            return;
        }
        Undo.RecordObject(boundStub, "Layout Editor Pilot Group");
        boundStub.pilotableObject = groupRoot.gameObject;
        EditorUtility.SetDirty(boundStub);
        PushPilotableToChildTerminal(boundGo, groupRoot.gameObject);
        WireDriveAuthority(groupRoot, boundGo);
    }

    private static void WireDriveAuthority(Transform groupRoot, GameObject terminalPseudoRoot)
    {
        if (groupRoot == null || terminalPseudoRoot == null)
            return;
        AnimGroupBakery.AttachAnimPilotFloorMarker(groupRoot.gameObject, terminalPseudoRoot);
    }

    /// <summary>摇杆曾被「未配置 pilotable」降级时只剩 PseudoPrefabStub，需升回 Terminal 派生。</summary>
    private static PseudoPrefabTerminalStub EnsureTerminalStub(GameObject boundGo)
    {
        if (boundGo == null)
            return null;
        var stub = boundGo.GetComponent<PseudoPrefabTerminalStub>();
        if (stub != null)
            return stub;
        var baseStub = boundGo.GetComponent<PseudoPrefabStub>();
        PseudoPrefabSO so = baseStub != null ? baseStub.pseudoPrefabSO : null;
        if (baseStub != null)
            Undo.DestroyObjectImmediate(baseStub);
        stub = Undo.AddComponent<PseudoPrefabTerminalStub>(boundGo);
        if (so != null)
            stub.pseudoPrefabSO = so;
        var runtime = boundGo.GetComponent<PseudoPrefab>();
        if (runtime != null && !(runtime is PseudoPrefabTerminal))
            Undo.DestroyObjectImmediate(runtime);
        if (boundGo.GetComponent<PseudoPrefabTerminal>() == null)
            Undo.AddComponent<PseudoPrefabTerminal>(boundGo);
        return stub;
    }

    /// <summary>编辑期 child 已生成时，把绑定直写到 bundle 内 Terminal（Play 不依赖 stub 时序）。</summary>
    private static void PushPilotableToChildTerminal(GameObject terminalPseudoRoot, GameObject pilotRoot)
    {
        if (terminalPseudoRoot == null || pilotRoot == null)
            return;
        var pilot = pilotRoot.GetComponent<PilotMovement>();
        if (pilot == null)
            return;
        var pp = terminalPseudoRoot.GetComponent<PseudoPrefab>();
        if (pp == null || pp.childGameObject == null)
            return;
        var child = pp.childGameObject;
        StripForwardTriggersOnBundleChild(child);
        var term = child.GetComponent<Terminal>();
        if (term == null)
            return;
        if (term.m_pilotableObject != pilot)
        {
            Undo.RecordObject(term, "Layout Editor Pilot Group");
            term.m_pilotableObject = pilot;
            EditorUtility.SetDirty(term);
        }
    }

    private static void StripForwardTriggersOnBundleChild(GameObject child)
    {
        if (child == null)
            return;
        var forwards = child.GetComponents<ForwardTriggerToTarget>();
        for (int i = 0; i < forwards.Length; i++)
        {
            var f = forwards[i];
            if (f != null)
                Undo.DestroyObjectImmediate(f);
        }
    }

    private static GameObject ResolveTerminalGo(string instanceId,
        Dictionary<string, GameObject> createdObjects)
    {
        if (string.IsNullOrEmpty(instanceId)) return null;
        GameObject created;
        if (createdObjects != null && createdObjects.TryGetValue(instanceId, out created) && created != null)
            return created;
        if (instanceId.StartsWith("u:", StringComparison.Ordinal))
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

    private static List<PseudoPrefabTerminalStub> AllTerminalStubs()
    {
        var list = new List<PseudoPrefabTerminalStub>();
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid()) return list;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root == null) continue;
            list.AddRange(root.GetComponentsInChildren<PseudoPrefabTerminalStub>(true));
        }
        return list;
    }

    // ----------------------------------------------------------------- stale

    /// <summary>把不再属于本组的直系子物体移出移动根（world-stays，不再随组
    /// 移动；下次全量写回按普通物品/地板处理）。WalkBox 是 rig 部件豁免。</summary>
    private static void ReparentStaleChildren(Transform groupRoot, List<GameObject> members)
    {
        var memberSet = new HashSet<GameObject>(members);
        var container = groupRoot.parent != null ? groupRoot.parent : groupRoot.root;
        var moved = new List<Transform>();
        for (int i = 0; i < groupRoot.childCount; i++)
        {
            var child = groupRoot.GetChild(i);
            if (child == null || child.name == WalkBoxName) continue;
            if (memberSet.Contains(child.gameObject)) continue;
            moved.Add(child);
        }
        foreach (var child in moved)
        {
            LayoutEditorLog.Log("pilot group: member removed from group — reparenting " +
                child.name + " out of " + groupRoot.name);
            Undo.SetTransformParent(child, container, "Layout Editor Pilot Group");
        }
    }

    /// <summary>地板成员位置以文档世界坐标为权威（根不在世界原点时
    /// localPosition 与画布坐标会漂移；仅写位置，旋转/缩放不动——
    /// ApplyFloors 已按文档应用）。</summary>
    private static void NormalizeFloorMemberPositions(AnimGroupDto group, LayoutDocumentDto document,
        Dictionary<string, GameObject> createdObjects)
    {
        if (document == null || document.floors == null) return;
        var floorIds = new HashSet<string>(group.floorInstanceIds ?? new string[0]);
        if (floorIds.Count == 0) return;
        foreach (var f in document.floors)
        {
            if (f == null || string.IsNullOrEmpty(f.instanceId) || !floorIds.Contains(f.instanceId))
                continue;
            var go = ResolveMember(f.instanceId, new Dictionary<string, string>(), createdObjects);
            if (go == null) continue;
            float wx = f.worldPosition != null ? f.worldPosition.x
                : (f.localPosition != null ? f.localPosition.x : go.transform.position.x);
            float wz = f.worldPosition != null ? f.worldPosition.z
                : (f.localPosition != null ? f.localPosition.z : go.transform.position.z);
            float wy = f.worldPosition != null ? f.worldPosition.y
                : (f.localPosition != null ? f.localPosition.y : go.transform.position.y);
            wx = Mathf.Round(wx / SnapStep) * SnapStep;
            wz = Mathf.Round(wz / SnapStep) * SnapStep;
            var p = new Vector3(wx, wy, wz);
            if ((go.transform.position - p).sqrMagnitude > 0.000001f)
            {
                Undo.RecordObject(go.transform, "Layout Editor Pilot Group");
                go.transform.position = p;
            }
        }
    }

    /// <summary>文档中已不存在的摇杆组根：剥离 rig（成员原地保留在
    /// Design/Pilot Objects 层级下，不再随组移动）；指向它的终端绑定清空。</summary>
    private static void CleanupStale(Scene scene, List<GameObject> keepRoots)
    {
        var container = LayoutEditorHierarchy.FindByPath("Design/" + PilotObjectsRootName);
        if (container == null) return;
        var doomed = new List<Transform>();
        for (int i = 0; i < container.childCount; i++)
        {
            var child = container.GetChild(i);
            if (child == null) continue;
            if (child.GetComponent<PilotMovement>() == null) continue;
            if (keepRoots.Contains(child.gameObject)) continue;
            doomed.Add(child);
        }
        foreach (var root in doomed)
        {
            LayoutEditorLog.RecordApplyWarning("摇杆组已删除：剥离 " +
                LayoutEditorHierarchy.GetHierarchyPath(root) +
                " 的驾驶 rig（成员原地保留，终端绑定已清空）");
            foreach (var stub in AllTerminalStubs())
            {
                if (stub == null || stub.pilotableObject == null) continue;
                if (stub.pilotableObject != root.gameObject) continue;
                Undo.RecordObject(stub, "Layout Editor Pilot Group");
                stub.pilotableObject = null;
                EditorUtility.SetDirty(stub);
            }

            var walk = root.Find(WalkBoxName);
            if (walk != null)
                Undo.DestroyObjectImmediate(walk.gameObject);

            var probe = root.GetComponent<BoxCollider>();
            if (probe != null)
                Undo.DestroyObjectImmediate(probe);

            var pilot = root.GetComponent<PilotMovement>();
            if (pilot != null) Undo.DestroyObjectImmediate(pilot);
            var motion = root.GetComponent<RigidbodyMotion>();
            if (motion != null) Undo.DestroyObjectImmediate(motion);
            var rb = root.GetComponent<Rigidbody>();
            if (rb != null) Undo.DestroyObjectImmediate(rb);
        }
    }
}
