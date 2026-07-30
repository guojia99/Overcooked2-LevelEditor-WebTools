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

    public static string Apply(LayoutDocumentDto document, float snapStep, bool syncWalkable)
    {
        if (document == null || document.items == null)
            return "Empty layout document.";

        if (!LayoutEditorSafety.PrepareSceneForApply())
            return LayoutEditorSafety.LastError;

        document.items = PruneThemeBackgroundItems(document.items);

        var scene = EditorSceneManager.GetActiveScene();
        if (!string.IsNullOrEmpty(document.sceneAssetPath) && scene.path != document.sceneAssetPath)
        {
            if (!System.IO.File.Exists(document.sceneAssetPath))
                return "Scene not found: " + document.sceneAssetPath;
            EditorSceneManager.OpenScene(document.sceneAssetPath);
            scene = EditorSceneManager.GetActiveScene();
        }

        var before = SceneLayoutExporter.ExportFromScene();
        RemoveUnmatchedSceneItems(before, document.items);

        var usedSceneObjectIds = new HashSet<int>();
        foreach (var item in document.items)
        {
            if (item == null)
                continue;

            if (string.IsNullOrEmpty(item.prefabAssetPath) && string.IsNullOrEmpty(item.prefabGuid))
                continue;

            var assetPath = item.prefabAssetPath;
            if (string.IsNullOrEmpty(assetPath) && !string.IsNullOrEmpty(item.prefabGuid))
                assetPath = AssetDatabase.GUIDToAssetPath(item.prefabGuid);

            if (string.IsNullOrEmpty(assetPath))
                continue;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
                continue;

            var pos = item.localPosition != null ? item.localPosition.ToVector3() : Vector3.zero;
            pos = SnapVector(pos, snapStep);
            var rotY = item.localRotationY;

            if (item.instanceId != null && item.instanceId.StartsWith("new:", StringComparison.Ordinal))
            {
                CreateInstance(item, prefab, assetPath, pos, rotY);
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
                        posFull.x = SnapScalar(posFull.x, snapStep);
                        posFull.z = SnapScalar(posFull.z, snapStep);
                        t.localPosition = posFull;
                        t.localEulerAngles = new Vector3(t.localEulerAngles.x, rotY, t.localEulerAngles.z);
                        ApplyItemScale(t, item);
                        LayoutEditorStubIO.ApplyStub(t.gameObject, item);
                        continue;
                    }
                }

                CreateInstance(item, prefab, assetPath, pos, rotY);
            }
        }

        ApplyFloors(document);
        if (syncWalkable)
            SyncWalkableToFloors(document);

        EditorSceneManager.MarkSceneDirty(scene);
        LayoutEditorPseudoReload.ReloadPseudoAssets();
        return null;
    }

    /// <summary>
    /// Regenerate the Ground-layer "Col_Floor" walkable colliders under Design/Collision so
    /// the walkable area matches the visible floor planes (gaps between floors become fall pits).
    /// </summary>
    private static void SyncWalkableToFloors(LayoutDocumentDto document)
    {
        var collision = LayoutEditorHierarchy.FindOrCreatePath("Design/Collision");
        if (collision == null)
            return;

        var toRemove = new List<GameObject>();
        for (int i = 0; i < collision.childCount; i++)
        {
            var c = collision.GetChild(i);
            if (c != null && c.name == "Col_Floor")
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
            float w = floor.widthUnits > 0f ? floor.widthUnits : (floor.widthCells > 0 ? floor.widthCells * LayoutEditorCatalogLookup.GridCellSize : 1.2f);
            float d = floor.depthUnits > 0f ? floor.depthUnits : (floor.depthCells > 0 ? floor.depthCells * LayoutEditorCatalogLookup.GridCellSize : 1.2f);
            CreateColFloor(collision, groundLayer, cx, cz, w, d, floor.localRotationY);
        }

        // Also make surface-floor prefab items (raft planks, floor tiles ...) walkable.
        if (document.items != null)
        {
            foreach (var item in document.items)
            {
                if (item == null || !item.walkable)
                    continue;
                float cx = item.worldPosition != null ? item.worldPosition.x : (item.localPosition != null ? item.localPosition.x : 0f);
                float cz = item.worldPosition != null ? item.worldPosition.z : (item.localPosition != null ? item.localPosition.z : 0f);
                float scx = item.localScale != null ? item.localScale.x : 1f;
                float scz = item.localScale != null ? item.localScale.z : 1f;
                if (scx <= 0f) scx = 1f;
                if (scz <= 0f) scz = 1f;
                float w = (item.footprint != null && item.footprint.cellsX > 0 ? item.footprint.cellsX : 1) * LayoutEditorCatalogLookup.GridCellSize * scx;
                float d = (item.footprint != null && item.footprint.cellsZ > 0 ? item.footprint.cellsZ : 1) * LayoutEditorCatalogLookup.GridCellSize * scz;
                CreateColFloor(collision, groundLayer, cx, cz, w, d, item.localRotationY);
            }
        }
    }

    private static void CreateColFloor(Transform parent, int groundLayer, float cx, float cz, float w, float d, float rotY)
    {
        var go = new GameObject("Col_Floor");
        Undo.RegisterCreatedObjectUndo(go, "Layout Editor Col_Floor");
        go.layer = groundLayer;
        var t = go.transform;
        t.SetParent(parent, false);
        t.localPosition = new Vector3(cx, 0f, cz);
        t.localRotation = Quaternion.Euler(0f, rotY, 0f);
        t.localScale = Vector3.one;
        var col = go.AddComponent<BoxCollider>();
        col.size = new Vector3(w, 0.4f, d);
        col.center = new Vector3(0f, -0.2f, 0f);
    }

    private static void ApplyFloors(LayoutDocumentDto document)
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
            if (string.IsNullOrEmpty(floor.hierarchyPath) && string.IsNullOrEmpty(floor.instanceId))
                continue;

            if (floor.instanceId != null && floor.instanceId.StartsWith("new:", StringComparison.Ordinal))
            {
                CreateFloorInstance(floor);
                continue;
            }

            var t = FindFloorTransform(floor);
            if (t != null)
            {
                var id = t.gameObject.GetInstanceID();
                if (usedIds.Add(id))
                {
                    ApplyFloorTransform(t, floor);
                    continue;
                }
            }

            CreateFloorInstance(floor);
        }
    }

    private static void ApplyFloorTransform(Transform t, FloorDto floor)
    {
        Undo.RecordObject(t, "Layout Editor Floor");
        var pos = floor.localPosition != null ? floor.localPosition.ToVector3() : t.localPosition;
        pos.x = SnapScalar(pos.x, LayoutEditorCatalogLookup.GridCellSize);
        pos.z = SnapScalar(pos.z, LayoutEditorCatalogLookup.GridCellSize);
        t.localPosition = pos;
        t.localEulerAngles = new Vector3(t.localEulerAngles.x, floor.localRotationY, t.localEulerAngles.z);

        var scale = FloorScaleFromCells(floor, t.localScale.y);
        t.localScale = scale;

        ApplyFloorMaterial(t, floor);
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
        }
    }

    private static void CreateFloorInstance(FloorDto floor)
    {
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
        primitive.name = !string.IsNullOrEmpty(floor.displayName) ? floor.displayName : "Floor";

        var pos = floor.localPosition != null ? floor.localPosition.ToVector3() : Vector3.zero;
        pos.x = SnapScalar(pos.x, LayoutEditorCatalogLookup.GridCellSize);
        pos.z = SnapScalar(pos.z, LayoutEditorCatalogLookup.GridCellSize);
        if (Mathf.Abs(pos.y) < 0.0001f)
            pos.y = -0.05f;
        primitive.transform.localPosition = pos;
        primitive.transform.localEulerAngles = new Vector3(0f, floor.localRotationY, 0f);
        primitive.transform.localScale = FloorScaleFromCells(floor, 1f);

        ApplyFloorMaterial(primitive.transform, floor);
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

    private static void RemoveUnmatchedSceneItems(List<LayoutItemDto> before, LayoutItemDto[] documentItems)
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

        for (int i = 0; i < unmatched.Count; i++)
            DeleteItem(unmatched[i]);
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

    private static void CreateInstance(LayoutItemDto item, GameObject prefab, string assetPath, Vector3 pos, float rotY)
    {
        var parentPath = item.parentPath;
        if (string.IsNullOrEmpty(parentPath))
            parentPath = LayoutEditorCatalogLookup.DefaultParentForAssetPath(assetPath);

        var parent = LayoutEditorHierarchy.FindOrCreatePath(parentPath);
        if (parent == null)
            return;

        var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
            return;

        Undo.RegisterCreatedObjectUndo(instance, "Layout Editor Create");
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = pos;
        instance.transform.localEulerAngles = new Vector3(0f, rotY, 0f);
        ApplyItemScale(instance.transform, item);

        if (!string.IsNullOrEmpty(item.displayName))
            instance.name = item.displayName;

        LayoutEditorStubIO.ApplyStub(instance, item);
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
