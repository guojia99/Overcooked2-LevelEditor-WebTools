using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Collects the scene's editable floor/background surface objects.
/// These are plain (non-prefab) GameObjects using a built-in Plane/Quad mesh
/// plus a MeshRenderer - the real kitchen floor that the regular prefab
/// exporter skips. Themed floor-tile prefabs (ice_floor_01, alien_floor_tile, ...)
/// are NOT collected here: they already round-trip through the items[] export
/// and are classified into the floor layer by the catalog surfaceTier.
/// </summary>
public static class SceneFloorExporter
{
    private const int PlaneMeshFileId = 10209;
    private const int QuadMeshFileId = 10210;
    /// <summary>SceneLayoutApplier 生成的 warp（透视贴合）内嵌网格的 meshId 占位值
    ///  （非内置网格，仅用于在导出器内部与 Plane/Quad 区分）。</summary>
    private const int WarpMeshFileId = 1;
    /// <summary>空气地板碰撞盒名称：仅此名称的 Ground 层 BoxCollider 被识别为空气地板
    /// （无可见 Plane，只有可行走碰撞盒）。几何与普通 Col_Floor 相同。与空气墙的
    /// 1×1×1.132 魔法数不同，空气地板仅靠名称识别，故不会被 items 导出器捡走。</summary>
    public const string AirFloorColliderName = "Col_AirFloor";

    /// <summary>True for editor air-floor walk colliders (Col_AirFloor and Unity
    /// duplicate suffixes Col_AirFloor (1), …).</summary>
    public static bool IsAirFloorColliderName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return false;
        return objectName == AirFloorColliderName
            || objectName.StartsWith(AirFloorColliderName + " (", System.StringComparison.Ordinal);
    }

    public static List<FloorDto> ExportFromScene()
    {
        var floors = new List<FloorDto>();
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
            return floors;

        foreach (var rootGo in scene.GetRootGameObjects())
        {
            CollectRecursive(rootGo.transform, floors);
        }

        return floors;
    }

    private static void CollectRecursive(Transform root, List<FloorDto> floors)
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

            if (IsPrefabInstance(go))
                continue;

            // Skip real bundle prefabs spawned at edit-time by the PseudoPrefab
            // system: they appear as plain (non-prefab) GameObjects under a
            // placeholder carrying a PseudoPrefabStub, and their quad/mesh would
            // otherwise be exported as a duplicate "floor" next to the wrapper
            // instance that the items exporter already round-trips.
            if (go.GetComponentInParent<LevelEditorStub.PseudoPrefabStub>() != null)
                continue;

            TryAddFloor(go, t, floors);
            TryAddAirFloor(go, t, floors);
        }
    }

    /// <summary>识别空气地板：名称为 Col_AirFloor 的 Ground 层 BoxCollider
    /// （仅有可行走碰撞盒，无可见 Plane）。仅靠名称区分——几何与普通 Col_Floor 相同，
    /// 故不能像空气墙那样用高度魔法数识别。</summary>
    private static void TryAddAirFloor(GameObject go, Transform t, List<FloorDto> floors)
    {
        // 动画组烘焙后的岛式层级：AirFloor / Ground（对齐 oc1_story 3-4 Island/Ground）。
        if (go.name == AirFloorRig.GroundChildName && t.parent != null
            && AirFloorRig.IsWrapperName(t.parent.name))
        {
            go = t.gameObject;
            t = go.transform;
        }
        else if (!IsAirFloorColliderName(go.name))
        {
            return;
        }
        var col = go.GetComponent<BoxCollider>();
        if (col == null)
            return;
        if (!IsGroundLayer(go))
            return;

        var b = col.bounds;
        if (b.size.x <= 0.001f || b.size.z <= 0.001f)
            return;

        // 写回失败重试会在动画组里堆 Col_AirFloor (1)… 副本；只导出同尺寸同位置
        // 的第一个，避免 web 文档出现多条空气地板、下次写回再克隆一轮。
        foreach (var existing in floors)
        {
            if (existing == null || !existing.airFloor)
                continue;
            var ep = existing.worldPosition ?? existing.localPosition;
            if (ep == null)
                continue;
            if (Mathf.Abs(ep.x - b.center.x) < 0.05f
                && Mathf.Abs(ep.z - b.center.z) < 0.05f
                && Mathf.Abs((existing.widthUnits > 0f ? existing.widthUnits : existing.widthCells * LayoutEditorCatalogLookup.GridCellSize) - b.size.x) < 0.15f
                && Mathf.Abs((existing.depthUnits > 0f ? existing.depthUnits : existing.depthCells * LayoutEditorCatalogLookup.GridCellSize) - b.size.z) < 0.15f)
                return;
        }

        var path = LayoutEditorHierarchy.GetHierarchyPath(t);
        var parentPath = t.parent != null
            ? LayoutEditorHierarchy.GetHierarchyPath(t.parent)
            : string.Empty;

        floors.Add(new FloorDto
        {
            instanceId = "u:" + go.GetInstanceID(),
            hierarchyPath = path,
            parentPath = parentPath,
            displayName = "AirFloor",
            surfaceKind = "solid",
            meshType = "plane",
            meshFileId = 0,
            localPosition = LayoutVector3.From(t.localPosition),
            worldPosition = LayoutVector3.From(t.position),
            localRotationY = t.localEulerAngles.y,
            localScale = LayoutVector3.From(UnityEngine.Vector3.one),
            widthUnits = b.size.x,
            depthUnits = b.size.z,
            widthCells = Mathf.RoundToInt(b.size.x / LayoutEditorCatalogLookup.GridCellSize),
            depthCells = Mathf.RoundToInt(b.size.z / LayoutEditorCatalogLookup.GridCellSize),
            airFloor = true,
        });
    }

    private static bool IsGroundLayer(GameObject go)
    {
        if (go == null)
            return false;
        return go.layer == 9 || LayerMask.LayerToName(go.layer) == "Ground";
    }

    private static bool IsPrefabInstance(GameObject go)
    {
        var type = PrefabUtility.GetPrefabType(go);
        return type == PrefabType.PrefabInstance || type == PrefabType.DisconnectedPrefabInstance;
    }

    private static void TryAddFloor(GameObject go, Transform t, List<FloorDto> floors)
    {
        var mf = go.GetComponent<MeshFilter>();
        var mr = go.GetComponent<MeshRenderer>();
        if (mf == null || mr == null)
            return;

        var mesh = mf.sharedMesh;
        if (mesh == null)
            return;

        int meshId = GetBuiltinMeshFileId(mesh);
        bool isWarp = meshId == WarpMeshFileId;
        if (meshId != PlaneMeshFileId && meshId != QuadMeshFileId && meshId != WarpMeshFileId)
            return;

        if (!LooksLikeFloor(go.name, t))
            return;

        float baseX, baseZ;
        string meshType;
        if (isWarp)
        {
            baseX = 1f;
            baseZ = 1f;
            meshType = "warp";
        }
        else if (meshId == PlaneMeshFileId)
        {
            baseX = 10f;
            baseZ = 10f;
            meshType = "plane";
        }
        else
        {
            baseX = 1f;
            baseZ = 1f;
            meshType = "quad";
        }

        var scale = t.localScale;
        var rotY = t.localEulerAngles.y;

        // World-space size from the renderer bounds (axis-aligned; floor planes
        // are rotated only about Y so this equals the true footprint).
        var bounds = mr.bounds;
        float widthUnits = bounds.size.x;
        float depthUnits = bounds.size.z;
        if (widthUnits <= 0.001f)
        {
            widthUnits = baseX * scale.x;
            depthUnits = baseZ * scale.z;
        }
        // warp 网格覆盖整个相机视野，包围盒巨大且不代表意图尺寸——碰撞盒尺寸
        // 用网格名后缀 "_WxD" 里作者设定的格数还原。
        int warpW = 0, warpD = 0;
        if (isWarp && TryParseWarpMeshSize(mesh.name, out warpW, out warpD))
        {
            widthUnits = warpW * LayoutEditorCatalogLookup.GridCellSize;
            depthUnits = warpD * LayoutEditorCatalogLookup.GridCellSize;
        }

        var mat = mr.sharedMaterial;
        string matGuid = null;
        string matPath = null;
        string matName = null;
        int materialTilingW = 0;
        int materialTilingD = 0;
        if (mat != null)
        {
            matPath = AssetDatabase.GetAssetPath(mat);
            if (!string.IsNullOrEmpty(matPath))
            {
                matGuid = AssetDatabase.AssetPathToGUID(matPath);
                matName = System.IO.Path.GetFileNameWithoutExtension(matPath);
            }
            else
            {
                int tw, td;
                if (LayoutEditorFloorMaterialsApi.TryParseMaterialTilingSuffix(mat.name, out tw, out td))
                {
                    materialTilingW = tw;
                    materialTilingD = td;
                }
                else if (LayoutEditorFloorMaterialsApi.TryInferTilingFromMainTex(mat, out tw, out td))
                {
                    materialTilingW = tw;
                    materialTilingD = td;
                }

                string resolvedGuid, resolvedPath;
                if (LayoutEditorFloorMaterialsApi.TryResolveFloorMaterialByName(mat.name, out resolvedGuid, out resolvedPath))
                {
                    matGuid = resolvedGuid;
                    matPath = resolvedPath;
                    matName = System.IO.Path.GetFileNameWithoutExtension(resolvedPath);
                }
                else
                {
                    matName = LayoutEditorFloorMaterialsApi.StripMaterialTilingSuffix(mat.name);
                }
            }
        }

        var path = LayoutEditorHierarchy.GetHierarchyPath(t);
        var parentPath = t.parent != null
            ? LayoutEditorHierarchy.GetHierarchyPath(t.parent)
            : string.Empty;

        // Image-floor state is encoded in the GameObject name (imgfloor|...|mode|opacity[|rotation]).
        string imgPath;
        string imgMode;
        float imgOpacity;
        int imgRotation;
        SceneLayoutApplier.TryParseImageFloorName(go.name, out imgPath, out imgMode, out imgOpacity, out imgRotation);

        var widthCells = Mathf.RoundToInt(widthUnits / LayoutEditorCatalogLookup.GridCellSize);
        var depthCells = Mathf.RoundToInt(depthUnits / LayoutEditorCatalogLookup.GridCellSize);

        floors.Add(new FloorDto
        {
            instanceId = "u:" + go.GetInstanceID(),
            hierarchyPath = path,
            parentPath = parentPath,
            displayName = imgPath != null
                ? System.IO.Path.GetFileName(imgPath)
                : StripTintFromName(go.name),
            surfaceKind = InferSurfaceKind(go.name, matName, matPath),
            meshType = meshType,
            meshFileId = meshId,
            materialGuid = matGuid,
            materialAssetPath = matPath,
            materialName = matName,
            tintColor = ExtractTintFromName(go.name),
            tintEnabled = !string.IsNullOrEmpty(ExtractTintFromName(go.name)),
            imageTexturePath = imgPath,
            imageMode = imgMode,
            imageOpacity = imgOpacity,
            imageRotation = imgRotation,
            localPosition = LayoutVector3.From(t.localPosition),
            worldPosition = LayoutVector3.From(t.position),
            localRotationY = rotY,
            localScale = LayoutVector3.From(scale),
            widthUnits = widthUnits,
            depthUnits = depthUnits,
            widthCells = widthCells,
            depthCells = depthCells,
            materialTilingW = materialTilingW > 0 ? materialTilingW : widthCells,
            materialTilingD = materialTilingD > 0 ? materialTilingD : depthCells,
        });
    }

    /** Extract a "#rrggbb" tint encoded in the floor name, or null. */
    private static string ExtractTintFromName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        var i = name.IndexOf('#');
        if (i < 0) return null;
        var hex = name.Substring(i + 1);
        return hex.Length >= 6 ? ("#" + hex.Substring(0, 6).ToLowerInvariant()) : null;
    }

    /** Floor display name with any trailing "#rrggbb" tint marker removed. */
    private static string StripTintFromName(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        var i = name.IndexOf('#');
        return i < 0 ? name : name.Substring(0, i);
    }

    private static string InferSurfaceKind(string objectName, string materialName, string materialPath)
    {
        var n = objectName == null ? "" : objectName.ToLowerInvariant();
        var m = materialName == null ? "" : materialName.ToLowerInvariant();
        var p = materialPath == null ? "" : materialPath.ToLowerInvariant();
        // Image floors encode their state in the name; they are solid planes
        // textured by an image (the path may contain themed words like "ice").
        if (n.StartsWith(SceneLayoutApplier.ImageFloorPrefix, System.StringComparison.Ordinal))
            return "solid";
        if (n == "sky" || n.Contains("background") || m.Contains("sky") || m.Contains("background") || p.Contains("background"))
            return "background";
        // Infer from material name (e.g. themed floor materials authored in Unity).
        if (m.Contains("ice"))
            return "ice";
        if (m.Contains("snow"))
            return "snow";
        if (m.Contains("sand"))
            return "sand";
        if (m.Contains("alien"))
            return "alien";
        if (m.Contains("carpet"))
            return "carpet";
        return "solid";
    }

    private static bool LooksLikeFloor(string name, Transform t)
    {
        var n = name == null ? "" : name.ToLowerInvariant();
        if (n.Contains("floor") || n == "sky" || n.Contains("background") || n.Contains("ground"))
            return true;

        // Also accept generic plane/quad surfaces that live under a Ground/Floor group.
        // "Animated Objects" is included so floors reparented into a move-control
        // group keep round-tripping through the exporter.
        var p = t.parent;
        while (p != null)
        {
            var pn = p.name == null ? "" : p.name.ToLowerInvariant();
            if (pn == "ground" || pn.Contains("floor")
                || pn == "animated objects"
                || pn.Contains("city") || pn.Contains("exterior")
                || pn.Contains("terrain") || pn.Contains("environment"))
                return true;
            p = p.parent;
        }

        return false;
    }

    private static int GetBuiltinMeshFileId(Mesh mesh)
    {
        if (mesh == null)
            return 0;
        var n = mesh.name;
        if (n == "Plane")
            return PlaneMeshFileId;
        if (n == "Quad")
            return QuadMeshFileId;
        if (n != null && n.StartsWith(SceneLayoutApplier.WarpMeshNamePrefix, System.StringComparison.Ordinal))
            return WarpMeshFileId;
        return 0;
    }

    /// <summary>warp 网格名 "ImgWarpFloorMesh_20x12" → (20, 12)；失败返回 false。</summary>
    private static bool TryParseWarpMeshSize(string meshName, out int w, out int d)
    {
        w = 0;
        d = 0;
        if (string.IsNullOrEmpty(meshName))
            return false;
        var idx = meshName.LastIndexOf('_');
        if (idx < 0) return false;
        var tail = meshName.Substring(idx + 1);
        var x = tail.IndexOf('x');
        if (x <= 0 || x >= tail.Length - 1) return false;
        return int.TryParse(tail.Substring(0, x), out w)
            && int.TryParse(tail.Substring(x + 1), out d)
            && w > 0 && d > 0;
    }
}


