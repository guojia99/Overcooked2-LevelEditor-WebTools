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
    /// <summary>格数（1 格 = 1.2m；半格小型厨具/工具为 0.5）。</summary>
    public float cellsX = 1f;
    public float cellsZ = 1f;
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

/// <summary>开关 → 目标对象的联动（断头台/果汁机/酱料机的按钮触发）。
///  id 约定同 Teleportal："u:<instanceID>"（既有场景对象）或文档 instanceId（"new:..."）。</summary>
/// <summary>启动环境一次性自检（/api/env/status）：前端启动时拉取一次，
///  据此判断各依赖是否可用（common03 / 音频导出 / 游戏 bundle / dump 清单等）。</summary>
[Serializable]
public class EnvStatusDto
{
    public bool ok;
    public int port;
    /** web 静态页面（dist）就绪。 */
    public bool staticDist;
    public int schemaVersion;
    /** recipe-knowledge 已加载。 */
    public bool knowledgeLoaded;
    /** 手册词典已加载。 */
    public bool dictionaryLoaded;
    /** 音频导出清单（audio-exports/audio-exports.json）存在。 */
    public bool audioExports;
    /** 已导出的 ogg 数量（无导出时为 0）。 */
    public int audioExportClips;
    /** 游戏 bundle 目录（StreamingAssets/Windows）存在。 */
    public bool gameBundles;
    /** 游戏 bundle 数量（不含 .meta）。 */
    public int gameBundleCount;
    /** dump_bundle/manifest.json（bundle 分析依赖）存在。 */
    public bool dumpManifest;
}

[Serializable]
public class LayoutSwitchLinkDto
{
    /** 开关对象（PseudoPrefabSwitchStub 所在对象；缺失时导入端自动补组件）。 */
    public string switchId;
    /** 被触发的目标对象（如断头台/饮料机/酱料机）。 */
    public string targetId;
    /** 触发消息名（TriggerOnObject.m_triggerToFire；空时导入端默认 "Switch"）。 */
    public string trigger;
}

[Serializable]
public class LayoutPressureSwitchStubDto
{
    public string occupiedMaterialGuid;
    public string unoccupiedMaterialGuid;
}

/// <summary>按钮/压力开关 → 移动组联动（ButtonLinkBakery 烘焙为 Design/Button Logic
///  下的隐藏 Animator 逻辑物体）。
///  顺序触发：每次按压按 groupNames 顺序启动下一组（循环）；
///  lockUntilFinished：组运行期间忽略按压（组完成后才接受下一次）；
///  共轭对：两条 link 共享 pairId（一对一，各方最多 2 组），按下时本方各组同时启动，
///  全部完成后对方按钮抬起、本方按下（反之亦然）。</summary>
[Serializable]
public class LayoutButtonLinkDto
{
    public string id;
    /** 触发源物品 id（Switch / PressureSwitch；"u:<instanceID>" 或 "new:..."）。 */
    public string sourceId;
    /** 按顺序触发的移动组 displayName 列表（displayName 为跨保存稳定键）。 */
    public string[] groupNames;
    /** true = 组运行期间忽略按压（完成后才接受下一次按压）。 */
    public bool lockUntilFinished = true;
    /** 共轭对 id（两条 link 共享；空 = 非共轭）。 */
    public string pairId;
    /** 共轭对中本按钮初始为抬起（可按）状态。 */
    public bool pairStartsUp;
}

[Serializable]
public class LayoutButtonLinkDataDto
{
    public LayoutButtonLinkDto[] links;
}

/// <summary>按钮 ↔ 事件组联动（ButtonEventBakery 烘焙为 Design/Button Logic 下的
///  隐藏 Animator 逻辑物体）。
///  顺序广播：每次按压向下一事件组广播全部事件（最后一组后循环回第一组）；
///  组内全部事件完成（doneTrigger，未配置 = 立即完成）后才接受下一次按压。</summary>
[Serializable]
public class LayoutButtonEventDto
{
    /** 目标物品 id（"u:<instanceID>" 或 "new:..."）。 */
    public string targetId;
    /** 广播给目标的触发消息（如 switch_dlc08_drink_machine_1）。 */
    public string trigger;
    /** 目标完成事件时广播的触发消息（空 = 该事件立即视为完成）。 */
    public string doneTrigger;
}

[Serializable]
public class LayoutButtonEventGroupDto
{
    public string id;
    public LayoutButtonEventDto[] events;
}

[Serializable]
public class LayoutButtonEventLinkDto
{
    public string id;
    /** 触发源物品 id（Switch / PressureSwitch）。 */
    public string sourceId;
    public LayoutButtonEventGroupDto[] groups;
}

[Serializable]
public class LayoutButtonEventDataDto
{
    public LayoutButtonEventLinkDto[] links;
}

[Serializable]
public class LayoutTerminalStubDto
{
    /** "u:<instanceID>" of the pilotable GameObject, or empty. */
    public string pilotableObjectInstanceId;
}

/// <summary>大炮参数（dlc08/dlc09 cannon）。
/// freeRotation=true → PilotRotation 限位写为 ±180°（360° 自由旋转）；
/// 缺省/未配置 = 不改动 prefab 自带限位（固定小角度 ±45°）。</summary>
[Serializable]
public class LayoutCannonStubDto
{
    public bool freeRotation;
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

/// <summary>火锅灶台定时开关（开局自动循环：开 onSeconds 秒 → 关 offSeconds 秒；
/// 关闭期 CookingRegion.enabled=false，锅具不烹饪、火焰熄灭）。
/// 组件随场景烘焙到灶台伪根（LayoutRuntimeTimedCookingSwitch，游戏程序集）。</summary>
[Serializable]
public class LayoutTimedSwitchDto
{
    /** false = 配置保留但不生效（灶台常开）。 */
    public bool enabled = true;
    /** 开启期秒数（最小 3）。 */
    public float onSeconds = 30f;
    /** 关闭期秒数（最小 3）。 */
    public float offSeconds = 30f;
    /** 初始相位为开启。 */
    public bool startOn = true;
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
    public float localRotationZ;
    public LayoutVector3 localScale;
    /** 空气墙碰撞盒中心（局部坐标）。空气墙导出时携带，写回时还原 col.center。 */
    public LayoutVector3 colliderCenter;
    public LayoutFootprint footprint;
    /** True for surface-floor prefabs (raft planks, ice_floor, ...) → generate a walkable Col_Floor under them. */
    public bool walkable;
    /** 空气墙（隐形碰撞块）：应用为 1×1×1.132 的 BoxCollider（1.132 为魔法数，导出据此识别），不生成 Col_Floor。 */
    public bool airWall;
    /** Dispenser | AttachingFoodSpawner | Conveyor | Teleportal | CookingUtensil | Travelator | Flamethrower | CleanPlateStack | Burner | Player | ServingStation | PlateReturn | GlassReturn | Switch | CannonSwitch | PressureSwitch | Terminal | empty */
    public string stubKind;
    /** Counter/Dispenser etc. appearance SO guid (base PseudoPrefabStub.pseudoPrefabSO). */
    public string pseudoPrefabGuid;
    public LayoutCannonStubDto cannon;
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
    /** 火锅灶台定时开关（cooking_region_floorburner / dlc10 变体）。 */
    public LayoutTimedSwitchDto timedSwitch;
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
    /** 开关联动（按钮 → 断头台/饮料机/酱料机等目标）。 */
    public LayoutSwitchLinkDto[] switchLinks;
    /** 按钮/压力开关 ↔ 移动组联动（仅全量写回携带）。 */
    public LayoutButtonLinkDataDto buttonLinks;
    /** 按钮 ↔ 事件组联动（仅全量写回携带）。 */
    public LayoutButtonEventDataDto buttonEvents;
    /** 游戏相机（背景色 / FOV；仅全量写回携带）。 */
    public CameraInfoDto cameraInfo;
    /** Art/Lights 下非 prefab 灯光（颜色/强度/范围/启用；仅全量写回携带）。 */
    public LightInfoDto[] lights;
}

/** 游戏相机信息：背景色与 FOV 可编辑，transform 为只读快照供前端绘制视野。 */
[Serializable]
public class CameraInfoDto
{
    /** "#rrggbb" — 运行时相机 clear 色（空洞主题无背景 prefab，即游戏背景色）。 */
    public string backgroundColor;
    public float fieldOfView;
    /** 只读：主相机世界位置与欧拉角快照（度）。 */
    public LayoutVector3 position;
    public float pitch;
    public float yaw;
    public float roll;
    public float nearClip;
    public float farClip;
}

/** Art/Lights 子树中非 prefab instance 的灯光（prefab 灯作为普通 item 往返，不在此列）。 */
[Serializable]
public class LightInfoDto
{
    /** 场景层级路径（如 "Art/Lights/day"），写回时按路径匹配。 */
    public string hierarchyPath;
    public string displayName;
    /** LightType 枚举值（0=Spot 1=Directional 2=Point 3=Area）。 */
    public int lightType;
    /** "#rrggbb" */
    public string color;
    public float intensity;
    public float range;
    public float spotAngle;
    public bool enabled;
    /** 只读：世界欧拉角快照（度）。 */
    public LayoutVector3 eulerAngles;
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
    /** 空气地板：仅有可行走 Col_AirFloor 碰撞盒（Ground 层，几何与普通 Col_Floor
     *  相同），无可见 Plane。导出时按名称 Col_AirFloor 识别，写回时不创建可见面。 */
    public bool airFloor;
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
    /** Mixed 类型自定义菜谱：先搅拌（MixingBowl）再烹饪（卡片显示双步骤）。 */
    public bool mixing;
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
    /** 食材级步骤角标（JsonUtility 不支持 Dictionary，用 pair 数组）：
     *  如炒饭的米 → Pot 煮锅角标。 */
    public RecipeIngredientStepDto[] ingredientSteps;
}

[Serializable]
public class RecipeIngredientStepDto
{
    /** 食材 asset id。 */
    public string ingredient;
    /** 该食材的步骤角标（如 "Pot"）。 */
    public string[] steps;
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
    /** 与 recipeGuids 对应的菜谱 id（文件名），前端据此按 id 兜底匹配勾选状态。 */
    public string[] recipeIds;
}

[Serializable]
public class LevelRecipesUpdateDto
{
    public string levelInfoAssetPath;
    public string[] recipeGuids;
}

// ---------- 通用内容菜谱库（内置菜谱管理，已废弃：改为 common03 直接引用 + 静态 JSON） ----------

[Serializable]
public class WebRecipeEntryDto
{
    /** 源库 guid（原 Import 源库；现 common03 资产）。 */
    public string guid;
    public string id;
    public string nameZh;
    public string nameEn;
    public string assetPath;
    public string cookingStep;
    /** 去重后的叶食材 id 列表。 */
    public string[] ingredients;
    /** 难度估算分（20×食材+加成，20~120）。 */
    public int score;
    public string type;
    /** 去重键（规范化中文名，去 DLC 后缀）。 */
    public string dupKey;
    /** 是否为本菜组代表（同组内取最高 DLC）。 */
    public bool representative;
    /** 本关卡集 custom_web 是否已有副本。 */
    public bool installed;
    /** 已装副本 guid（未装为空）。 */
    public string installedGuid;
    /** 是否被本关卡集任一关卡引用。 */
    public bool referenced;
    /** 引用它的关卡名列表。 */
    public string[] referencedBy;
}

[Serializable]
public class WebRecipeLibraryDto
{
    public string setName;
    public WebRecipeEntryDto[] recipes;
}

[Serializable]
public class WebRecipeInstallDto
{
    public string setName;
    public string[] ids;
}

[Serializable]
public class WebRecipeUninstallDto
{
    public string setName;
    public string[] ids;
}

[Serializable]
public class WebRecipeUninstallResultDto
{
    public bool ok;
    public string error;
    /** 被引用时列出使用关卡（此时拒绝移除）。 */
    public string[] usedByLevels;
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
    public string[] availableAmbiences;
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

[Serializable]
public class SetExportStatusDto
{
    /** idle | running | done | error */
    public string status;
    public string setName;
    /** queued | prepare | clean | build | package | zip | done */
    public string phase;
    public string message;
    public string error;
    public string zipFileName;
    public int fileCount;
}

[Serializable]
public class SetExportStartDto
{
    public string setName;
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
    public int minOrderCount;
    public int maxOrderCount;
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
    public int minOrderCount;
    public int maxOrderCount;
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
    public string cookingStepIconId;
    public string platingStepId;
    public string mixingIconId;
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
    /** 模型原点偏移（模型节点 localPosition），旋转/缩放绕偏移后的原点。 */
    public float modelPivotX;
    public float modelPivotY;
    public float modelPivotZ;
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
    public float modelPivotX;
    public float modelPivotY;
    public float modelPivotZ;
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
    public float modelPivotX;
    public float modelPivotY;
    public float modelPivotZ;
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

/// <summary>模型上传结果：成功时附带 Unity 导入后的原始尺寸（不含配置变换），
///  供前端自动按 Unity 实际尺寸校准缩放/位置（three.js 预览尺寸可能与 Unity 不一致）。</summary>
[Serializable]
public class CustomRecipeUploadResultDto
{
    public bool ok;
    public string error;
    public float rawSizeX;
    public float rawSizeY;
    public float rawSizeZ;
    public float rawMinY;
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
