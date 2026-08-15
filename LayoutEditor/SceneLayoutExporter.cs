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
        var imported = MoveControlImporter.ImportFromScene(scene, sceneName,
            MoveControlBakery.GetAnimationsFolder(scene.path));
        MoveControlDataDto moveControls = null;
        if (imported.Count > 0)
            moveControls = new MoveControlDataDto { groups = imported.ToArray() };

        LayoutEditorLog.Log("move control: export " + scene.name + " -> " +
            (moveControls != null ? moveControls.groups.Length : 0) + " group(s)");

        var doc = new LayoutDocumentDto
        {
            sceneAssetPath = scene.path,
            items = items.ToArray(),
            floors = SceneFloorExporter.ExportFromScene().ToArray(),
            walkable = SceneWalkabilityReader.ReadWalkable().ToArray(),
            deathInfo = SceneWalkabilityReader.ReadDeathInfo(),
            moveControls = moveControls
        };
        return doc;
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
            var stub = go.GetComponentInParent<LevelEditorStub.PseudoPrefabStub>();
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
                var stub = go.GetComponentInParent<LevelEditorStub.PseudoPrefabStub>();
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

    /// <summary>空气墙魔法数识别：三个轴向恰好一个≈1.132，另外两个≈1（允许任意轴向排列）。</summary>
    private static bool IsAirWallCollider(BoxCollider col)
    {
        var s = col.size;
        int tall = (Mathf.Approximately(s.x, 1.132f) ? 1 : 0)
                 + (Mathf.Approximately(s.y, 1.132f) ? 1 : 0)
                 + (Mathf.Approximately(s.z, 1.132f) ? 1 : 0);
        int unit = (Mathf.Approximately(s.x, 1f) ? 1 : 0)
                 + (Mathf.Approximately(s.y, 1f) ? 1 : 0)
                 + (Mathf.Approximately(s.z, 1f) ? 1 : 0);
        return tall == 1 && unit == 2;
    }
}
