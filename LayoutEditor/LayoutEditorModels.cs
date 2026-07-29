using System;

[Serializable]
public class LayoutVector3
{
    public float x;
    public float y;
    public float z;

    public static LayoutVector3 From(UnityEngine.Vector3 v)
    {
        return new LayoutVector3 { x = v.x, y = v.y, z = v.z };
    }

    public UnityEngine.Vector3 ToVector3()
    {
        return new UnityEngine.Vector3(x, y, z);
    }
}

[Serializable]
public class LayoutFootprint
{
    public int cellsX = 1;
    public int cellsZ = 1;
}

[Serializable]
public class LayoutDispenserStubDto
{
    public string spawnerItemPrefabGuid;
}

[Serializable]
public class LayoutFoodSpawnerStubDto
{
    public bool spawnInOrder = true;
    public string[] attachmentPrefabGuids;
    public float[] weights;
    public float triggerTime = 5f;
    public bool triggerAtStart = true;
}

[Serializable]
public class LayoutItemDto
{
    public string instanceId;
    public string hierarchyPath;
    public string prefabGuid;
    public string prefabAssetPath;
    public string parentPath;
    public string displayName;
    public LayoutVector3 localPosition;
    public LayoutVector3 worldPosition;
    public float localRotationY;
    public LayoutFootprint footprint;
    /** Dispenser | AttachingFoodSpawner | empty */
    public string stubKind;
    public LayoutDispenserStubDto dispenser;
    public LayoutFoodSpawnerStubDto foodSpawner;
}

[Serializable]
public class LayoutDocumentDto
{
    public string sceneAssetPath;
    public LayoutItemDto[] items;
    /** Editable floor/background objects (plane floors + themed floor prefabs). */
    public FloorDto[] floors;
    /** Read-only walkable collision rectangles (Col_Floor / Col_Ice). */
    public WalkableRectDto[] walkable;
    /** Read-only death configuration (water / goo / fall). */
    public DeathInfoDto deathInfo;
}

/** A floor/background surface object in the scene. */
[Serializable]
public class FloorDto
{
    public string instanceId;
    public string hierarchyPath;
    public string parentPath;
    public string displayName;
    /** solid | ice | snow | sand | alien | walkway | carpet | section | conveyor | background */
    public string surfaceKind;
    /** plane | quad | prefab (prefab = themed floor tile / travelator / sky, footprint only) */
    public string meshType;
    /** Built-in mesh fileID when meshType is plane/quad (10209/10210), else 0. */
    public int meshFileId;
    public string materialGuid;
    public string materialAssetPath;
    public string materialName;
    public LayoutVector3 localPosition;
    public LayoutVector3 worldPosition;
    public float localRotationY;
    public LayoutVector3 localScale;
    /** World-space width / depth in units (computed from mesh + scale). */
    public float widthUnits;
    public float depthUnits;
    /** Same in grid cells (divided by 1.2). */
    public int widthCells;
    public int depthCells;
    /** For prefab-typed floors, the prefab guid/assetPath. */
    public string prefabGuid;
    public string prefabAssetPath;
}

[Serializable]
public class WalkableRectDto
{
    /** solid | ice */
    public string surfaceType;
    public float cx;
    public float cz;
    public float sx;
    public float sz;
    public string sourcePath;
}

[Serializable]
public class KillPlaneInfoDto
{
    public string hierarchyPath;
    public string respawnType;
    public string deathEffectName;
}

[Serializable]
public class DeathInfoDto
{
    /** water | goo | fall */
    public string deathType;
    public string deathEffectName;
    public KillPlaneInfoDto[] killPlanes;
}

[Serializable]
public class FloorMaterialDto
{
    public string guid;
    public string id;
    public string assetPath;
    public string nameZh;
    /** Parsed dimension tag if name matches *_<W>x<H> (e.g. "12x8"), else empty. */
    public string sizeTag;
}

[Serializable]
public class FloorMaterialCatalogDto
{
    public FloorMaterialDto[] materials;
}

[Serializable]
public class GridInfoDto
{
    public bool found;
    public LayoutVector3 worldPosition;
    public LayoutVector3 cellSize;
    public int gridHalfSizeX;
    public int gridHalfSizeZ;
    public float origin;
}

[Serializable]
public class LevelSetSceneDto
{
    public string assetPath;
    public string levelSet;
    public string sceneName;
}

[Serializable]
public class LevelSetSceneListDto
{
    public LevelSetSceneDto[] scenes;
}

[Serializable]
public class ApiErrorDto
{
    public string error;
}

[Serializable]
public class IngredientEntryDto
{
    public string guid;
    public string id;
    public string nameZh;
    public string nameEn;
    public string assetPath;
}

[Serializable]
public class IngredientCatalogDto
{
    public IngredientEntryDto[] ingredients;
}

[Serializable]
public class RecipeEntryDto
{
    public string guid;
    public string id;
    public string nameZh;
    public string assetPath;
}

[Serializable]
public class RecipeCatalogDto
{
    public RecipeEntryDto[] recipes;
}

[Serializable]
public class LevelRecipesDto
{
    public string levelInfoAssetPath;
    public string levelName;
    public string[] recipeGuids;
}

[Serializable]
public class LevelRecipesUpdateDto
{
    public string levelInfoAssetPath;
    public string[] recipeGuids;
}
