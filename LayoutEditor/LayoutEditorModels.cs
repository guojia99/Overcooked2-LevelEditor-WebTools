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
public class LayoutPlateReturnStubDto
{
    /** True = the station returns clean plates/glasses; false = dirty ones. */
    public bool returnClean;
}

[Serializable]
public class LayoutServingStationStubDto
{
    /** "u:<instanceID>" of the bound PlateReturn's GameObject, or empty. (Legacy
     *  single binding; superseded by plateReturnInstanceIds.) */
    public string plateReturnInstanceId;
    /** One-to-many binding: "u:<instanceID>" (or doc instanceId) of each bound
     *  return station (PlateReturn / GlassReturn). */
    public string[] plateReturnInstanceIds;
}

[Serializable]
public class LayoutSwitchStubDto
{
    public bool startEnabled = true;
    public string activeMaterialGuid;
    public string inactiveMaterialGuid;
}

[Serializable]
public class LayoutPressureSwitchStubDto
{
    public string occupiedMaterialGuid;
    public string unoccupiedMaterialGuid;
}

[Serializable]
public class LayoutTerminalStubDto
{
    /** "u:<instanceID>" of the pilotable GameObject, or empty. */
    public string pilotableObjectInstanceId;
}

[Serializable]
public class LayoutMeshWithMaterialStubDto
{
    public string pseudoPrefabGuid;
    public string materialGuid;
}

[Serializable]
public class LayoutSOArrayStubDto
{
    public string[] pseudoPrefabGuids;
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
    /** Euler X in degrees — needed for quad-based floor tiles that lie flat via x=90. */
    public float localRotationX;
    public float localRotationY;
    public LayoutVector3 localScale;
    public LayoutFootprint footprint;
    /** True for surface-floor prefabs (raft planks, ice_floor, ...) → generate a walkable Col_Floor under them. */
    public bool walkable;
    /** Dispenser | AttachingFoodSpawner | Conveyor | Teleportal | CookingUtensil | Travelator | Flamethrower | CleanPlateStack | Burner | Player | ServingStation | PlateReturn | GlassReturn | Switch | PressureSwitch | Terminal | empty */
    public string stubKind;
    /** Counter/Dispenser etc. appearance SO guid (base PseudoPrefabStub.pseudoPrefabSO). */
    public string pseudoPrefabGuid;
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
    public LayoutPlateReturnStubDto plateReturn;
    public LayoutSwitchStubDto switchStub;
    public LayoutPressureSwitchStubDto pressureSwitch;
    public LayoutTerminalStubDto terminal;
    public LayoutMeshWithMaterialStubDto meshWithMaterial;
    public LayoutSOArrayStubDto soArray;
}

[Serializable]
public class MoveGroupWaypointDto
{
    public string id;
    /** World XZ of the waypoint. The bakery converts to per-item local curve keys. */
    public float x;
    public float z;
    /** Keyframe time in seconds inside the event clip (imported levels only;
     * web-authored routes use uniform intervals). */
    public bool hasTime;
    public float t;
    /** Dwell seconds after arriving at this waypoint before continuing (hold). */
    public float wait;
    /** Seconds to travel from this waypoint to the next (default: event interval). */
    public float segmentSeconds;
}

[Serializable]
public class MoveGroupEventDto
{
    public string id;
    /** "move" | "wait" | "lift" | "drop" — waits bake as pure queue delays (no
     * controller state); lift/drop are pure-Y clips (no route). */
    public string type;
    /** Auto-generated when empty ("Move1".."MoveN"), matching original level naming. */
    public string triggerName;
    /** Seconds between the previous trigger and this one (TriggerQueue delays[i]). */
    public float delay;
    /** Seconds per waypoint segment inside this event's move clip (uniform routes). */
    public float intervalSeconds;
    /** Ordered waypoint route (contiguous slice of the group waypoint list). */
    public string[] waypointIds;
    /** Loop the clip itself (loopTime=1, no exit transition) — scrolling patterns
     * like 3_4/6_3 Islands. Only honored on the last move event. */
    public bool loop;
    /** Ping-pong loop: move to the end of the route, then back (wrapMode=PingPong).
     * Mutually exclusive with loop; only honored on the last move event. */
    public bool pingpong;
    /** lift/drop: target Y members rise/fall to (absolute; drop default 0). */
    public float yTo;
    /** Vertical motion seconds: lift/drop clip duration, or move event rise time. */
    public float liftSeconds;
    /** move events: lift height above the base before moving (0 = no lift). */
    public float liftHeight;
    /** move events: seconds to lower back at the end (0 = stays up). */
    public float dropSeconds;
}

[Serializable]
public class MoveGroupMemberOffsetDto
{
    public string instanceId;
    public float x;
    public float z;
    /** Phase shift in seconds (looping scroll patterns: the member follows the same
     * route shifted in time, e.g. 3_4/6_3 islands). Exclusive with x/z. */
    public float t;
    /** Bind this member to a specific waypoint id: it holds at that point (+ offset)
     * for the whole sequence instead of riding the route. */
    public string followWaypointId;
    /** Plain-object member display name (imported levels; web display only). */
    public string displayName;
    /** Full scene hierarchy path of the member (web shows its child items). */
    public string hierarchyPath;
}

[Serializable]
public class MoveGroupMemberDto
{
    public string instanceId;
    /** Plain-object member display name (imported levels; web display only). */
    public string displayName;
    /** Full scene hierarchy path of the member (web shows its child items). */
    public string hierarchyPath;
}

[Serializable]
public class MoveGroupMemberGroupDto
{
    public string id;
    public string name;
    /** Instance ids of the members in this group (subset of item/floor/object ids). */
    public string[] memberInstanceIds;
}

[Serializable]
public class MoveGroupDto
{
    public string id;
    public string displayName;
    /** Unity instance ids ("u:xxx"/"new:xxx") of the items driven by this group. */
    public string[] itemInstanceIds;
    /** Unity instance ids of the floor (Plane/Quad) objects driven by this group. */
    public string[] floorInstanceIds;
    /** Unity instance ids of plain scene objects driven by this group (imported
     * original levels: island roots, lorry parts, ...). */
    public string[] objectInstanceIds;
    /** Per-member local offset from the route (parallel tracks; imported levels). */
    public MoveGroupMemberOffsetDto[] memberOffsets;
    /** Member ids that hold still while the route moves (6_3 duplicate-cover trick). */
    public MoveGroupMemberDto[] memberStatic;
    /** User-created member groups (organizational; baked as named sub-roots under
     *  the group root so a later scene import can reconstruct them). */
    public MoveGroupMemberGroupDto[] memberGroups;
    /** Seconds after round start before the queue begins (TriggerTimer). */
    public float startDelay;
    /** Loop the whole trigger queue (loopWhenFinished + loopDelay). */
    public bool loop;
    public float loopDelay;
    /** Queue waits for the animator's finished trigger before advancing. */
    public bool waitForFinished;
    /** External trigger name that starts the queue (empty = auto start). */
    public string startTrigger;
    /** External trigger name that cancels the queue. */
    public string cancelTrigger;
    /** Trigger broadcast when the whole queue finishes (target = group root). */
    public string endTrigger;
    /** Trigger name the animator emits on clip completion (default "AnimationFinished"). */
    public string finishedTrigger;
    /** Animator.applyRootMotion. */
    public bool applyRootMotion;
    public MoveGroupWaypointDto[] waypoints;
    public MoveGroupEventDto[] events;
    /** Backend-owned: hierarchy path of the created "Animated Objects" group root. */
    public string groupHierarchyPath;
}

[Serializable]
public class MoveControlDataDto
{
    public MoveGroupDto[] groups;
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
    /** Movable object movement control (decor + items layer). */
    public MoveControlDataDto moveControls;
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
    /** Optional manual tint color (html hex, e.g. "#88aabb") for a solid floor —
     *  a user-driven recolor feature independent of the floor's material. */
    public string tintColor;
    /** Whether the tint is active. When false the floor keeps its real material
     *  even if tintColor is set. */
    public bool tintEnabled;
    /** Image floor: asset path of an uploaded texture in the level's data dir. */
    public string imageTexturePath;
    /** Image tiling mode: "tile" (repeat per cell) or "stretch" (fill the rect). */
    public string imageMode;
    /** Image opacity 0..1 (0 = transparent, 1 = opaque). */
    public float imageOpacity;
    /** Image rotation in degrees (0/90/180/270, clockwise viewed from above). */
    public int imageRotation;
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
    /** "core" / "dlc02" / "dlc05" — matches ingredients.json. */
    public string group;
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
    /** 装盘容器 id（Plate / Glass / …；"" = 无），用于关卡编辑器推断容器堆（盘子堆/杯子堆）。 */
    public string platingStep;
    public string[] ingredients;
    /** Direct composition ids for custom recipes (ingredient ids and/or sub-recipe ids,
     *  "" / null for original recipes). Used by the composition-aware grouping branch. */
    public string[] compositionIds;
    public int ingredientCount;
    public int cookingStepCount;
    public int score;
    public bool isCustom;
    /** "core" / "custom" / "dlc02" / "dlc05" — matches recipes.json. */
    public string group;
    /** Recipe family: burger / pizza / sushi / kebab / smoothie … — matches recipes.json. */
    public string type;
    /** True for score-0 half-finished products (batter, fried parts, optional pizza parts) — not orderable. */
    public bool intermediate;
    /** Ingredient cooking groups for the recipe-list UI (recipe book grouping rules):
     *  raw group first (step = ""), then per-step groups, optional final-step marker last. */
    public RecipeCookingGroupDto[] cookingGroups;
}

/** One ingredient group of a recipe for the recipe-list UI: ingredients cooked
 *  together in the same step (step = "" means a raw group with no utensil),
 *  plus the utensils/workstations used by that step. */
[Serializable]
public class RecipeCookingGroupDto
{
    /** CookingSteps asset id, "" = no cooking (raw ingredients). */
    public string step;
    /** Utensil / workstation ids for the step (e.g. "Cooker","FryPan"). */
    public string[] utensils;
    /** Ingredient asset ids in this group. */
    public string[] ingredients;
}

[Serializable]
public class IconStatusItemDto
{
    public string id;
    public string nameZh;
    public string nameEn;
    public bool hasIcon;
    public string group;
    public string type;
}

[Serializable]
public class IconStatusListDto
{
    public IconStatusItemDto[] missingRecipes;
    public IconStatusItemDto[] missingIngredients;
    public int totalRecipes;
    public int totalIngredients;
    public int recipesWithIcon;
    public int ingredientsWithIcon;
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
    public string[] baseBundles;
    public string[] alwaysLoadedBundles;
    public string[] mandatoryDirectoryIds;
    public DirectoryEventDto[] directoryEvents;
    public AudioThemeDto[] themes;
    public AudioDeathThemeDto[] deathThemes;
    public AmbienceLabelDto[] ambienceLabels;
    public AudioItemRuleDto[] itemAudioRules;
}

[Serializable]
public class DirectoryEventDto
{
    public string id;
    public string[] eventsZh;
    public string desc;
}

[Serializable]
public class AudioThemeDto
{
    public string key;
    public string[] directories;
    public string[] ambiences;
    public string[] bgm;
    public string deathTheme;
}

[Serializable]
public class AudioDeathThemeDto
{
    public string key;
    public string effectIdHint;
    public string note;
}

[Serializable]
public class BundleAnalysisDto
{
    public string[] @base;
    public string[] alwaysLoaded;
    public string[] required;
    public string[] current;
    public string[] missing;
    public string[] extras;
}

[Serializable]
public class BundleManifestEntryDto
{
    public string name;
    public string[] deps;
}

[Serializable]
public class BundleManifestDto
{
    public BundleManifestEntryDto[] dependencies;
}

[Serializable]
public class AmbienceLabelDto
{
    public string name;
    public string zh;
}

[Serializable]
public class AudioItemRuleDto
{
    public string[] items;
    public string theme;
    public string[] directories;
    public string[] ambiences;
    public string labelZh;
}

[Serializable]
public class AmbienceCatalogDto
{
    public string[] ambiences;
    public AmbienceLabelDto[] ambienceLabels;
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
    /** Asset path of the screenshot texture ("" when none), for the web UI to preview. */
    public string screenshotPath;
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
    /** Asset path of the screenshot texture ("" when none), for the web UI to preview. */
    public string screenshotPath;
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
public class ImageUploadDto
{
    public string setName;
    public string fileName;
    /** Base64-encoded image bytes (no data: prefix). */
    public string base64;
}

[Serializable]
public class ImageUploadResultDto
{
    public string texturePath;
}

[Serializable]
public class ScreenshotUploadDto
{
    /** LevelInfoSO asset path, e.g. Assets/LevelSets/my_set/data/my_level/LevelInfo_my_level.asset */
    public string assetPath;
    public string fileName;
    /** Base64-encoded image bytes (no data: prefix). */
    public string base64;
}

[Serializable]
public class ScreenshotUploadResultDto
{
    public string texturePath;
}

[Serializable]
public class AssetPathListDto
{
    public string[] paths;
}

// ---------- Custom Recipe Management ----------

[Serializable]
public class CustomRecipeCategoryDto
{
    public string id;
    public string zh;
    public string en;
}

[Serializable]
public class CustomRecipeConfigDto
{
    public int uidPrefix;
    public int nextSequence;
    public CustomRecipeCategoryDto[] categories;
}

[Serializable]
public class CustomRecipeSummaryDto
{
    public string guid;
    public string id;
    public string assetPath;
    public string recipeName;
    public string nameZh;
    public string nameEn;
    public int uID;
    public int score;
    public string category;
    public string type;
    /** Direct composition ids (ingredient ids and/or sub-recipe ids). */
    public string[] compositionIds;
    /** Leaf ingredients, recursively expanded through sub-recipes. */
    public string[] ingredients;
    /** Composition-aware cooking groups for the recipe-card UI ("" when not computable). */
    public RecipeCookingGroupDto[] cookingGroups;
    /** True when score <= 0 (半成品), used for badges and the sub-recipe picker. */
    public bool intermediate;
    /** "core" / "custom" / "dlc02" / "dlc05" / "levelset" — matches recipes.json. */
    public string group;
    public string cookingStepId;
    public string platingStepId;
    public bool hasIcon;
    public bool hasModel;
    /** 模型在游戏中的缩放/旋转/位置（应用到 prefab 根节点，运行时直接生效）。 */
    public float modelScale;
    public float modelRotationY;
    public float modelRotationX;
    public float modelRotationZ;
    public float modelPositionX;
    public float modelPositionY;
    public float modelPositionZ;
    /** Unity 导入后的模型包围盒（含配置变换；minY 为底面高度，用于前端按 Unity 实际尺寸校准缩放/朝向）。 */
    public float boundsMinY;
    public float boundsSizeX;
    public float boundsSizeY;
    public float boundsSizeZ;
}

[Serializable]
public class CustomRecipeEditDto
{
    public string setName;
    public string assetPath;
    public string recipeName;
    public string nameZh;
    public string nameEn;
    public string category;
    public int score;
    public string type;
    public string[] compositionIds;
    public string cookingStepId;
    public string cookingStepIconId;
    public string platingStepId;
    public string mixingIconId;
    public string modelPrefabId;
    public int cookingProgress;
    public int mixingProgress;
    public float modelScale;
    public float modelRotationY;
    public float modelRotationX;
    public float modelRotationZ;
    public float modelPositionX;
    public float modelPositionY;
    public float modelPositionZ;
}

[Serializable]
public class CustomRecipeListDto
{
    public CustomRecipeSummaryDto[] recipes;
}

[Serializable]
public class CustomRecipeModelFileListDto
{
    public string[] files;
}

[Serializable]
public class CustomRecipeUploadFileDto
{
    public string fileName;
    public string base64;
}

[Serializable]
public class CustomRecipeScanDiagDto
{
    public string setName;
    public string recipesDir;
    public bool dirExists;
    public int scannedCount;
    public int loadedCount;
    public string[] fsAssets;
}

/// <summary>菜谱模型/装盘链路诊断结果（只读）。</summary>
[Serializable]
public class CustomRecipeDiagnoseDto
{
    public string assetPath;
    public string error;
    public bool modelDirect;
    public bool modelSOBased;
    public string modelPath;
    public string modelType;
    /** prefab 结构：single-node（旧结构）/ root+child（新结构）/ fbx-direct。 */
    public string modelStructure;
    public int rendererCount;
    public int meshCount;
    public int materialCount;
    /** Unity 导入后的模型世界包围盒（min 与 size，用于判断实际朝向/薄轴）。 */
    public float boundsMinX;
    public float boundsMinY;
    public float boundsMinZ;
    public float boundsSizeX;
    public float boundsSizeY;
    public float boundsSizeZ;
    public int compositionCount;
    public bool cookingStepSet;
    public bool platingStepSet;
    public bool platingPrefabSet;
    public float modelScale;
    public float modelRotationY;
    public float modelRotationX;
    public float modelRotationZ;
    public float modelPositionX;
    public float modelPositionY;
    public float modelPositionZ;
}

[Serializable]
public class CustomRecipeUploadDto
{
    public string setName;
    public string recipeAssetPath;
    public string fileName;
    public string base64;
    /** 多文件上传（FBX/OBJ + PNG 贴图组合）：第一个模型文件作为主模型，其余为贴图等附属文件。 */
    public CustomRecipeUploadFileDto[] files;
}

[Serializable]
public class CustomRecipeReferenceEntryDto
{
    public string guid;
    public string id;
    /** Source recipe id owning this model ("" for standalone entries). */
    public string recipeId;
    public string nameZh;
    public string nameEn;
    public string assetPath;
}

[Serializable]
public class CustomRecipeReferencesDto
{
    public CustomRecipeReferenceEntryDto[] cookingSteps;
    public CustomRecipeReferenceEntryDto[] platingSteps;
    /** 装盘容器（盘子/杯子等 PlatingSteps 目录资产），运行时映射为 PlatingStepData。 */
    public CustomRecipeReferenceEntryDto[] platingContainers;
    public CustomRecipeReferenceEntryDto[] icons;
    public CustomRecipeReferenceEntryDto[] reusableModels;
    public string[] ingredients;
}

[Serializable]
public class CustomRecipeCategoryDeleteDto
{
    public string setName;
    public string category;
}

[Serializable]
public class CustomRecipeCategoryCreateDto
{
    public string setName;
    public string id;
    public string zh;
    public string en;
}

[Serializable]
public class CustomRecipeCategoryRenameDto
{
    public string setName;
    public string oldId;
    public string newId;
    public string newZh;
    public string newEn;
}

// ==================== Audio export manifest ====================

[Serializable]
public class AudioExportManifestDto
{
    public string generatedAt;
    public AudioExportBgmDto[] bgm;
    public AudioExportSfxDirDto[] sfx;
    public AudioExportAmbienceDto[] ambiences;
}

[Serializable]
public class AudioExportBgmDto
{
    public string guid;
    public string id;
    public string nameZh;
    public string filename;
}

[Serializable]
public class AudioExportSfxDirDto
{
    public string id;
    public string nameZh;
    public AudioExportSfxClipDto[] clips;
}

[Serializable]
public class AudioExportSfxClipDto
{
    public string tag;
    public string type; // "oneshot", "looping", "looping_start", "looping_end"
    public string filename;
}

[Serializable]
public class AudioExportAmbienceDto
{
    public string tag;
    public bool found;
    public string filename;
    public string dirId;
}
