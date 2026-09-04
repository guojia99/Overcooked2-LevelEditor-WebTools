using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneLayoutExporter
{
    /// <summary>空气墙在 web catalog 中的合成 guid（与 build-catalog.mjs 一致）。</summary>
    public const string AirWallCatalogGuid = "0d0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a";

    private static readonly string[] ExportRootNames = { "Design", "Art", "Chefs" };
    private static readonly HashSet<string> SkippedSubtrees = new HashSet<string>
    {
        "Design/Collision",
    };

    public static LayoutDocumentDto ExportActiveScene()
    {
        var scene = SceneManager.GetActiveScene();
        var items = ExportFromScene();

        // The scene itself is the single source of truth for move groups: rebuild
        // them directly from the animated objects (Animator + TriggerQueue/Timer +
        // controller/clips). No external JSON config is read.
        var sceneName = System.IO.Path.GetFileNameWithoutExtension(scene.path);
        var imported = AnimGroupImporter.ImportFromScene(scene, sceneName,
            AnimGroupBakery.GetAnimationsFolder(scene.path));
        AnimControlDataDto animControls = null;
        if (imported.Count > 0)
            animControls = new AnimControlDataDto { groups = imported.ToArray() };

        LayoutEditorLog.Log("anim group: export " + scene.name + " -> " +
            (animControls != null ? animControls.groups.Length : 0) + " group(s)");

        // 按钮↔动画组联动也从场景重建（Design/Button Logic 下的 helper 接线）。
        var buttonLinks = ButtonLinkBakery.ImportFromScene(scene, imported, items);

        // 按钮↔事件组联动同样从场景重建（Design/Button Event Logic 下的 helper 接线）。
        var buttonEvents = ButtonEventBakery.ImportFromScene(scene, items);

        var doc = new LayoutDocumentDto
        {
            sceneAssetPath = scene.path,
            items = items.ToArray(),
            floors = SceneFloorExporter.ExportFromScene().ToArray(),
            walkable = SceneWalkabilityReader.ReadWalkable().ToArray(),
            deathInfo = SceneWalkabilityReader.ReadDeathInfo(),
            animControls = animControls,
            switchLinks = CollectSwitchLinks(items).ToArray(),
            buttonLinks = buttonLinks.Count > 0
                ? new LayoutButtonLinkDataDto { links = buttonLinks.ToArray() }
                : null,
            buttonEvents = buttonEvents.Count > 0
                ? new LayoutButtonEventDataDto { links = buttonEvents.ToArray() }
                : null,
            cameraInfo = CollectCameraInfo(),
            lights = CollectLights().ToArray()
        };
        return doc;
    }

    /// <summary>导出游戏相机（背景色 / FOV + 只读 transform 快照）。
    ///  优先取 tag=MainCamera 的相机，兜底第一个启用相机。</summary>
    public static CameraInfoDto CollectCameraInfo()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
            return null;

        var cam = Camera.main;
        if (cam == null)
        {
            foreach (var rootGo in scene.GetRootGameObjects())
            {
                cam = rootGo.GetComponentInChildren<Camera>();
                if (cam != null)
                    break;
            }
        }
        if (cam == null)
            return null;

        var t = cam.transform;
        var e = t.eulerAngles;
        return new CameraInfoDto
        {
            backgroundColor = "#" + ColorUtility.ToHtmlStringRGB(cam.backgroundColor),
            fieldOfView = cam.fieldOfView,
            position = LayoutVector3.From(t.position),
            pitch = e.x,
            yaw = e.y,
            roll = e.z,
            nearClip = cam.nearClipPlane,
            farClip = cam.farClipPlane
        };
    }

    /// <summary>收集所有根物体下 "Lights" 子树中的非 prefab 灯光
    ///  （通常为 Art/Lights/day；prefab 灯作为普通 item 往返，不在此列）。</summary>
    public static List<LightInfoDto> CollectLights()
    {
        var lights = new List<LightInfoDto>();
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
            return lights;

        foreach (var rootGo in scene.GetRootGameObjects())
        {
            for (int i = 0; i < rootGo.transform.childCount; i++)
            {
                var child = rootGo.transform.GetChild(i);
                if (child.name != "Lights")
                    continue;
                CollectNonPrefabLights(child, lights);
            }
        }
        return lights;
    }

    private static void CollectNonPrefabLights(Transform root, List<LightInfoDto> lights)
    {
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

            // Prefab-instance lights (decoration_wall_light 1 …) ride as regular items.
            if (PrefabUtility.GetPrefabType(go) != PrefabType.None)
                continue;

            // Temp-loaded bundle content under a PseudoPrefab placeholder moves with
            // the placeholder and must not be exported as a scene light.
            // 2026-09-03：防线泛化到 Stub 基类——新式 SetupCustomPrefab 家族
            // （dlc08_cannon 等）的 Setup 在编辑模式实例化的模型子树同样要跳过。
            if (go.GetComponentInParent<LevelEditorStub.Stub>() != null)
                continue;

            var light = go.GetComponent<Light>();
            if (light == null)
                continue;

            // FX 闪电灯由动画组烘焙拥有（Lights/FX_Lightning），不进灯光面板。
            if (LayoutEditorHierarchy.GetHierarchyPath(t) == AnimGroupBakery.FxLightPath)
                continue;

            lights.Add(new LightInfoDto
            {
                hierarchyPath = LayoutEditorHierarchy.GetHierarchyPath(t),
                displayName = go.name,
                lightType = (int)light.type,
                color = "#" + ColorUtility.ToHtmlStringRGB(light.color),
                intensity = light.intensity,
                range = light.range,
                spotAngle = light.spotAngle,
                enabled = light.enabled,
                eulerAngles = LayoutVector3.From(t.eulerAngles)
            });
        }
    }

    /// <summary>收集场景中所有 PseudoPrefabSwitchStub 的 objectToTrigger 联动，
    ///  输出为文档级 switchLinks（断头台/饮料机/酱料机按钮触发）。</summary>
    private static System.Collections.Generic.List<LayoutSwitchLinkDto> CollectSwitchLinks(
        System.Collections.Generic.List<LayoutItemDto> items)
    {
        var links = new System.Collections.Generic.List<LayoutSwitchLinkDto>();
        foreach (var item in items)
        {
            if (item == null || string.IsNullOrEmpty(item.instanceId))
                continue;
            if (!item.instanceId.StartsWith("u:", System.StringComparison.Ordinal))
                continue;
            int id;
            if (!int.TryParse(item.instanceId.Substring(2), out id))
                continue;
            var go = EditorUtility.InstanceIDToObject(id) as GameObject;
            if (go == null)
                continue;
            var sw = go.GetComponent<LevelEditorStub.PseudoPrefabSwitchStub>();
            // ToggleSwitch（拉杆）走 PseudoPrefabToggleSwitchStub，同款字段。
            var toggleSw = go.GetComponent<LevelEditorStub.PseudoPrefabToggleSwitchStub>();
            GameObject[] targets;
            string triggerName;
            if (sw != null)
            {
                targets = sw.objectToTrigger;
                triggerName = sw.triggerOnObject;
            }
            else if (toggleSw != null)
            {
                targets = toggleSw.objectToTrigger;
                triggerName = toggleSw.triggerOnObject;
            }
            else
            {
                continue;
            }
            if (targets == null || targets.Length == 0)
                continue;
            foreach (var target in targets)
            {
                if (target == null)
                    continue;
                links.Add(new LayoutSwitchLinkDto
                {
                    switchId = item.instanceId,
                    targetId = "u:" + target.GetInstanceID(),
                    trigger = triggerName ?? ""
                });
            }
        }
        return links;
    }

    public static List<LayoutItemDto> ExportFromScene()
    {
        var items = new List<LayoutItemDto>();
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
            return items;

        foreach (var rootGo in scene.GetRootGameObjects())
        {
            if (!IsExportRoot(rootGo.name))
                continue;

            CollectUnderTransform(rootGo.transform, items);
        }

        CollectCollisionObjects(items);

        return items;
    }

    private static bool IsExportRoot(string name)
    {
        for (int i = 0; i < ExportRootNames.Length; i++)
        {
            if (ExportRootNames[i] == name)
                return true;
        }

        return false;
    }

    private static void CollectUnderTransform(Transform root, List<LayoutItemDto> items)
    {
        var stack = new Stack<Transform>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var t = stack.Pop();
            for (int i = 0; i < t.childCount; i++)
                stack.Push(t.GetChild(i));

            var go = t.gameObject;
            var path = LayoutEditorHierarchy.GetHierarchyPath(t);

            if (ShouldSkipSubtree(path))
                continue;

            if ((go.hideFlags & HideFlags.HideAndDontSave) != 0)
                continue;

            if (!IsPrefabInstanceRoot(go))
                continue;

            // Skip temp-loaded bundle content spawned under a PseudoPrefab
            // placeholder: it moves with the placeholder and must not appear
            // as a separate item. The placeholder root itself carries the
            // stub, so the check must exclude the GameObject itself.
            // 2026-09-03：泛化到 Stub 基类（PseudoPrefabStub 与 SetupCannonStub 等
            // SetupCustomPrefabStub 家族的共同基类）——否则新式炮/终端等在编辑模式
            // 实例化的子树（RotatingPart/ModelParent/Target/PFX…）会被枚举成
            // 独立物品（s_test7 大炮 ×16 事故实证）。
            var stub = go.GetComponentInParent<LevelEditorStub.Stub>();
            if (stub != null && stub.gameObject != go)
                continue;

            var prefabAsset = PrefabUtility.GetPrefabParent(go) as GameObject;
            if (prefabAsset == null)
                continue;

            var assetPath = AssetDatabase.GetAssetPath(prefabAsset);
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            var parentPath = t.parent != null
                ? LayoutEditorHierarchy.GetHierarchyPath(t.parent)
                : string.Empty;

            var eulerY = t.localEulerAngles.y;
            items.Add(new LayoutItemDto
            {
                instanceId = "u:" + go.GetInstanceID(),
                hierarchyPath = path,
                prefabGuid = guid,
                prefabAssetPath = assetPath,
                parentPath = parentPath,
                displayName = go.name,
                localPosition = LayoutVector3.From(t.localPosition),
                worldPosition = LayoutVector3.From(t.position),
                localRotationX = t.localEulerAngles.x,
                localRotationY = eulerY,
                localScale = LayoutVector3.From(t.localScale),
                footprint = ResolveFootprint(go, path, prefabAsset.name)
            });
            LayoutEditorStubIO.ExportStub(go, items[items.Count - 1]);
        }
    }

    private static bool ShouldSkipSubtree(string hierarchyPath)
    {
        foreach (var skipped in SkippedSubtrees)
        {
            if (hierarchyPath == skipped || hierarchyPath.StartsWith(skipped + "/"))
                return true;
        }

        return false;
    }

    private static bool IsPrefabInstanceRoot(GameObject go)
    {
        var prefabType = PrefabUtility.GetPrefabType(go);
        if (prefabType != PrefabType.PrefabInstance && prefabType != PrefabType.DisconnectedPrefabInstance)
            return false;

        var root = PrefabUtility.FindPrefabRoot(go);
        return root == go;
    }

    /// <summary>
    /// Footprint priority: hand-authored catalog override (gameplay-critical,
    /// e.g. ServingStation 2x1) > measured renderer bounds (decor only, items
    /// under Art/) > 1x1 fallback. Gameplay items (Design/, Chefs/) keep the
    /// catalog/default footprint untouched.
    /// </summary>
    private static LayoutFootprint ResolveFootprint(GameObject go, string hierarchyPath, string prefabId)
    {
        LayoutFootprint fp;
        if (LayoutEditorCatalogLookup.TryGetFootprint(prefabId, out fp))
            return fp;

        if (!string.IsNullOrEmpty(hierarchyPath) && hierarchyPath.StartsWith("Art/", System.StringComparison.Ordinal))
            return LayoutEditorFootprintMeasure.MeasureCells(go);

        return new LayoutFootprint { cellsX = 1, cellsZ = 1 };
    }

    /// <summary>
    /// Collect standalone (non-prefab) collision-only GameObjects — BoxCollider
    /// objects without MeshRenderer/MeshFilter — that would otherwise be invisible
    /// to the export pipeline and lost during sync or write-back. These include
    /// Col_Wall under Design/Collision and collidor_* helpers under Art/Decoration.
    /// </summary>
    private static void CollectCollisionObjects(List<LayoutItemDto> items)
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
            return;

        foreach (var rootGo in scene.GetRootGameObjects())
        {
            if (rootGo.name != "Design" && rootGo.name != "Art" && rootGo.name != "Chefs")
                continue;

            var stack = new Stack<Transform>();
            stack.Push(rootGo.transform);

            while (stack.Count > 0)
            {
                var t = stack.Pop();
                for (int i = 0; i < t.childCount; i++)
                    stack.Push(t.GetChild(i));

                var go = t.gameObject;
                if ((go.hideFlags & HideFlags.HideAndDontSave) != 0)
                    continue;

                if (IsPrefabInstanceRoot(go))
                    continue;

                // Skip PseudoPrefab-spawned temp content (see CollectUnderTransform).
                // 2026-09-03：泛化到 Stub 基类（同上，覆盖 SetupCustomPrefab 家族）。
                var stub = go.GetComponentInParent<LevelEditorStub.Stub>();
                if (stub != null && stub.gameObject != go)
                    continue;

                var mr = go.GetComponent<MeshRenderer>();
                var mf = go.GetComponent<MeshFilter>();
                var col = go.GetComponent<BoxCollider>();
                if (mr != null || mf != null || col == null)
                    continue;

                // 只导出空气墙（1×1×1.132 魔法数识别，轴向无关：绕 X/Z 旋转躺平的墙也能识别）。
                // Col_Wall / Col_Floor / collidor_* 等场景碰撞助手一律过滤：
                // 它们是场景级辅助对象，不属于编辑器物品，不进入核心层。
                if (!IsAirWallCollider(col))
                    continue;

                var path = LayoutEditorHierarchy.GetHierarchyPath(t);
                var parentPath = t.parent != null
                    ? LayoutEditorHierarchy.GetHierarchyPath(t.parent)
                    : string.Empty;

                var euler = t.localEulerAngles;
                items.Add(new LayoutItemDto
                {
                    instanceId = "u:" + go.GetInstanceID(),
                    hierarchyPath = path,
                    prefabGuid = AirWallCatalogGuid,
                    prefabAssetPath = "",
                    parentPath = parentPath,
                    displayName = "AirWall",
                    localPosition = LayoutVector3.From(t.localPosition),
                    worldPosition = LayoutVector3.From(t.position),
                    localRotationX = euler.x,
                    localRotationY = euler.y,
                    localRotationZ = euler.z,
                    localScale = LayoutVector3.From(t.localScale),
                    colliderCenter = LayoutVector3.From(col.center),
                    footprint = new LayoutFootprint { cellsX = 1, cellsZ = 1 },
                    stubKind = "Collision",
                    airWall = true,
                    walkable = false
                });
            }
        }
    }

    /// <summary>空气墙魔法数识别：一轴≈1.132，另两轴≈1.2（一格）或旧版≈1（允许任意轴向排列）。</summary>
    private static bool IsAirWallCollider(BoxCollider col)
    {
        var s = col.size;
        int tall = (Mathf.Approximately(s.x, 1.132f) ? 1 : 0)
                 + (Mathf.Approximately(s.y, 1.132f) ? 1 : 0)
                 + (Mathf.Approximately(s.z, 1.132f) ? 1 : 0);
        int unit = (IsAirWallHorizontalSize(s.x) ? 1 : 0)
                 + (IsAirWallHorizontalSize(s.y) ? 1 : 0)
                 + (IsAirWallHorizontalSize(s.z) ? 1 : 0);
        return tall == 1 && unit == 2;
    }

    private static bool IsAirWallHorizontalSize(float v)
    {
        return Mathf.Approximately(v, 1f)
            || Mathf.Approximately(v, LayoutEditorCatalogLookup.GridCellSize);
    }
}
