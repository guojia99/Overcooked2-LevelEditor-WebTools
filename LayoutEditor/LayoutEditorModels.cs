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
public class LayoutConveyorStubDto
{
    public float conveySpeed = 0.5f;
}

[Serializable]
public class LayoutTeleportalStubDto
{
    /** "u:<instanceID>" of the paired Teleportal's GameObject, or empty. */
    public string exitPortalInstanceId;
    /** PortalColor enum value (int). */
    public int portalColor;
    public bool doubleSided;
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
public class LayoutCookingUtensilStubDto
{
    public int capacity;
    public string[] allowedIngredientGuids;
}

[Serializable]
public class LayoutTravelatorStubDto
{
    public float speed = 2.5f;
}

[Serializable]
public class LayoutFlamethrowerStubDto
{
    public float cookingRate = 4f;
}

[Serializable]
public class LayoutCleanPlateStackStubDto
{
    public int plateCount;
    public string platePrefabGuid;
}

[Serializable]
public class LayoutBurnerStubDto
{
    /** ProjectileSpawner.FireMode enum value (int). */
    public int fireMode;
    public float airTime;
    public bool randomTargetOrder;
    public bool hideVisual;
}

[Serializable]
public class LayoutPlayerStubDto
{
    /** PseudoPrefabPlayerStub.Player enum value (int). 11 = Count (auto). */
    public int playerID = 11;
}

[Serializable]
public class LayoutServingStationStubDto
{
    /** "u:<instanceID>" of the bound PlateReturn's GameObject, or empty. */
    public string plateReturnInstanceId;
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
    public LayoutVector3 localScale;
    public LayoutFootprint footprint;
    /** True for surface-floor prefabs (raft planks, ice_floor, ...) → generate a walkable Col_Floor under them. */
    public bool walkable;
    /** Dispenser | AttachingFoodSpawner | Conveyor | Teleportal | CookingUtensil | Travelator | Flamethrower | CleanPlateStack | Burner | Player | ServingStation | empty */
    public string stubKind;
    public LayoutDispenserStubDto dispenser;
    public LayoutConveyorStubDto conveyor;
    public LayoutTeleportalStubDto teleportal;
    public LayoutFoodSpawnerStubDto foodSpawner;
    public LayoutCookingUtensilStubDto cookingUtensil;
    public LayoutTravelatorStubDto travelator;
    public LayoutFlamethrowerStubDto flamethrower;
    public LayoutCleanPlateStackStubDto cleanPlateStack;
    public LayoutBurnerStubDto burner;
    public LayoutPlayerStubDto player;
    public LayoutServingStationStubDto servingStation;
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
    /** World-space XZ bounds of the kill plane collider (the actual fall zone). */
    public float cx;
    public float cz;
    public float sx;
    public float sz;
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
    public string nameEn;
    public string assetPath;
    public string cookingStep;
    public string[] ingredients;
    public int ingredientCount;
    public int cookingStepCount;
    public int score;
    public bool isCustom;
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

// ---------- Audio catalogs ----------

[Serializable]
public class MusicEntryDto
{
    public string guid;
    public string id;
    public string assetPath;
    public string bundleName;
    public string nameZh;
}

[Serializable]
public class MusicCatalogDto
{
    public MusicEntryDto[] music;
}

[Serializable]
public class AudioDirectoryEntryDto
{
    public string guid;
    public string id;
    public string assetPath;
    public string bundleName;
    public string nameZh;
}

[Serializable]
public class AudioDirectoryCatalogDto
{
    public AudioDirectoryEntryDto[] audioDirectories;
}

[Serializable]
public class AmbienceCatalogDto
{
    public string[] ambiences;
}

[Serializable]
public class DeathEffectEntryDto
{
    public string guid;
    public string id;
    public string assetPath;
    public string nameZh;
}

[Serializable]
public class DeathEffectCatalogDto
{
    public DeathEffectEntryDto[] deathEffects;
}

// ---------- Sets ----------

[Serializable]
public class LevelSetInfoDto
{
    public string setName;
    public string assetPath;
    public string dataDir;
    public string levelSetName;
    public string levelSetNameZH;
    public string author;
    public string version;
    public string uid;
    public int levelCount;
}

[Serializable]
public class LevelSetListDto
{
    public LevelSetInfoDto[] sets;
}

[Serializable]
public class LevelSetCreateDto
{
    public string setName;
    public string levelSetName;
    public string levelSetNameZH;
    public string author;
}

[Serializable]
public class LevelSetInfoUpdateDto
{
    public string setName;
    public string levelSetName;
    public string levelSetNameZH;
    public string author;
    public string version;
}

// ---------- Levels ----------

[Serializable]
public class LevelSummaryDto
{
    public string assetPath;
    public string dataDir;
    public string levelName;
    public string levelNameZH;
    public string sceneName;
    public string sceneAssetPath;
    public bool hasScreenshot;
    public bool hasScene;
}

[Serializable]
public class LevelListDto
{
    public LevelSummaryDto[] levels;
}

[Serializable]
public class PerPlayerConfigDto
{
    public bool exists;
    public int orderLifeTime;
    public int timeBetweenOrders;
    public int plateReturnTime;
    public float survivalTimeMultiplier;
    public int roundTime;
    public int oneStarScore;
    public int twoStarScore;
    public int threeStarScore;
    public int fourStarScore;
}

[Serializable]
public class AudioConfigDto
{
    public string inLevelMusicGuid;
    public string inLevelMusicId;
    public string[] ambiences;
    public string[] audioDirectoryGuids;
    public string[] audioDirectoryIds;
    public string onDeathEffectGuid;
    public string onDeathEffectId;
}

[Serializable]
public class LevelDetailDto
{
    public string levelInfoAssetPath;
    public string levelName;
    public string levelNameZH;
    public string sceneName;
    public string sceneAssetPath;
    public bool hasScreenshot;
    public int debugRecipeCount;
    public bool disableDynamicParenting;
    public string[] dependencies;
    public PerPlayerConfigDto[] configs;
    public AudioConfigDto audio;
}

[Serializable]
public class LevelCreateDto
{
    public string setName;
    public string levelId;
    public string levelName;
    public string levelNameZH;
}

[Serializable]
public class LevelInfoUpdateDto
{
    public string assetPath;
    public string levelName;
    public string levelNameZH;
    public string sceneName;
    public int debugRecipeCount;
    public bool disableDynamicParenting;
    public string[] dependencies;
}

[Serializable]
public class LevelConfigUpdateDto
{
    public string assetPath;
    public PerPlayerConfigDto config_1p;
    public PerPlayerConfigDto config_2p;
    public PerPlayerConfigDto config_3p;
    public PerPlayerConfigDto config_4p;
}

[Serializable]
public class LevelAudioUpdateDto
{
    public string sceneAssetPath;
    public string inLevelMusicGuid;
    public string[] ambiences;
    public string[] audioDirectoryGuids;
    public string onDeathEffectGuid;
}

[Serializable]
public class LevelDeleteDto
{
    public string setName;
    public string levelId;
}

[Serializable]
public class DeathThemeDto
{
    public string sceneAssetPath;
    /** void | water | lava | sky | goo */
    public string theme;
}

[Serializable]
public class KillPlaneBoundsDto
{
    public string sceneAssetPath;
    public float cx;
    public float cz;
    public float sx;
    public float sz;
}

[Serializable]
public class AssetPathListDto
{
    public string[] paths;
}
