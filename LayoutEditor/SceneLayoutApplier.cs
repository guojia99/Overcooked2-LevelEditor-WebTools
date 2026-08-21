using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using LevelEditor;

public static class SceneLayoutApplier
{
    private static readonly string[] ThemeBackgroundPrefabNames = { "Sky", "raft_water", "alien_gue" };

    public static string Apply(LayoutDocumentDto document, float snapStep, bool syncWalkable, bool itemsOnly = false)
    {
        return Apply(document, snapStep, syncWalkable, itemsOnly ? "items" : null);
    }

    /// <summary>
    /// <paramref name="only"/> scopes the write-back to one layer:
    /// null = full, "items" = gameplay items only, "decor" = decor only,
    /// "floors" = floors/background + surface items only.
    /// </summary>
    public static string Apply(LayoutDocumentDto document, float snapStep, bool syncWalkable, string only)
    {
        if (document == null || document.items == null)
            return "Empty layout document.";

        document.items = PruneThemeBackgroundItems(document.items);

        var scene = EditorSceneManager.GetActiveScene();
        if (!string.IsNullOrEmpty(document.sceneAssetPath) && scene.path != document.sceneAssetPath)
        {
            if (!System.IO.File.Exists(document.sceneAssetPath))
                return "Scene not found: " + document.sceneAssetPath;
            EditorSceneManager.OpenScene(document.sceneAssetPath);
            scene = EditorSceneManager.GetActiveScene();
        }

        if (!LayoutEditorSafety.PrepareSceneForApply())
            return LayoutEditorSafety.LastError;

        var before = SceneLayoutExporter.ExportFromScene();
        RemoveUnmatchedSceneItems(before, document.items, only);

        var usedSceneObjectIds = new HashSet<int>();
        var createdObjects = new Dictionary<string, GameObject>();
        foreach (var item in document.items)
        {
            if (item == null)
                continue;

            if (item.stubKind == "Collision")
            {
                ApplyCollisionItem(item, snapStep, usedSceneObjectIds, createdObjects);
                continue;
            }

            if (string.IsNullOrEmpty(item.prefabAssetPath) && string.IsNullOrEmpty(item.prefabGuid))
            {
                Debug.LogWarning("[LayoutEditor] Apply: item skipped — no prefabAssetPath or prefabGuid (displayName: " + (item.displayName ?? "?") + ")");
                continue;
            }

            var assetPath = item.prefabAssetPath;
            if (string.IsNullOrEmpty(assetPath) && !string.IsNullOrEmpty(item.prefabGuid))
                assetPath = AssetDatabase.GUIDToAssetPath(item.prefabGuid);

            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogWarning("[LayoutEditor] Apply: item skipped — cannot resolve asset path (displayName: " + (item.displayName ?? "?") + ", guid: " + (item.prefabGuid ?? "?") + ")");
                continue;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                Debug.LogWarning("[LayoutEditor] Apply: item skipped — prefab not found at " + assetPath + " (displayName: " + (item.displayName ?? "?") + ")");
                continue;
            }

            var itemSnap = SnapStepForItem(item, snapStep);
            var pos = item.localPosition != null ? item.localPosition.ToVector3() : Vector3.zero;
            pos = SnapVector(pos, itemSnap);
            var rotY = item.localRotationY;

            // New instances are placed by world position: the doc's parentPath
            // may resolve to a parent whose offset differs from the source scene
            // (e.g. Art/Ground/Left at x=-12.8) or was just created at the origin
            // by FindOrCreatePath — localPosition alone would shift the item.
            Vector3? worldPos = null;
            if (item.worldPosition != null)
            {
                var wp = item.worldPosition.ToVector3();
                wp.x = SnapScalar(wp.x, itemSnap);
                wp.z = SnapScalar(wp.z, itemSnap);
                worldPos = wp;
            }

            if (item.instanceId != null && item.instanceId.StartsWith("new:", StringComparison.Ordinal))
            {
                var created = CreateInstance(item, prefab, assetPath, pos, rotY, worldPos);
                if (created != null)
                    createdObjects[item.instanceId] = created;
            }
            else
            {
                var t = FindItemTransform(item);
                if (t != null)
                {
                    var objectId = t.gameObject.GetInstanceID();
                    if (!usedSceneObjectIds.Contains(objectId))
                    {
                        usedSceneObjectIds.Add(objectId);
                        Undo.RecordObject(t, "Layout Editor Move");
                        var posFull = item.localPosition != null ? item.localPosition.ToVector3() : t.localPosition;
                        posFull.x = SnapScalar(posFull.x, itemSnap);
                        posFull.z = SnapScalar(posFull.z, itemSnap);
                        t.localPosition = posFull;
                        var rotX = item.localRotationX != 0f ? item.localRotationX : t.localEulerAngles.x;
                        t.localEulerAngles = new Vector3(rotX, rotY, t.localEulerAngles.z);
                        ApplyItemScale(t, item);
                        LayoutEditorStubIO.ApplyStub(t.gameObject, item);
                        if (!string.IsNullOrEmpty(item.instanceId))
                            createdObjects[item.instanceId] = t.gameObject;
                        continue;
                    }
                }

                var created = CreateInstance(item, prefab, assetPath, pos, rotY, worldPos);
                if (created != null && !string.IsNullOrEmpty(item.instanceId))
                    createdObjects[item.instanceId] = created;
            }
        }

        // Second pass: teleportal exit pairing and serving station plate return binding
        // need both referenced objects to exist.
        foreach (var item in document.items)
        {
            if (item == null)
                continue;

            var isTeleportal = item.stubKind == "Teleportal" && item.teleportal != null;
            var isServing = item.stubKind == "ServingStation" && item.servingStation != null;
            var isTerminal = item.stubKind == "Terminal" && item.terminal != null;
            if (!isTeleportal && !isServing && !isTerminal)
                continue;

            GameObject go;
            if (string.IsNullOrEmpty(item.instanceId) || !createdObjects.TryGetValue(item.instanceId, out go))
            {
                var t = FindItemTransform(item);
                go = t != null ? t.gameObject : null;
            }
            if (go == null)
                continue;

            if (isTeleportal)
                LayoutEditorStubIO.ApplyTeleportalExit(go, item.teleportal.exitPortalInstanceId ?? "", createdObjects);
            else if (isTerminal)
                LayoutEditorStubIO.ApplyTerminalPilotable(go, item.terminal.pilotableObjectInstanceId ?? "", createdObjects);
            else
                LayoutEditorStubIO.ApplyServingStationPlateReturns(go, item.servingStation.plateReturnInstanceIds, createdObjects);
        }

        // Second pass (cont.): document-level switch links (按钮 → 断头台/饮料机/酱料机)。
        // 只在文档携带链接时写入；空/缺失时不动场景里既有的联动（避免误清手工配置）。
        LayoutEditorStubIO.ApplySwitchLinks(document.switchLinks, createdObjects);

        // Snap floors with the web-sent precision (default 0.01). Floor centers sit on
        // half-cell (0.6) multiples, which are also multiples of that precision, so the
        // round trip is lossless. Snapping to full GridCellSize (1.2) would shift
        // even-sized floors by half a cell, breaking flush adjacency.
        if (only == null || only == "floors")
        {
            ApplyFloors(document, snapStep, createdObjects);
            if (syncWalkable)
                SyncWalkableToFloors(document, snapStep);
        }

        // Camera background/FOV + Art/Lights colors — full writes only (scoped writes
        // never touch the camera or the lights).
        string cameraLightError = null;
        if (only == null)
        {
            cameraLightError = ApplyCameraInfo(document.cameraInfo);
            var lightsError = ApplyLights(document.lights);
            if (!string.IsNullOrEmpty(lightsError))
            {
                cameraLightError = string.IsNullOrEmpty(cameraLightError)
                    ? lightsError
                    : cameraLightError + "; " + lightsError;
            }
        }

        // Bake move controls into the scene — full writes only (scoped writes never
        // touch the scene's existing move groups). The scene itself (group roots +
        // Animator + TriggerQueue/TriggerTimer + controller/clips) is the single
        // source of truth: no external JSON config is kept.
        string bakeError = null;
        if (only == null && document.moveControls != null)
        {
            // 按钮联动 phase 1：为被绑定的移动组覆写 start/end 触发器（须在烘焙前）。
            if (document.buttonLinks != null)
                ButtonLinkBakery.PrepareGroups(document);

            // Remap "new:" group member IDs to the real Unity IDs of objects created this pass.
            foreach (var group in document.moveControls.groups ?? new MoveGroupDto[0])
            {
                if (group == null) continue;
                RemapNewIds(group.itemInstanceIds, createdObjects);
                RemapNewIds(group.floorInstanceIds, createdObjects);
                RemapNewIds(group.objectInstanceIds, createdObjects);
            }

            bakeError = MoveControlBakery.Sync(scene, document.moveControls);
            if (!string.IsNullOrEmpty(bakeError))
                LayoutEditorLog.LogWarning(bakeError);

            // 按钮联动 phase 2：移动组烘焙完成后创建逻辑 helper 并接线（组根须已存在）。
            if (document.buttonLinks != null)
            {
                var blError = ButtonLinkBakery.Sync(scene, document, createdObjects);
                if (!string.IsNullOrEmpty(blError))
                {
                    LayoutEditorLog.LogWarning(blError);
                    bakeError = string.IsNullOrEmpty(bakeError) ? blError : bakeError + "; " + blError;
                }
            }

            // 按钮事件组：独立的按钮逻辑 helper（Design/Button Event Logic），
            // 事件目标为伪根（按名字广播），done 中继写到目标伪根（Play 期挂到 child）。
            if (document.buttonEvents != null)
            {
                var beError = ButtonEventBakery.Sync(scene, document, createdObjects);
                if (!string.IsNullOrEmpty(beError))
                {
                    LayoutEditorLog.LogWarning(beError);
                    bakeError = string.IsNullOrEmpty(bakeError) ? beError : bakeError + "; " + beError;
                }
            }
        }

        // 烤菜烤盘「默认能放」：把所选烤菜菜谱的叶食材追加为场景烤盘 stub 的额外食材
        // （allowedIngredientSOs，只增不删）。基础版烤盘（utensil_roasting_tray）内置
        // approvedContents 只含基础/dlc07 烤菜食材，dlc09 节点需显式登记才能放入——
        // 覆盖「手动放置烤盘未跑自动填充」的情况。
        var roastInfo = LayoutEditorLevelInfoResolver.ResolveForScene(scene.path);
        if (roastInfo != null)
        {
            LayoutEditorRoastTrayFill.EnsureRoastTrayIngredients(roastInfo);
            LayoutEditorHotPotFill.EnsureHotPotIngredients(roastInfo);
        }

        // After mutating placeholder transforms, persist with the canonical Tools workflow:
        // Toggle Prepare For Building (strip temp-loaded children so they aren't baked into the
        // saved scene) -> Save to disk -> Reload Pseudo Assets (re-load children, restore UI).
        // Groups that failed to bake are reported but never prevent the save — a write-back
        // that does not persist to disk is worse than a partial one.
        LayoutEditorPseudoReload.EnsurePrepareForBuilding();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        LayoutEditorPseudoReload.ReloadPseudoAssetsFull();

        // Reload 会从 bundle 重建伪 prefab child（EditorGridSnap 的 X/Z 约束随之复位），
        // 写回末尾即时解除断头台/石炉台的半格约束，防编辑模式每帧拉回整格。
        LayoutEditorGridSnapGuard.RelaxGridSnapOnScene();

        var partialError = cameraLightError;
        if (!string.IsNullOrEmpty(bakeError))
            partialError = string.IsNullOrEmpty(partialError) ? bakeError : partialError + "; " + bakeError;
        return string.IsNullOrEmpty(partialError)
            ? null
            : "部分写回失败（场景已保存）：" + partialError;
    }

    /// <summary>定位场景游戏相机：优先 tag=MainCamera，兜底第一个启用相机。</summary>
    private static Camera FindSceneCamera()
    {
        var cam = Camera.main;
        if (cam != null)
            return cam;

        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid())
            return null;
        foreach (var rootGo in scene.GetRootGameObjects())
        {
            cam = rootGo.GetComponentInChildren<Camera>();
            if (cam != null)
                return cam;
        }
        return null;
    }

    /// <summary>写回相机背景色（同时确保 clearFlags=SolidColor）与 FOV。
    ///  运行时从不写这两个值，改场景序列化即生效。</summary>
    private static string ApplyCameraInfo(CameraInfoDto info)
    {
        if (info == null)
            return null;

        var cam = FindSceneCamera();
        if (cam == null)
            return "场景中未找到游戏相机，背景色/FOV 未写入";

        var changed = false;
        Color bg;
        if (!string.IsNullOrEmpty(info.backgroundColor) &&
            ColorUtility.TryParseHtmlString(info.backgroundColor, out bg))
        {
            Undo.RecordObject(cam, "Layout Editor Camera");
            cam.backgroundColor = bg;
            cam.clearFlags = CameraClearFlags.SolidColor;
            changed = true;
        }
        if (info.fieldOfView > 0f)
        {
            Undo.RecordObject(cam, "Layout Editor Camera");
            cam.fieldOfView = Mathf.Clamp(info.fieldOfView, 1f, 179f);
            changed = true;
        }
        if (changed)
            EditorUtility.SetDirty(cam);
        return null;
    }

    /// <summary>写回 Art/Lights 非 prefab 灯光的颜色/强度/范围/启用状态。
    ///  按 hierarchyPath 匹配；场景缺失则新建（方向角用导出快照），
    ///  场景中多余的非 prefab 灯光（web 已删除）一并移除。</summary>
    private static string ApplyLights(LightInfoDto[] lights)
    {
        if (lights == null)
            return null;

        var keepPaths = new HashSet<string>();
        foreach (var dto in lights)
        {
            if (dto == null || string.IsNullOrEmpty(dto.hierarchyPath))
                continue;
            keepPaths.Add(dto.hierarchyPath);

            var t = LayoutEditorHierarchy.FindByPath(dto.hierarchyPath);
            var light = t != null ? t.GetComponent<Light>() : null;
            if (light == null)
            {
                var parentPath = ParentPathOf(dto.hierarchyPath);
                var leafName = LeafNameOf(dto.hierarchyPath);
                var parent = string.IsNullOrEmpty(parentPath)
                    ? null
                    : LayoutEditorHierarchy.FindOrCreatePath(parentPath);
                if (parent == null)
                    continue;
                var go = new GameObject(string.IsNullOrEmpty(leafName) ? "Light" : leafName);
                Undo.RegisterCreatedObjectUndo(go, "Layout Editor Light");
                go.transform.SetParent(parent, false);
                if (dto.eulerAngles != null)
                    go.transform.eulerAngles = dto.eulerAngles.ToVector3();
                light = go.AddComponent<Light>();
            }

            Undo.RecordObject(light, "Layout Editor Light");
            if (dto.lightType >= 0 && dto.lightType <= 4)
                light.type = (LightType)dto.lightType;
            Color c;
            if (!string.IsNullOrEmpty(dto.color) && ColorUtility.TryParseHtmlString(dto.color, out c))
                light.color = c;
            light.intensity = Mathf.Max(0f, dto.intensity);
            if (light.type == LightType.Spot || light.type == LightType.Point)
                light.range = Mathf.Max(0.01f, dto.range);
            if (light.type == LightType.Spot)
                light.spotAngle = Mathf.Clamp(dto.spotAngle, 1f, 179f);
            light.enabled = dto.enabled;
            EditorUtility.SetDirty(light);
        }

        RemoveUnmatchedSceneLights(keepPaths);
        return null;
    }

    /// <summary>删除 "Lights" 子树中不在文档里的非 prefab 灯光（web 侧已删除的）。</summary>
    private static void RemoveUnmatchedSceneLights(HashSet<string> keepPaths)
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid())
            return;

        foreach (var rootGo in scene.GetRootGameObjects())
        {
            for (int i = 0; i < rootGo.transform.childCount; i++)
            {
                var child = rootGo.transform.GetChild(i);
                if (child.name != "Lights")
                    continue;
                RemoveUnmatchedLightsUnder(child, keepPaths);
            }
        }
    }

    private static void RemoveUnmatchedLightsUnder(Transform root, HashSet<string> keepPaths)
    {
        // Collect first: destroying while walking the hierarchy is unsafe.
        var doomed = new List<GameObject>();
        var stack = new Stack<Transform>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var t = stack.Pop();
            for (int i = 0; i < t.childCount; i++)
                stack.Push(t.GetChild(i));

            var go = t.gameObject;
            if ((go.hideFlags & HideFlags.HideAndDontSave) != 0)
                continue;
            if (PrefabUtility.GetPrefabType(go) != PrefabType.None)
                continue;
            if (go.GetComponentInParent<LevelEditorStub.PseudoPrefabStub>() != null)
                continue;
            if (go.GetComponent<Light>() == null)
                continue;
            if (keepPaths.Contains(LayoutEditorHierarchy.GetHierarchyPath(t)))
                continue;
            doomed.Add(go);
        }

        foreach (var go in doomed)
            Undo.DestroyObjectImmediate(go);
    }

    private static string ParentPathOf(string hierarchyPath)
    {
        if (string.IsNullOrEmpty(hierarchyPath))
            return null;
        var idx = hierarchyPath.LastIndexOf('/');
        return idx < 0 ? null : hierarchyPath.Substring(0, idx);
    }

    private static string LeafNameOf(string hierarchyPath)
    {
        if (string.IsNullOrEmpty(hierarchyPath))
            return null;
        var idx = hierarchyPath.LastIndexOf('/');
        return idx < 0 ? hierarchyPath : hierarchyPath.Substring(idx + 1);
    }

    /// <summary>Replace "new:" instance ids with the real Unity ids of objects created
    /// during this apply pass.</summary>
    private static void RemapNewIds(string[] ids, Dictionary<string, GameObject> createdObjects)
    {
        if (ids == null) return;
        for (int i = 0; i < ids.Length; i++)
        {
            var id = ids[i];
            if (string.IsNullOrEmpty(id) || !id.StartsWith("new:", StringComparison.Ordinal)) continue;
            GameObject created;
            if (createdObjects.TryGetValue(id, out created) && created != null)
                ids[i] = "u:" + created.GetInstanceID();
        }
    }

    /// <summary>
    /// Regenerate the Ground-layer "Col_Floor" walkable colliders under Design/Collision so
    /// the walkable area matches the visible floor planes (gaps between floors become fall pits).
    /// </summary>
    private static void SyncWalkableToFloors(LayoutDocumentDto document, float snapStep)
    {
        var collision = LayoutEditorHierarchy.FindOrCreatePath("Design/Collision");
        if (collision == null)
            return;

        var toRemove = new List<GameObject>();
        for (int i = 0; i < collision.childCount; i++)
        {
            var c = collision.GetChild(i);
            if (c != null && IsEditorFloorCollider(c.gameObject))
                toRemove.Add(c.gameObject);
        }
        foreach (var go in toRemove)
            Undo.DestroyObjectImmediate(go);

        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer < 0)
            groundLayer = 9;

        if (document == null || document.floors == null)
            return;

        foreach (var floor in document.floors)
        {
            if (floor == null)
                continue;
            // Background planes are visual only — no walkable collider.
            if (floor.surfaceKind == "background")
                continue;
            float cx = floor.worldPosition != null ? floor.worldPosition.x : (floor.localPosition != null ? floor.localPosition.x : 0f);
            float cz = floor.worldPosition != null ? floor.worldPosition.z : (floor.localPosition != null ? floor.localPosition.z : 0f);
            // Same snap as ApplyFloorTransform so Col_Floor stays glued to the visible plane
            // (otherwise the UI shows “空气地板” strips beside shifted floors).
            cx = SnapScalar(cx, snapStep);
            cz = SnapScalar(cz, snapStep);
            float w = floor.widthUnits > 0f ? floor.widthUnits : (floor.widthCells > 0 ? floor.widthCells * LayoutEditorCatalogLookup.GridCellSize : 1.2f);
            float d = floor.depthUnits > 0f ? floor.depthUnits : (floor.depthCells > 0 ? floor.depthCells * LayoutEditorCatalogLookup.GridCellSize : 1.2f);
            CreateColFloor(collision, groundLayer, cx, cz, w, d, FloorWalkY(floor.localPosition != null ? floor.localPosition.y : 0f), floor.localRotationY,
                floor.airFloor ? SceneFloorExporter.AirFloorColliderName : "Col_Floor");
        }

        // Also make surface-floor prefab items (ice tiles, walkways, …) walkable.
        // Raft planks are walkable:false; their solid Col_Floor comes from the raft floor rect above.
        if (document.items != null)
        {
            foreach (var item in document.items)
            {
                if (item == null || !item.walkable)
                    continue;
                // 压力开关（含莲花变体）是自带碰撞的可踩踏机制，不生成隐形 Col_Floor
                // （否则 Play 期莲花底下出现一块「空气地板」）。按 prefab id 匹配，
                // 兜底处理旧文档里 walkable=true 的历史数据。
                var itemId = !string.IsNullOrEmpty(item.prefabAssetPath)
                    ? System.IO.Path.GetFileNameWithoutExtension(item.prefabAssetPath) : string.Empty;
                if (itemId.IndexOf("pressureswitch", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                float cx = item.worldPosition != null ? item.worldPosition.x : (item.localPosition != null ? item.localPosition.x : 0f);
                float cz = item.worldPosition != null ? item.worldPosition.z : (item.localPosition != null ? item.localPosition.z : 0f);
                float scx = item.localScale != null ? item.localScale.x : 1f;
                float scz = item.localScale != null ? item.localScale.z : 1f;
                if (scx <= 0f) scx = 1f;
                if (scz <= 0f) scz = 1f;
                float w = (item.footprint != null && item.footprint.cellsX > 0 ? item.footprint.cellsX : 1) * LayoutEditorCatalogLookup.GridCellSize * scx;
                float d = (item.footprint != null && item.footprint.cellsZ > 0 ? item.footprint.cellsZ : 1) * LayoutEditorCatalogLookup.GridCellSize * scz;
                CreateColFloor(collision, groundLayer, cx, cz, w, d, FloorWalkY(item.localPosition != null ? item.localPosition.y : 0f), item.localRotationY);
            }
        }
    }

    /// <summary>True for walkable colliders this editor regenerates: Col_Floor (visible
    /// floors / walkable items) and Col_AirFloor (air floors). Unity auto-suffixes
    /// duplicate sibling names with " (N)", so prefix-matching avoids stale colliders
    /// accumulating across write-backs.</summary>
    private static bool IsEditorFloorCollider(GameObject go)
    {
        if (go == null)
            return false;
        return go.name == "Col_Floor"
            || go.name == SceneFloorExporter.AirFloorColliderName
            || go.name.StartsWith("Col_Floor (", StringComparison.Ordinal)
            || go.name.StartsWith(SceneFloorExporter.AirFloorColliderName + " (", StringComparison.Ordinal);
    }

    /// <summary>Walk-surface height convention shared with the web editor: legacy
    /// default floors sit at visual y=-0.05 (plane) / 0.01 (themed) while their walk
    /// surface is 0, so small |y| values all mean "ground level". Only genuinely
    /// raised floors (y &gt; 0.05) lift the Col_Floor collider — keeping legacy
    /// scenes' colliders exactly at y=0 as before.</summary>
    private static float FloorWalkY(float y)
    {
        return y <= 0.05f ? 0f : y;
    }

    private static void CreateColFloor(Transform parent, int groundLayer, float cx, float cz, float w, float d, float y, float rotY, string colliderName = null)
    {
        var name = string.IsNullOrEmpty(colliderName) ? "Col_Floor" : colliderName;
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Layout Editor Col_Floor");
        go.layer = groundLayer;
        var t = go.transform;
        t.SetParent(parent, false);
        t.localPosition = new Vector3(cx, y, cz);
        t.localRotation = Quaternion.Euler(0f, rotY, 0f);
        t.localScale = Vector3.one;
        var col = go.AddComponent<BoxCollider>();
        col.size = new Vector3(w, 0.4f, d);
        col.center = new Vector3(0f, -0.2f, 0f);
    }

    /// <summary>空气地板没有可见 Plane，只有 Ground 层可行走碰撞盒（几何与普通
    /// Col_Floor 相同：size=(w,0.4,d)、center=(0,-0.2,0)，仅名称 Col_AirFloor 不同）。
    /// 无论 autoWalkable 开关都会创建/更新——否则空气地板没有任何作用。</summary>
    private static void ApplyAirFloorCollider(FloorDto floor, float snapStep,
        Dictionary<string, GameObject> createdObjects)
    {
        var parentPath = !string.IsNullOrEmpty(floor.parentPath) ? floor.parentPath : "Design/Collision";
        var parent = LayoutEditorHierarchy.FindOrCreatePath(parentPath);
        if (parent == null)
        {
            Debug.LogWarning("[LayoutEditor] ApplyAirFloorCollider: parent path not found \"" + parentPath + "\"");
            return;
        }

        float w = floor.widthUnits > 0f ? floor.widthUnits : (floor.widthCells > 0 ? floor.widthCells * LayoutEditorCatalogLookup.GridCellSize : 1.2f);
        float d = floor.depthUnits > 0f ? floor.depthUnits : (floor.depthCells > 0 ? floor.depthCells * LayoutEditorCatalogLookup.GridCellSize : 1.2f);

        var t = FindFloorTransform(floor);
        if (t != null)
        {
            Undo.RecordObject(t, "Layout Editor Air Floor");
            Undo.RecordObject(t.gameObject, "Layout Editor Air Floor");
            var pos = floor.localPosition != null ? floor.localPosition.ToVector3() : t.localPosition;
            pos.x = SnapScalar(pos.x, snapStep);
            pos.z = SnapScalar(pos.z, snapStep);
            t.localPosition = pos;
            t.localEulerAngles = new Vector3(0f, floor.localRotationY, 0f);
            t.localScale = Vector3.one;
            var col = t.GetComponent<BoxCollider>();
            if (col != null)
            {
                Undo.RecordObject(col, "Layout Editor Air Floor");
                col.size = new Vector3(w, 0.4f, d);
                col.center = new Vector3(0f, -0.2f, 0f);
            }
            if (!string.IsNullOrEmpty(floor.instanceId))
                createdObjects[floor.instanceId] = t.gameObject;
            return;
        }

        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer < 0)
            groundLayer = 9;

        var go = new GameObject(SceneFloorExporter.AirFloorColliderName);
        Undo.RegisterCreatedObjectUndo(go, "Layout Editor Air Floor");
        go.layer = groundLayer;
        var gt = go.transform;
        gt.SetParent(parent, false);
        var newPos = floor.localPosition != null ? floor.localPosition.ToVector3() : Vector3.zero;
        newPos.x = SnapScalar(newPos.x, snapStep);
        newPos.z = SnapScalar(newPos.z, snapStep);
        gt.localPosition = newPos;
        gt.localEulerAngles = new Vector3(0f, floor.localRotationY, 0f);
        gt.localScale = Vector3.one;
        var newCol = go.AddComponent<BoxCollider>();
        newCol.size = new Vector3(w, 0.4f, d);
        newCol.center = new Vector3(0f, -0.2f, 0f);

        if (createdObjects != null && !string.IsNullOrEmpty(floor.instanceId))
            createdObjects[floor.instanceId] = go;
    }

    private static void ApplyFloors(LayoutDocumentDto document, float snapStep,
        Dictionary<string, GameObject> createdObjects)
    {
        if (document == null)
            return;

        var docFloors = document.floors;
        if (docFloors == null)
            return;

        var before = SceneFloorExporter.ExportFromScene();
        RemoveUnmatchedFloors(before, docFloors);

        var usedIds = new HashSet<int>();
        foreach (var floor in docFloors)
        {
            if (floor == null)
                continue;
            // 空气地板：无可见 Plane，只维护可行走碰撞盒（几何同普通 Col_Floor，仅名称
            // 不同）。此处无条件创建/更新——否则关闭 autoWalkable 时空气地板没有碰撞盒
            // 就毫无作用；开启时随后由 SyncWalkableToFloors 重建，终态一致。
            if (floor.airFloor)
            {
                ApplyAirFloorCollider(floor, snapStep, createdObjects);
                continue;
            }
            // Raft floors are visualized via planks in items[]; keep the floor
            // entry only so SyncWalkableToFloors can emit one Col_Floor for the rect.
            if (floor.surfaceKind == "raft")
                continue;
            // Themed floors (carpet / ice / snow / …) are likewise visualized via
            // tiled prefab tiles in items[] — no Plane mesh is created for them.
            if (!string.IsNullOrEmpty(floor.prefabGuid))
            {
                // Converting solid -> themed: actively remove the old Plane so it
                // never lingers next to the prefab visualization (belt & braces —
                // RemoveUnmatchedFloors above already covers the common path).
                if (floor.instanceId == null || !floor.instanceId.StartsWith("new:", StringComparison.Ordinal))
                {
                    var oldPlane = FindFloorTransform(floor);
                    if (oldPlane != null)
                        Undo.DestroyObjectImmediate(oldPlane.gameObject);
                }
                continue;
            }
            if (string.IsNullOrEmpty(floor.hierarchyPath) && string.IsNullOrEmpty(floor.instanceId))
                continue;

            if (floor.instanceId != null && floor.instanceId.StartsWith("new:", StringComparison.Ordinal))
            {
                CreateFloorInstance(floor, snapStep, createdObjects);
                continue;
            }

            var t = FindFloorTransform(floor);
            if (t != null)
            {
                var id = t.gameObject.GetInstanceID();
                if (usedIds.Add(id))
                {
                    ApplyFloorTransform(t, floor, snapStep);
                    continue;
                }
            }

            CreateFloorInstance(floor, snapStep, createdObjects);
        }
    }

    private static void ApplyFloorTransform(Transform t, FloorDto floor, float snapStep)
    {
        Undo.RecordObject(t, "Layout Editor Floor");
        Undo.RecordObject(t.gameObject, "Layout Editor Floor");
        var pos = floor.localPosition != null ? floor.localPosition.ToVector3() : t.localPosition;
        // Match web finalizeFloor — snap with the web-sent precision (lossless for
        // half-cell aligned floor centers).
        pos.x = SnapScalar(pos.x, snapStep);
        pos.z = SnapScalar(pos.z, snapStep);
        t.localPosition = pos;
        t.localEulerAngles = new Vector3(t.localEulerAngles.x, floor.localRotationY, t.localEulerAngles.z);

        var scale = FloorScaleFromCells(floor, t.localScale.y);
        t.localScale = scale;

        // Authoritatively encode the surface kind into the GameObject name so a
        // type-corrected floor (ice/sand/...) survives save/reload — otherwise
        // InferSurfaceKind can only guess from the material and reverts to solid.
        t.gameObject.name = FloorGameObjectName(floor);

        ApplyFloorMaterial(t, floor);
    }

    /** GameObject name for a floor, encoding an optional tint color suffix so
     *  the tint survives save/reload. Image floors encode their texture path +
     *  mode + opacity (+ rotation when non-zero) so the state round-trips. */
    public const string ImageFloorPrefix = "imgfloor|";

    private static string FloorGameObjectName(FloorDto floor)
    {
        // Image floor: encode texture path (with '|' for '/') + mode + opacity
        // (+ rotation as a trailing field when non-zero).
        if (floor != null && !string.IsNullOrEmpty(floor.imageTexturePath))
        {
            var mode = string.IsNullOrEmpty(floor.imageMode) ? "stretch" : floor.imageMode;
            var op = floor.imageOpacity > 0f ? floor.imageOpacity : 1f;
            var name = ImageFloorPrefix
                + floor.imageTexturePath.Replace('/', '|') + "|" + mode + "|"
                + op.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            var rot = NormalizeImageRotation(floor.imageRotation);
            if (rot != 0) name += "|" + rot;
            return name;
        }

        var baseName = !string.IsNullOrEmpty(floor.displayName) ? floor.displayName : "Floor";
        // Strip a stale tint marker from the incoming display name first.
        var hash = baseName.IndexOf('#');
        if (hash >= 0) baseName = baseName.Substring(0, hash);
        if (string.IsNullOrEmpty(baseName)) baseName = "Floor";
        // Append the manual tint color as "#rrggbb" so the tint round-trips —
        // but only while enabled (disabled tint must not persist to the name).
        if (floor != null && floor.tintEnabled && !string.IsNullOrEmpty(floor.tintColor))
            baseName += "#" + floor.tintColor.TrimStart('#');
        return baseName;
    }

    /** Snap an image rotation to the supported 90° steps (0/90/180/270). */
    public static int NormalizeImageRotation(int rotation)
    {
        var r = ((rotation % 360) + 360) % 360;
        return ((r + 45) / 90 * 90) % 360;
    }

    /** Parse an image-floor name back into (texturePath, mode, opacity, rotation).
     *  The name shape is: imgfloor|<path with | for />|<mode>|<opacity>[|<rotation>]
     *  — opacity and rotation are optional trailing numeric fields; a single
     *  trailing number is opacity, two trailing numbers are opacity + rotation. */
    public static bool TryParseImageFloorName(string name, out string texturePath, out string mode, out float opacity, out int rotation)
    {
        texturePath = null;
        mode = null;
        opacity = 1f;
        rotation = 0;
        if (string.IsNullOrEmpty(name) || !name.StartsWith(ImageFloorPrefix, StringComparison.Ordinal))
            return false;
        var rest = name.Substring(ImageFloorPrefix.Length);
        var parts = rest.Split('|');
        if (parts.Length < 2) return false;

        int last = parts.Length - 1;
        float parsedLast = 0f, parsedPrev = 0f;
        bool lastIsNum = float.TryParse(parts[last], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out parsedLast);
        bool prevIsNum = last >= 1 && float.TryParse(parts[last - 1], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out parsedPrev);

        int modeIndex;
        if (lastIsNum && prevIsNum && parts.Length >= 4)
        {
            rotation = NormalizeImageRotation(Mathf.RoundToInt(parsedLast));
            opacity = Mathf.Clamp01(parsedPrev);
            modeIndex = last - 2;
        }
        else if (lastIsNum)
        {
            opacity = Mathf.Clamp01(parsedLast);
            modeIndex = last - 1;
        }
        else
        {
            modeIndex = last;
        }
        if (modeIndex < 1) return false;
        mode = parts[modeIndex];
        texturePath = string.Join("/", parts, 0, modeIndex);
        return true;
    }

    private static Vector3 FloorScaleFromCells(FloorDto floor, float preserveY)
    {
        var cell = LayoutEditorCatalogLookup.GridCellSize;
        int wcx = floor.widthCells > 0 ? floor.widthCells : Mathf.RoundToInt(floor.widthUnits / cell);
        int dcz = floor.depthCells > 0 ? floor.depthCells : Mathf.RoundToInt(floor.depthUnits / cell);
        if (wcx <= 0) wcx = 1;
        if (dcz <= 0) dcz = 1;

        float wU = wcx * cell;
        float dU = dcz * cell;

        if (floor.meshType == "quad")
            return new Vector3(wU, dU, preserveY > 0f ? preserveY : 1f);

        // plane: base 10x10
        return new Vector3(wU / 10f, preserveY > 0f ? preserveY : 1f, dU / 10f);
    }

    private static void ApplyFloorMaterial(Transform t, FloorDto floor)
    {
        var mr = t.GetComponent<MeshRenderer>();
        if (mr == null)
            return;

        // Image floor: a shared material with the uploaded texture + per-floor
        // tiling via MaterialPropertyBlock (tile = repeat per cell, stretch = fill).
        // The V axis is flipped (negative tiling + offset) because Unity's Plane UVs
        // render the texture upside-down when viewed from above.
        if (!string.IsNullOrEmpty(floor.imageTexturePath))
        {
            var texMat = ImageFloorMaterial(floor.imageTexturePath, NormalizeImageRotation(floor.imageRotation));
            if (texMat != null)
            {
                Undo.RecordObject(mr, "Layout Editor Floor Material");
                mr.sharedMaterial = texMat;
                float tileX = 1f, tileY = 1f;
                if (floor.imageMode == "tile")
                {
                    tileX = floor.widthCells > 0 ? floor.widthCells : 1;
                    tileY = floor.depthCells > 0 ? floor.depthCells : 1;
                }
                float opacity = floor.imageOpacity > 0f ? Mathf.Clamp01(floor.imageOpacity) : 1f;
                var block = new MaterialPropertyBlock();
                // Unity's Plane UVs mirror the texture horizontally; flip U (negative
                // tiling + compensating offset) so the image renders upright/correct.
                block.SetVector("_MainTex_ST", new Vector4(-tileX, tileY, tileX, 0f));
                // Per-floor opacity via the material color alpha (shader must be in a
                // transparent/fade mode — set on the shared material in ImageFloorMaterial).
                block.SetVector("_Color", new Vector4(1f, 1f, 1f, opacity));
                block.SetVector("_BaseColor", new Vector4(1f, 1f, 1f, opacity));
                mr.SetPropertyBlock(block);
                return;
            }
        }

        // Non-image floors never use a texture-tiling block unless the size-tag
        // scaler below applies one — clear any stale block left from a previous
        // image-floor state.
        mr.SetPropertyBlock(null);

        // Manual tint feature: only when explicitly enabled does an explicit
        // tintColor recolor the floor (flat color). Disabled tint keeps the real
        // material, so you can switch materials without the tint overriding.
        Color tint;
        if (floor.tintEnabled && !string.IsNullOrEmpty(floor.tintColor) && ColorUtility.TryParseHtmlString(floor.tintColor, out tint))
        {
            Undo.RecordObject(mr, "Layout Editor Floor Material");
            mr.sharedMaterial = ColoredMaterial(tint);
            return;
        }

        Material mat = null;
        if (!string.IsNullOrEmpty(floor.materialGuid))
        {
            var path = AssetDatabase.GUIDToAssetPath(floor.materialGuid);
            if (!string.IsNullOrEmpty(path))
                mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        }
        else if (!string.IsNullOrEmpty(floor.materialAssetPath))
        {
            mat = AssetDatabase.LoadAssetAtPath<Material>(floor.materialAssetPath);
        }

        if (mat != null)
        {
            Undo.RecordObject(mr, "Layout Editor Floor Material");
            mr.sharedMaterial = mat;
            ApplyFloorTiling(mr, mat, floor);
        }
    }

    private static readonly System.Text.RegularExpressions.Regex FloorSizeTagRegex =
        new System.Text.RegularExpressions.Regex(@"_([0-9]+)x([0-9]+)(?:_|$)");

    /// <summary>材质 id 中的尺寸标签（mat_kevin_floor_12x8 → 12×8），
    ///  与 LayoutEditorFloorMaterialsApi.SizeTagOf 同一正则。</summary>
    private static bool TryParseFloorSizeTag(string materialName, out int w, out int h)
    {
        w = 0;
        h = 0;
        var m = FloorSizeTagRegex.Match(materialName ?? "");
        if (!m.Success)
            return false;
        return int.TryParse(m.Groups[1].Value, out w)
            && int.TryParse(m.Groups[2].Value, out h)
            && w > 0 && h > 0;
    }

    /// <summary>格子地板改宽/改深后按比例重算材质 tiling：尺寸材质（mat_*_WxH）
    ///  的 _MainTex/_BumpMap ST 是按 sizeTag 格数烘焙的，Plane 缩放后不改 ST 纹理
    ///  会被拉伸。按 bakedST × (当前格数 / sizeTag 格数) 写 MaterialPropertyBlock
    ///  （与 image floor 同机制，保留原 offset），维持作者意图的每格贴图密度；
    ///  无 sizeTag 或格数恰好一致时不写块（材质烘焙值即正确）。</summary>
    private static void ApplyFloorTiling(MeshRenderer mr, Material mat, FloorDto floor)
    {
        int tagW, tagH;
        if (!TryParseFloorSizeTag(mat.name, out tagW, out tagH))
            return;
        if (floor == null || floor.widthCells <= 0 || floor.depthCells <= 0)
            return;
        if (floor.widthCells == tagW && floor.depthCells == tagH)
            return;

        var block = new MaterialPropertyBlock();
        if (mat.HasProperty("_MainTex"))
        {
            var scale = mat.GetTextureScale("_MainTex");
            var offset = mat.GetTextureOffset("_MainTex");
            block.SetVector("_MainTex_ST", new Vector4(
                scale.x * floor.widthCells / tagW,
                scale.y * floor.depthCells / tagH,
                offset.x, offset.y));
        }
        if (mat.HasProperty("_BumpMap"))
        {
            var scale = mat.GetTextureScale("_BumpMap");
            var offset = mat.GetTextureOffset("_BumpMap");
            block.SetVector("_BumpMap_ST", new Vector4(
                scale.x * floor.widthCells / tagW,
                scale.y * floor.depthCells / tagH,
                offset.x, offset.y));
        }
        mr.SetPropertyBlock(block);
    }

    private static readonly System.Collections.Generic.Dictionary<string, Material> _imageFloorMats =
        new System.Collections.Generic.Dictionary<string, Material>();

    /** Shared runtime material for an image floor (one per uploaded texture +
     *  rotation), with the texture assigned. Per-floor tiling is set on each
     *  renderer via a MaterialPropertyBlock, so one shared material serves
     *  floors of any size. 90°-step rotations are baked into a rotated texture
     *  asset (_MainTex_ST cannot express a 90° rotation). */
    private static Material ImageFloorMaterial(string texturePath, int rotation)
    {
        // In-game the plane renders the texture rotated 180° relative to the
        // editor preview, so bake a 180° compensation into every rotation.
        rotation = NormalizeImageRotation(rotation + 180);
        var key = texturePath + "|rot" + rotation;
        Material m;
        if (_imageFloorMats.TryGetValue(key, out m) && m != null)
            return m;
        var tex = rotation == 0
            ? AssetDatabase.LoadAssetAtPath<Texture>(texturePath)
            : RotatedImageTexture(texturePath, rotation);
        if (tex == null) return null;
        var shader = Shader.Find("Standard")
            ?? Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("UI/Default");
        m = new Material(shader);
        m.mainTexture = tex;
        m.name = "img_floor_" + System.IO.Path.GetFileNameWithoutExtension(texturePath)
            + (rotation != 0 ? "_rot" + rotation : "");
        // Put the material in a transparent render mode so the per-floor opacity
        // (set via MaterialPropertyBlock _Color/_BaseColor alpha) is respected.
        SetupTransparentMode(m);
        _imageFloorMats[key] = m;
        return m;
    }

    /** Asset path of the rotated copy of an uploaded floor texture. */
    private static string RotatedTexturePath(string texturePath, int rotation)
    {
        var dir = System.IO.Path.GetDirectoryName(texturePath);
        dir = string.IsNullOrEmpty(dir) ? "" : dir.Replace('\\', '/') + "/";
        var stem = System.IO.Path.GetFileNameWithoutExtension(texturePath);
        return dir + stem + "_rot" + rotation + ".png";
    }

    /** Load-or-create a clockwise-rotated (90° steps) copy of an uploaded floor
     *  texture as a real asset next to the source, so scene-saved materials keep
     *  a persistent texture reference. Decodes from file bytes, so the source
     *  import settings (read/write) don't matter. */
    private static Texture RotatedImageTexture(string texturePath, int rotation)
    {
        var rotPath = RotatedTexturePath(texturePath, rotation);
        var existing = AssetDatabase.LoadAssetAtPath<Texture>(rotPath);
        if (existing != null) return existing;

        try
        {
            var bytes = System.IO.File.ReadAllBytes(System.IO.Path.GetFullPath(texturePath));
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!src.LoadImage(bytes))
            {
                UnityEngine.Object.DestroyImmediate(src);
                return null;
            }
            var dst = RotateTextureClockwise(src, rotation);
            UnityEngine.Object.DestroyImmediate(src);
            if (dst == null) return null;
            var png = dst.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(dst);
            System.IO.File.WriteAllBytes(System.IO.Path.GetFullPath(rotPath), png);
            AssetDatabase.ImportAsset(rotPath);
            return AssetDatabase.LoadAssetAtPath<Texture>(rotPath);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[LayoutEditor] Failed to rotate image floor texture: " + e.Message);
            return null;
        }
    }

    /** Rotate a texture clockwise by 90/180/270 degrees (viewed upright). */
    private static Texture2D RotateTextureClockwise(Texture2D src, int rotation)
    {
        var pix = src.GetPixels32();
        int w = src.width, h = src.height;
        Texture2D dst;
        Color32[] outp;
        if (rotation == 180)
        {
            dst = new Texture2D(w, h, TextureFormat.RGBA32, false);
            outp = new Color32[pix.Length];
            for (int i = 0; i < pix.Length; i++)
                outp[i] = pix[pix.Length - 1 - i];
        }
        else if (rotation == 90 || rotation == 270)
        {
            dst = new Texture2D(h, w, TextureFormat.RGBA32, false);
            outp = new Color32[pix.Length];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // Pixel rows are bottom-up. 90 CW (upright): x' = y, y' = w-1-x.
                    // 270 CW: x' = h-1-y, y' = x.
                    int di = rotation == 90
                        ? (w - 1 - x) * h + y
                        : x * h + (h - 1 - y);
                    outp[di] = pix[y * w + x];
                }
            }
        }
        else
        {
            return null;
        }
        dst.SetPixels32(outp);
        dst.Apply();
        return dst;
    }

    /** Switch a Standard (or URP/Lit) material into Fade/Transparent mode so that
     *  the color alpha controls opacity. No-op for unknown shaders. */
    private static void SetupTransparentMode(Material m)
    {
        if (m == null) return;
        // Built-in Standard shader.
        if (m.HasProperty("_Mode"))
        {
            m.SetFloat("_Mode", 2f); // Fade
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.renderQueue = 3000;
            return;
        }
        // Universal RP / Lit: toggle the _Surface property + surface options keyword.
        if (m.HasProperty("_Surface"))
        {
            m.SetFloat("_Surface", 1f); // 1 = Transparent
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.renderQueue = 3000;
        }
    }

    private static readonly System.Collections.Generic.Dictionary<string, Material> _tintedFloorMats =
        new System.Collections.Generic.Dictionary<string, Material>();

    /** A shared runtime flat-color material for the manual floor-tint feature,
     *  cached by hex color so all floors of the same tint share one instance. */
    private static Material ColoredMaterial(Color c)
    {
        var key = ColorUtility.ToHtmlStringRGBA(c);
        Material m;
        if (!_tintedFloorMats.TryGetValue(key, out m) || m == null)
        {
            var shader = Shader.Find("Standard")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("UI/Default");
            m = new Material(shader);
            m.color = c;
            m.name = "floor_tint_" + key;
            _tintedFloorMats[key] = m;
        }
        return m;
    }

    private static void CreateFloorInstance(FloorDto floor, float snapStep,
        Dictionary<string, GameObject> createdObjects)
    {
        if (floor != null && floor.surfaceKind == "raft")
            return;

        var parentPath = !string.IsNullOrEmpty(floor.parentPath) ? floor.parentPath : "Art/Ground";
        var parent = LayoutEditorHierarchy.FindOrCreatePath(parentPath);
        if (parent == null)
            parent = LayoutEditorHierarchy.FindOrCreatePath("Art");

        var isQuad = floor.meshType == "quad";
        var primitive = GameObject.CreatePrimitive(isQuad ? PrimitiveType.Quad : PrimitiveType.Plane);
        // Remove the default collider - floor collision is handled by Col_Floor.
        var collider = primitive.GetComponent<Collider>();
        if (collider != null)
            UnityEngine.Object.DestroyImmediate(collider);

        Undo.RegisterCreatedObjectUndo(primitive, "Layout Editor Floor Create");
        primitive.transform.SetParent(parent, false);
        primitive.name = FloorGameObjectName(floor);

        var pos = floor.localPosition != null ? floor.localPosition.ToVector3() : Vector3.zero;
        pos.x = SnapScalar(pos.x, snapStep);
        pos.z = SnapScalar(pos.z, snapStep);
        if (Mathf.Abs(pos.y) < 0.0001f)
            pos.y = -0.05f;
        primitive.transform.localPosition = pos;
        primitive.transform.localEulerAngles = new Vector3(0f, floor.localRotationY, 0f);
        primitive.transform.localScale = FloorScaleFromCells(floor, 1f);

        ApplyFloorMaterial(primitive.transform, floor);

        if (createdObjects != null && floor != null && !string.IsNullOrEmpty(floor.instanceId))
            createdObjects[floor.instanceId] = primitive;
    }

    private static Transform FindFloorTransform(FloorDto floor)
    {
        if (floor == null)
            return null;

        if (!string.IsNullOrEmpty(floor.instanceId) && floor.instanceId.StartsWith("u:", StringComparison.Ordinal))
        {
            int id;
            if (int.TryParse(floor.instanceId.Substring(2), out id))
            {
                var obj = EditorUtility.InstanceIDToObject(id) as GameObject;
                if (obj != null)
                    return obj.transform;
            }
        }

        var path = !string.IsNullOrEmpty(floor.hierarchyPath) ? floor.hierarchyPath : floor.instanceId;
        if (string.IsNullOrEmpty(path))
            return null;
        return LayoutEditorHierarchy.FindByPath(path);
    }

    private static void RemoveUnmatchedFloors(List<FloorDto> before, FloorDto[] docFloors)
    {
        var unmatched = new List<FloorDto>(before);
        if (docFloors == null)
            docFloors = new FloorDto[0];

        foreach (var doc in docFloors)
        {
            if (doc == null)
                continue;
            if (doc.instanceId != null && doc.instanceId.StartsWith("new:", StringComparison.Ordinal))
                continue;
            // Raft floors carry no Plane mesh (visualized via planks in items[]),
            // so a raft floor must NOT rescue a pre-existing solid Plane — otherwise
            // converting solid -> raft leaves the old Plane behind as a stray floor.
            if (doc.surfaceKind == "raft")
                continue;
            // Themed floors are also visualized via tiled prefabs (no Plane) — same
            // reasoning when converting solid -> themed.
            if (!string.IsNullOrEmpty(doc.prefabGuid))
                continue;

            for (int i = 0; i < unmatched.Count; i++)
            {
                if (FloorMatches(unmatched[i], doc))
                {
                    unmatched.RemoveAt(i);
                    break;
                }
            }
        }

        for (int i = 0; i < unmatched.Count; i++)
        {
            var t = FindFloorTransform(unmatched[i]);
            if (t != null)
                Undo.DestroyObjectImmediate(t.gameObject);
        }
    }

    private static bool FloorMatches(FloorDto scene, FloorDto doc)
    {
        if (scene == null || doc == null)
            return false;
        if (!string.IsNullOrEmpty(doc.instanceId) && doc.instanceId.StartsWith("u:", StringComparison.Ordinal))
            return scene.instanceId == doc.instanceId;
        var docPath = !string.IsNullOrEmpty(doc.hierarchyPath) ? doc.hierarchyPath : doc.instanceId;
        return !string.IsNullOrEmpty(docPath) && scene.hierarchyPath == docPath;
    }

    private static void RemoveUnmatchedSceneItems(List<LayoutItemDto> before, LayoutItemDto[] documentItems, string only = null)
    {
        var unmatched = new List<LayoutItemDto>(before);
        if (documentItems == null)
            documentItems = new LayoutItemDto[0];

        foreach (var doc in documentItems)
        {
            if (doc == null)
                continue;
            if (doc.instanceId != null && doc.instanceId.StartsWith("new:", StringComparison.Ordinal))
                continue;

            var matchedIndex = -1;
            for (int i = 0; i < unmatched.Count; i++)
            {
                if (SceneItemMatchesDocumentItem(unmatched[i], doc))
                {
                    matchedIndex = i;
                    break;
                }
            }

            if (matchedIndex >= 0)
                unmatched.RemoveAt(matchedIndex);
        }

        var deletedCount = 0;
        var skippedCount = 0;
        for (int i = 0; i < unmatched.Count; i++)
        {
            if (only == "items" && !IsGameplayItem(unmatched[i]))
            {
                skippedCount++;
                continue;
            }
            if (only == "decor" && !IsDecorSceneItem(unmatched[i]))
            {
                skippedCount++;
                continue;
            }
            if (only == "floors" && !IsSurfaceLikeSceneItem(unmatched[i]))
            {
                skippedCount++;
                continue;
            }
            DeleteItem(unmatched[i]);
            deletedCount++;
        }

        if (deletedCount > 0 || skippedCount > 0)
            Debug.Log("[LayoutEditor] RemoveUnmatchedSceneItems: deleted " + deletedCount
                + " unmatched items" + (skippedCount > 0 ? ", skipped " + skippedCount + " non-gameplay items" : "")
                + " (total before: " + before.Count + ", doc: " + documentItems.Length + ")");
    }

    private static bool IsGameplayItem(LayoutItemDto item)
    {
        if (item == null)
            return false;
        var path = item.hierarchyPath ?? "";
        return path.StartsWith("Design/", StringComparison.Ordinal);
    }

    /// <summary>
    /// Decor-scope deletion candidate: a prefab item under Art/ that is not a
    /// collision stub and not a surface item (themed floor tiles / background
    /// prefabs / travelators belong to the floor layer and must survive a
    /// decor-only write-back).
    /// </summary>
    private static bool IsDecorSceneItem(LayoutItemDto item)
    {
        if (item == null)
            return false;
        var path = item.hierarchyPath ?? "";
        if (!path.StartsWith("Art/", StringComparison.Ordinal))
            return false;
        if (item.stubKind == "Collision")
            return false;
        return !IsSurfaceLikeSceneItem(item);
    }

    /// <summary>
    /// Surface (floor-layer) prefab item, mirroring build-catalog.mjs surfaceMeta:
    /// themed floor tiles, background/environment prefabs, travelators.
    /// </summary>
    private static bool IsSurfaceLikeSceneItem(LayoutItemDto item)
    {
        if (item == null)
            return false;
        var id = "";
        if (!string.IsNullOrEmpty(item.prefabAssetPath))
            id = System.IO.Path.GetFileNameWithoutExtension(item.prefabAssetPath);
        if (string.IsNullOrEmpty(id))
            id = item.displayName ?? "";
        if (string.IsNullOrEmpty(id))
            return false;

        var n = id.ToLowerInvariant();
        if (n == "sky" || n.Contains("background"))
            return true;
        if (id == "raft_water" || id == "alien_gue" || id == "sand_01")
            return true;
        if (id == "p_dlc5_camp_water" || id == "p_dlc5_camp_river")
            return true;
        if (n.StartsWith("raft_raft_", StringComparison.Ordinal))
            return true;
        if (n.Contains("floor") || n.Contains("carpet") || n.Contains("blacktiles") || n.Contains("walkway"))
            return true;
        if (id == "Travelator")
            return true;
        return false;
    }

    private static bool SceneItemMatchesDocumentItem(LayoutItemDto scene, LayoutItemDto doc)
    {
        if (scene == null || doc == null)
            return false;

        if (!string.IsNullOrEmpty(doc.instanceId) && doc.instanceId.StartsWith("u:", StringComparison.Ordinal))
            return scene.instanceId == doc.instanceId;

        var docPath = !string.IsNullOrEmpty(doc.hierarchyPath) ? doc.hierarchyPath : doc.instanceId;
        if (!string.IsNullOrEmpty(docPath) && scene.hierarchyPath == docPath)
            return true;

        return !string.IsNullOrEmpty(doc.instanceId) && scene.instanceId == doc.instanceId;
    }

    private static void ApplyCollisionItem(LayoutItemDto item, float snapStep, HashSet<int> usedSceneObjectIds, Dictionary<string, GameObject> createdObjects)
    {
        var itemSnap = SnapStepForItem(item, snapStep);
        if (item.instanceId != null && item.instanceId.StartsWith("new:", StringComparison.Ordinal))
        {
            var parentPath = !string.IsNullOrEmpty(item.parentPath)
                ? item.parentPath
                : "Design/Collision";
            var parent = LayoutEditorHierarchy.FindOrCreatePath(parentPath);
            if (parent == null)
            {
                Debug.LogWarning("[LayoutEditor] ApplyCollisionItem: parent path not found \"" + parentPath + "\" for " + (item.displayName ?? "?"));
                return;
            }

            var go = new GameObject(item.displayName ?? "Collision");
            Undo.RegisterCreatedObjectUndo(go, "Layout Editor Create Collision");
            go.transform.SetParent(parent, false);
            if (item.worldPosition != null)
            {
                var wp = item.worldPosition.ToVector3();
                wp.x = SnapScalar(wp.x, itemSnap);
                wp.z = SnapScalar(wp.z, itemSnap);
                go.transform.position = wp;
            }
            else
            {
                var pos = item.localPosition != null ? item.localPosition.ToVector3() : Vector3.zero;
                pos.x = SnapScalar(pos.x, itemSnap);
                pos.z = SnapScalar(pos.z, itemSnap);
                go.transform.localPosition = pos;
            }
            go.transform.localEulerAngles = new Vector3(
                item.localRotationX != 0f ? item.localRotationX : 0f,
                item.localRotationY,
                item.localRotationZ != 0f ? item.localRotationZ : 0f);
            if (item.localScale != null)
            {
                var sc = item.localScale.ToVector3();
                if (sc.x > 0f && sc.y > 0f && sc.z > 0f)
                    go.transform.localScale = sc;
            }
            var col = go.AddComponent<BoxCollider>();
            // 空气墙：1×1 占地、高 1.132（魔法数，导出时据此识别，避免与其他碰撞混淆）
            col.size = item.airWall
                ? new Vector3(1f, 1.132f, 1f)
                : new Vector3(1.2f, 2f, 1.2f);
            if (item.colliderCenter != null)
                col.center = item.colliderCenter.ToVector3();
            if (!string.IsNullOrEmpty(item.instanceId))
                createdObjects[item.instanceId] = go;
            return;
        }

        var t = FindItemTransform(item);
        if (t != null)
        {
            var objectId = t.gameObject.GetInstanceID();
            if (usedSceneObjectIds.Contains(objectId))
                return;
            usedSceneObjectIds.Add(objectId);
            Undo.RecordObject(t, "Layout Editor Move");
            var pos = item.localPosition != null ? item.localPosition.ToVector3() : t.localPosition;
            pos.x = SnapScalar(pos.x, itemSnap);
            pos.z = SnapScalar(pos.z, itemSnap);
            t.localPosition = pos;
            t.localEulerAngles = new Vector3(
                item.localRotationX != 0f ? item.localRotationX : t.localEulerAngles.x,
                item.localRotationY,
                item.localRotationZ != 0f ? item.localRotationZ : t.localEulerAngles.z);
            ApplyItemScale(t, item);
            // 修复空气墙碰撞几何：确保 size 为魔法数 1×1×1.132，center 与文档一致。
            if (item.airWall)
            {
                var col = t.GetComponent<BoxCollider>();
                if (col != null)
                {
                    Undo.RecordObject(col, "Layout Editor Collision");
                    col.size = new Vector3(1f, 1.132f, 1f);
                    if (item.colliderCenter != null)
                        col.center = item.colliderCenter.ToVector3();
                }
            }
        }
    }

    /// <summary>
    /// Snap step used when writing positions back. The web client sends the selected
    /// placement precision (0.1 / 0.01 / 0.001) as the `snap` parameter; every web-side
    /// position — grid-snapped (half-cell 0.6 lattice) or free-placed — is a multiple of
    /// that precision, so snapping with it is lossless for all items.
    /// </summary>
    private static float SnapStepForItem(LayoutItemDto item, float snapStep)
    {
        return snapStep;
    }

    private static GameObject CreateInstance(LayoutItemDto item, GameObject prefab, string assetPath, Vector3 pos, float rotY, Vector3? worldPos = null)
    {
        var parentPath = item.parentPath;
        if (string.IsNullOrEmpty(parentPath))
            parentPath = LayoutEditorCatalogLookup.DefaultParentForAssetPath(assetPath);

        var parent = LayoutEditorHierarchy.FindOrCreatePath(parentPath);
        if (parent == null)
        {
            Debug.LogWarning("[LayoutEditor] CreateInstance: parent path not found \"" + parentPath
                + "\" for " + assetPath + " (displayName: " + (item.displayName ?? "?") + ")");
            return null;
        }

        var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
        {
            Debug.LogWarning("[LayoutEditor] CreateInstance: InstantiatePrefab returned null for "
                + assetPath + " (displayName: " + (item.displayName ?? "?") + ")");
            return null;
        }

        Undo.RegisterCreatedObjectUndo(instance, "Layout Editor Create");
        instance.transform.SetParent(parent, false);
        if (worldPos.HasValue)
            instance.transform.position = worldPos.Value;
        else
            instance.transform.localPosition = pos;
        // Preserve the doc's euler X (quad floor tiles lie flat via x=90) instead
        // of forcing identity — otherwise recreated tiles render standing up.
        instance.transform.localEulerAngles = new Vector3(item.localRotationX, rotY, 0f);
        ApplyItemScale(instance.transform, item);

        if (!string.IsNullOrEmpty(item.displayName))
            instance.name = item.displayName;

        LayoutEditorStubIO.ApplyStub(instance, item);
        return instance;
    }

    private static void ApplyItemScale(Transform t, LayoutItemDto item)
    {
        if (item == null || item.localScale == null)
            return;
        var s = item.localScale.ToVector3();
        if (s.x <= 0f) s.x = t.localScale.x;
        if (s.y <= 0f) s.y = t.localScale.y;
        if (s.z <= 0f) s.z = t.localScale.z;
        if (Mathf.Abs(s.x - 1f) > 0.001f || Mathf.Abs(s.y - 1f) > 0.001f || Mathf.Abs(s.z - 1f) > 0.001f)
            t.localScale = s;
    }

    private static Transform FindItemTransform(LayoutItemDto item)
    {
        if (item == null)
            return null;

        if (!string.IsNullOrEmpty(item.instanceId) && item.instanceId.StartsWith("u:", StringComparison.Ordinal))
        {
            int id;
            if (int.TryParse(item.instanceId.Substring(2), out id))
            {
                var obj = EditorUtility.InstanceIDToObject(id) as GameObject;
                if (obj != null)
                    return obj.transform;
            }
        }

        var path = !string.IsNullOrEmpty(item.hierarchyPath) ? item.hierarchyPath : item.instanceId;
        if (string.IsNullOrEmpty(path))
            return null;
        return LayoutEditorHierarchy.FindByPath(path);
    }

    private static void DeleteItem(LayoutItemDto item)
    {
        var t = FindItemTransform(item);
        if (t == null)
            return;
        Undo.DestroyObjectImmediate(t.gameObject);
    }

    private static bool IsThemeBackgroundPrefab(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return false;
        var id = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        for (int i = 0; i < ThemeBackgroundPrefabNames.Length; i++)
        {
            if (ThemeBackgroundPrefabNames[i] == id)
                return true;
        }
        return false;
    }

    /// <summary>Keep at most one theme-managed environment background in the layout document.</summary>
    private static LayoutItemDto[] PruneThemeBackgroundItems(LayoutItemDto[] items)
    {
        if (items == null || items.Length == 0)
            return items ?? new LayoutItemDto[0];

        int lastBg = -1;
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null && IsThemeBackgroundPrefab(items[i].prefabAssetPath))
                lastBg = i;
        }

        if (lastBg < 0)
            return items;

        var result = new List<LayoutItemDto>(items.Length);
        for (int i = 0; i < items.Length; i++)
        {
            var it = items[i];
            if (it == null)
                continue;
            if (IsThemeBackgroundPrefab(it.prefabAssetPath) && i != lastBg)
                continue;
            result.Add(it);
        }
        return result.ToArray();
    }

    private static Vector3 SnapVector(Vector3 v, float step)
    {
        if (step <= 0f)
            return v;
        return new Vector3(
            SnapScalar(v.x, step),
            v.y,
            SnapScalar(v.z, step));
    }

    private static float SnapScalar(float v, float step)
    {
        if (step <= 0f)
            return v;
        return Mathf.Round(v / step) * step;
    }
}

public static class LayoutEditorSafety
{
    public static string LastError { get; private set; }

    public static bool PrepareSceneForApply()
    {
        LastError = null;
        LayoutEditorPseudoReload.EnsurePrepareForBuilding();
        return true;
    }

    public static bool CheckCanApply()
    {
        LastError = null;
        var scene = SceneManager.GetActiveScene();
        var manager = scene.GetRootGameObjects()
            .Select(root => root.GetComponent<PseudoPrefabManager>())
            .FirstOrDefault(comp => comp != null);

        if (manager == null)
            manager = PseudoPrefabManager.Instance;

        if (manager != null && !manager.prepareForBuilding)
        {
            LastError =
                "请先点击 Tools - Toggle Prepare For Building 清除临时物体后再写回场景。";
            return false;
        }

        return true;
    }
}
