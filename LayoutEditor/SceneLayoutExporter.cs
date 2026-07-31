using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneLayoutExporter
{
    private static readonly string[] ExportRootNames = { "Design", "Art", "Chefs" };
    private static readonly HashSet<string> SkippedSubtrees = new HashSet<string>
    {
        "Design/Collision",
    };

    public static LayoutDocumentDto ExportActiveScene()
    {
        var scene = SceneManager.GetActiveScene();
        var doc = new LayoutDocumentDto
        {
            sceneAssetPath = scene.path,
            items = ExportFromScene().ToArray(),
            floors = SceneFloorExporter.ExportFromScene().ToArray(),
            walkable = SceneWalkabilityReader.ReadWalkable().ToArray(),
            deathInfo = SceneWalkabilityReader.ReadDeathInfo()
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
                localRotationY = eulerY,
                localScale = LayoutVector3.From(t.localScale),
                footprint = LayoutEditorCatalogLookup.GetFootprint(prefabAsset.name)
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
}
