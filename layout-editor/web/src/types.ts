export interface CatalogStackMeta {
  /** Vertical local Y when parented under Design/Utensils (plate ≈ 1, pot ≈ 0.6). */
  y: number;
  hostRule:
    | "counter_standard"
    | "cooker"
    | "frying_station"
    | "mixer"
    | "blender"
    | "barbeque"
    | "campfire";
}

export interface CatalogItem {
  id: string;
  guid: string;
  assetPath: string;
  category: string;
  theme: string | null;
  nameZh: string;
  nameEn: string;
  defaultParent: string;
  footprint: { cellsX: number; cellsZ: number };
  /** core = gameplay; decor = art / NPC props; floor = floor/background surfaces */
  layoutTier?: "core" | "decor" | "floor";
  /** Floor-layer classification (set for floor/background catalog items). */
  surfaceTier?: "floor" | "background";
  surfaceKind?:
    | "solid"
    | "raft"
    | "ice"
    | "snow"
    | "sand"
    | "alien"
    | "walkway"
    | "carpet"
    | "section"
    | "ground"
    | "conveyor"
    | "decal"
    | "airwall"
    | "background";
  stack?: CatalogStackMeta;
  /** Intrinsic model height (measured renderer bounds size Y, world units).
   *  Flat floor tiles ~0.1; tall pieces (ice cliffs, blocks) 1+. Used by the
   *  floor palette's height-range filter. Undefined when not measured. */
  height?: number;
  /** True when an extracted icon PNG exists under web/public/icons/catalog/<id>.png. */
  icon?: boolean;
}

export interface CatalogPaletteGroup {
  key: string;
  labelZh: string;
  labelEn: string;
  layoutTier: "core" | "decor" | "floor";
  itemCount: number;
}

export interface Catalog {
  generatedAt: string;
  schemaVersion?: number;
  gridCellSize: number;
  itemCount: number;
  items: CatalogItem[];
  byCategory: Record<string, CatalogItem[]>;
  paletteGroups?: CatalogPaletteGroup[];
}

export interface LayoutVector3 {
  x: number;
  y: number;
  z: number;
}

export interface LayoutDispenserStub {
  spawnerItemPrefabGuid?: string;
}

export interface LayoutFoodSpawnerStub {
  spawnInOrder?: boolean;
  attachmentPrefabGuids?: string[];
  weights?: number[];
  triggerTime?: number;
  triggerAtStart?: boolean;
}

export interface LayoutConveyorStub {
  conveySpeed?: number;
}

export interface LayoutTeleportalStub {
  exitPortalInstanceId?: string;
  portalColor?: number;
  doubleSided?: boolean;
}

export interface LayoutCookingUtensilStub {
  capacity?: number;
  allowedIngredientGuids?: string[];
}

export interface LayoutTravelatorStub {
  speed?: number;
}

export interface LayoutFlamethrowerStub {
  cookingRate?: number;
}

export interface LayoutCleanPlateStackStub {
  plateCount?: number;
  platePrefabGuid?: string;
}

export interface LayoutBurnerStub {
  fireMode?: number;
  airTime?: number;
  randomTargetOrder?: boolean;
  hideVisual?: boolean;
}

export interface LayoutPlayerStub {
  playerID?: number;
}

export interface LayoutPlateReturnStub {
  /** True = return clean plates/glasses directly; false (default) = dirty ones. */
  returnClean?: boolean;
}

export interface LayoutServingStationStub {
  /** Legacy single binding (one PlateReturn). Kept for back-compat; superseded
   *  by plateReturnInstanceIds. */
  plateReturnInstanceId?: string;
  /** One-to-many binding to return stations (PlateReturn 脏盘台 and/or
   *  GlassReturn 脏杯台 — they share the same stub type). A serving station may
   *  bind to many returns; a return belongs to at most one serving station. */
  plateReturnInstanceIds?: string[];
}

export interface LayoutSwitchStub {
  startEnabled?: boolean;
  activeMaterialGuid?: string;
  inactiveMaterialGuid?: string;
}

export interface LayoutPressureSwitchStub {
  occupiedMaterialGuid?: string;
  unoccupiedMaterialGuid?: string;
}

export interface LayoutMeshWithMaterialStub {
  pseudoPrefabGuid?: string;
  materialGuid?: string;
}

export interface LayoutSOArrayStub {
  pseudoPrefabGuids?: string[];
}

export interface LayoutTerminalStub {
  pilotableObjectInstanceId?: string;
}

/** 大炮（dlc08/dlc09 cannon）参数。freeRotation=true → 360° 自由旋转（±180°）；
 *  缺省/未配置 = 固定小角度模式（±45°）。 */
export interface LayoutCannonStub {
  freeRotation?: boolean;
}

/** 火锅灶台定时开关（开局自动循环：开 onSeconds 秒 → 关 offSeconds 秒）。
 *  字段存在且 enabled!==false = 生效；关闭期灶台不加热、锅具不烹饪、火焰熄灭。 */
export interface LayoutTimedSwitchStub {
  enabled?: boolean;
  onSeconds?: number;
  offSeconds?: number;
  startOn?: boolean;
}

export interface LayoutItem {
  instanceId: string;
  hierarchyPath: string;
  prefabGuid: string;
  prefabAssetPath: string;
  parentPath: string;
  displayName: string;
  localPosition: LayoutVector3;
  worldPosition?: LayoutVector3;
  /** Euler X in degrees — quad-based floor tiles lie flat via x=90. */
  localRotationX?: number;
  localRotationY: number;
  localRotationZ?: number;
  /** 空气墙碰撞盒中心（局部坐标）。 */
  colliderCenter?: LayoutVector3;
  localScale?: LayoutVector3;
  footprint?: { cellsX: number; cellsZ: number };
  walkable?: boolean;
  stubKind?: string;
  /** 空气墙：隐形碰撞块。 */
  airWall?: boolean;
  dispenser?: LayoutDispenserStub;
  conveyor?: LayoutConveyorStub;
  teleportal?: LayoutTeleportalStub;
  foodSpawner?: LayoutFoodSpawnerStub;
  cookingUtensil?: LayoutCookingUtensilStub;
  travelator?: LayoutTravelatorStub;
  flamethrower?: LayoutFlamethrowerStub;
  cleanPlateStack?: LayoutCleanPlateStackStub;
  burner?: LayoutBurnerStub;
  player?: LayoutPlayerStub;
  servingStation?: LayoutServingStationStub;
  plateReturn?: LayoutPlateReturnStub;
  switchStub?: LayoutSwitchStub;
  pressureSwitch?: LayoutPressureSwitchStub;
  terminal?: LayoutTerminalStub;
  cannon?: LayoutCannonStub;
  /** Counter/Dispenser etc. appearance SO guid (base PseudoPrefabStub.pseudoPrefabSO). */
  pseudoPrefabGuid?: string;
  meshWithMaterial?: LayoutMeshWithMaterialStub;
  soArray?: LayoutSOArrayStub;
  /** 火锅灶台定时开关（cooking_region_floorburner / dlc10 变体）。 */
  timedSwitch?: LayoutTimedSwitchStub;
}

// ---------- Movable Object Control (Animated Objects groups) ----------

export interface MoveGroupWaypoint {
  id: string;
  /** World XZ of the waypoint. */
  x: number;
  z: number;
  /** Keyframe time in seconds inside the event clip (imported levels only). */
  hasTime?: boolean;
  t?: number;
  /** Dwell seconds after arriving at this waypoint before continuing (hold). */
  wait?: number;
  /** Seconds to travel from this waypoint to the next (default: event interval). */
  segmentSeconds?: number;
}

export interface MoveGroupEvent {
  id: string;
  /** "move" | "wait" | "lift" | "drop" — waits bake as pure queue delays (no
   *  controller state); lift/drop are pure Y clips (no route). */
  type: "move" | "wait" | "lift" | "drop";
  /** Auto-generated by the backend when empty ("Move1".."MoveN"). */
  triggerName?: string;
  /** Seconds between the previous trigger and this one (queue delays[i]). */
  delay: number;
  /** Seconds per waypoint segment inside this event's move clip. */
  intervalSeconds?: number;
  /** Ordered route (contiguous slice of the group waypoint list). */
  waypointIds?: string[];
  /** Loop the clip itself (loopTime=1, no exit transition) — scrolling patterns. */
  loop?: boolean;
  /** Ping-pong loop: move to the end of the route, then back (wrapMode=PingPong).
   *  Mutually exclusive with loop. Only honored on the last move event. */
  pingpong?: boolean;
  /** lift/drop: target Y the members rise/fall to (absolute; drop default 0). */
  yTo?: number;
  /** Vertical motion seconds: lift/drop clip duration, or the move event's rise time. */
  liftSeconds?: number;
  /** move events: lift height above the base before moving (0 = no lift). */
  liftHeight?: number;
  /** move events: seconds to lower back at the end (0 = stays up). */
  dropSeconds?: number;
}

export interface MoveGroupMemberOffset {
  instanceId: string;
  x: number;
  z: number;
  /** Phase shift in seconds (looping scroll members, time-shifted route). */
  t?: number;
  /** Bind this member to a specific waypoint: it holds at that point (+ offset)
   *  for the whole sequence instead of riding the route. */
  followWaypointId?: string;
  /** Plain-object member display name (imported levels; web display only). */
  displayName?: string;
  /** Full scene hierarchy path of the member (web shows its child items). */
  hierarchyPath?: string;
}

export interface MoveGroupMember {
  instanceId: string;
  /** Plain-object member display name (imported levels; web display only). */
  displayName?: string;
  /** Full scene hierarchy path of the member (web shows its child items). */
  hierarchyPath?: string;
}

/** A named container grouping some of the move group's members together.
 *  Organizational only — the flat member lists remain the bake's source of truth. */
export interface MoveGroupMemberGroup {
  id: string;
  name: string;
  /** Instance ids of members in this group (subset of item/floor/object ids). */
  memberInstanceIds: string[];
}

export interface MoveGroup {
  id: string;
  displayName: string;
  /** Unity instance ids ("u:xxx"/"new:xxx") of the items driven by this group. */
  itemInstanceIds: string[];
  /** Unity instance ids of the floor (Plane/Quad) objects driven by this group. */
  floorInstanceIds: string[];
  /** Unity instance ids of plain scene objects driven by this group (imported). */
  objectInstanceIds: string[];
  /** Per-member local offset from the route (parallel tracks; imported levels). */
  memberOffsets?: MoveGroupMemberOffset[];
  /** Members that hold still while the route moves. */
  memberStatic?: MoveGroupMember[];
  /** Seconds after round start before the queue begins (TriggerTimer). */
  startDelay: number;
  /** Loop the whole trigger queue (loopWhenFinished + loopDelay). */
  loop: boolean;
  loopDelay: number;
  /** Queue waits for the animator's finished trigger before advancing. */
  waitForFinished?: boolean;
  /** External trigger name that starts the queue (empty = auto start). */
  startTrigger?: string;
  /** External trigger name that cancels the queue. */
  cancelTrigger?: string;
  /** Trigger broadcast when the whole queue finishes (target = group root). */
  endTrigger?: string;
  /** Trigger name the animator emits on clip completion (default "AnimationFinished"). */
  finishedTrigger?: string;
  /** Animator.applyRootMotion. */
  applyRootMotion?: boolean;
  waypoints: MoveGroupWaypoint[];
  events: MoveGroupEvent[];
  /** User-created member groups (organizational containers; baked as sub-roots). */
  memberGroups?: MoveGroupMemberGroup[];
  /** Backend-owned: hierarchy path of the created "Animated Objects" group root. */
  groupHierarchyPath?: string;
}

export interface MoveControlData {
  groups: MoveGroup[];
}

export interface LayoutDocument {
  sceneAssetPath: string;
  items: LayoutItem[];
  /** Editable floor/background plane objects (scene-embedded). */
  floors?: FloorObject[];
  /** Read-only walkable collision rectangles. */
  walkable?: WalkableRect[];
  /** Read-only death configuration. */
  deathInfo?: DeathInfo;
  /** Movable object movement control (decor + items layer). */
  moveControls?: MoveControlData;
  /** 开关联动（按钮 → 断头台/饮料机/酱料机等目标）。
   *  id 约定同传送门："u:<instanceID>"（既有场景对象）或文档 instanceId（"new:..."）。 */
  switchLinks?: SwitchLink[];
  /** 按钮/压力开关 ↔ 移动组联动（顺序触发 / 运行期锁定 / 共轭对）。
   *  仅全量保存携带（引用移动组，与 moveControls 同策略）。 */
  buttonLinks?: ButtonLinkData;
  /** 按钮 ↔ 事件组联动（顺序广播多事件组 / 完成门控）。
   *  仅全量保存携带（引用场景物品）。 */
  buttonEvents?: ButtonEventData;
  /** 游戏相机（背景色 / FOV；仅全量保存携带）。 */
  cameraInfo?: CameraInfo | null;
  /** Art/Lights 非 prefab 灯光（颜色/强度/范围/启用；仅全量保存携带）。 */
  lights?: LightInfo[];
}

/** 游戏相机信息：背景色与 FOV 可编辑，transform 为只读快照（绘制视野用）。 */
export interface CameraInfo {
  /** "#rrggbb" — 相机 clear 色（空洞主题下即游戏背景色）。 */
  backgroundColor: string;
  fieldOfView: number;
  /** 只读：主相机世界位置与欧拉角快照（度）。 */
  position: LayoutVector3;
  pitch: number;
  yaw: number;
  roll: number;
  nearClip: number;
  farClip: number;
}

/** Art/Lights 子树中非 prefab instance 的灯光（prefab 灯作为普通 item 往返）。 */
export interface LightInfo {
  /** 场景层级路径（如 "Art/Lights/day"），写回按路径匹配。 */
  hierarchyPath: string;
  displayName: string;
  /** LightType 枚举值（0=Spot 1=Directional 2=Point 3=Area）。 */
  lightType: number;
  color: string;
  intensity: number;
  range: number;
  spotAngle: number;
  enabled: boolean;
  /** 只读：世界欧拉角快照（度）。 */
  eulerAngles: LayoutVector3;
}

export interface SwitchLink {
  /** 开关对象（按钮/Switch stub 所在物品）。 */
  switchId: string;
  /** 被触发的目标对象。 */
  targetId: string;
  /** 触发消息名（TriggerOnObject.m_triggerToFire；缺省后端按 "Switch" 处理）。 */
  trigger?: string;
}

// ---------- Button ↔ MoveGroup links（按钮/压力开关 联动移动组） ----------

/** 按钮/压力开关 → 移动组联动。
 *  顺序触发：每按一次按 groupNames 顺序启动下一组（最后一组后循环回第一组）；
 *  lockUntilFinished：组运行期间忽略按压（组完成后才接受下一次）。
 *  共轭对：两个 link 共享 pairId（一对一），每个按钮各绑 2 个移动组——
 *  按下时两组同时启动，两组全部完成后对方按钮抬起、本按钮按下（反之亦然）。 */
export interface ButtonLink {
  id: string;
  /** 触发源物品 instanceId（Switch / PressureSwitch）。 */
  sourceId: string;
  /** 按顺序触发的移动组 displayName 列表（displayName 是跨保存的稳定键）。 */
  groupNames: string[];
  /** true = 移动组运行期间忽略按压（完成后才接受下一次按压）。 */
  lockUntilFinished: boolean;
  /** 共轭对 id（两个 link 共享；空/缺省 = 非共轭）。 */
  pairId?: string;
  /** 共轭对中本按钮初始为抬起（可按）状态。 */
  pairStartsUp?: boolean;
}

export interface ButtonLinkData {
  links: ButtonLink[];
}

// ---------- 按钮事件组（按钮 → 多事件组顺序广播） ----------

/** 事件组内单条事件：按下按钮时对目标物品广播 trigger；
 *  doneTrigger 非空时目标完成该事件会广播 doneTrigger，组内全部事件完成后按钮才可再按。 */
export interface ButtonEvent {
  /** 目标物品 instanceId（"u:<instanceID>" 或文档 instanceId）。 */
  targetId: string;
  /** 广播给目标的触发消息（如 switch_dlc08_drink_machine_1）。 */
  trigger: string;
  /** 目标完成事件时广播的触发消息（可选；不配 = 该事件立即视为完成）。 */
  doneTrigger?: string;
}

export interface ButtonEventGroup {
  id: string;
  events: ButtonEvent[];
}

/** 按钮 ↔ 事件组联动：每次按压按顺序广播下一事件组的全部事件（最后一组后循环回第一组）；
 *  组内全部事件完成（doneTrigger 或立即完成）后才接受下一次按压。 */
export interface ButtonEventLink {
  id: string;
  /** 触发源物品 instanceId（Switch / PressureSwitch）。 */
  sourceId: string;
  groups: ButtonEventGroup[];
}

export interface ButtonEventData {
  links: ButtonEventLink[];
}

export type SurfaceKind =
  | "solid"
  | "ice"
  | "snow"
  | "sand"
  | "alien"
  | "walkway"
  | "carpet"
  | "section"
  | "conveyor"
  | "background";

export interface FloorObject {
  instanceId: string;
  hierarchyPath: string;
  parentPath: string;
  displayName: string;
  surfaceKind: string;
  meshType: "plane" | "quad" | "prefab";
  meshFileId?: number;
  materialGuid?: string;
  materialAssetPath?: string;
  materialName?: string;
  localPosition: LayoutVector3;
  worldPosition: LayoutVector3;
  localRotationY: number;
  localScale: LayoutVector3;
  widthUnits: number;
  depthUnits: number;
  widthCells: number;
  depthCells: number;
  prefabGuid?: string;
  prefabAssetPath?: string;
  /** Themed floor: exact prefab-instance scale/rotation captured at merge time,
   *  reused on write-back while the rect size is unchanged (official scenes use
   *  non-cell-aligned scales like 4×2.5m — quantizing them would shift visuals). */
  prefabScale?: LayoutVector3;
  prefabScaleCellsW?: number;
  prefabScaleCellsD?: number;
  /** Euler X of the source prefab instance (default 90 for flat quads). */
  prefabRotX?: number;
  /** Optional manual tint color (hex) for a solid floor — a user-driven recolor
   *  feature independent of the floor's material / surface kind. */
  tintColor?: string;
  /** Whether the tint is active. When false the floor uses its real material
   *  even if tintColor is set (so you can switch materials without the tint
   *  overriding). */
  tintEnabled?: boolean;
  /** Image floor: a solid Plane textured by an image uploaded into the level's
   *  data dir (Assets/LevelSets/<set>/data/). Set together with imageMode. */
  imageTexturePath?: string;
  /** Image tiling mode: "tile" repeats the image once per cell; "stretch"
   * stretches one copy across the whole floor rect; "warp" perspective-fits the
   * image to the game camera frustum so it shows undistorted on screen (mesh
   * is baked per camera; the floor's w/d cells only size its walk collider). */
  imageMode?: "tile" | "stretch" | "warp";
  /** Image opacity 0..1 (default 1 = fully opaque). */
  imageOpacity?: number;
  /** Image rotation in degrees, snapped to 90° steps (0/90/180/270, clockwise
   *  viewed from above). Default 0. */
  imageRotation?: number;
  /** 空气地板：不可见，仅可行走。 */
  airFloor?: boolean;
}

export interface WalkableRect {
  surfaceType: "solid" | "ice";
  cx: number;
  cz: number;
  sx: number;
  sz: number;
  sourcePath?: string;
}

export interface KillPlaneInfo {
  hierarchyPath: string;
  respawnType: string;
  deathEffectName: string;
  cx: number;
  cz: number;
  sx: number;
  sz: number;
}

export interface DeathInfo {
  deathType: "water" | "goo" | "fall";
  deathEffectName: string;
  killPlanes: KillPlaneInfo[];
}

export interface FloorMaterial {
  guid: string;
  id: string;
  assetPath: string;
  nameZh: string;
  sizeTag?: string;
  /** Level set name or "common01"/"common02" (static floor-materials.json only). */
  source?: string;
}

export interface FloorMaterialCatalog {
  materials: FloorMaterial[];
}

export interface GridInfo {
  found: boolean;
  worldPosition: LayoutVector3;
  cellSize: LayoutVector3;
  gridHalfSizeX: number;
  gridHalfSizeZ: number;
  origin: number;
}

export interface LevelSetScene {
  assetPath: string;
  levelSet: string;
  sceneName: string;
}

export type FoodGroup = "core" | "custom" | "dlc02" | "dlc05" | "levelset" | "web" | string;

export interface IngredientEntry {
  guid: string;
  id: string;
  nameZh: string;
  nameEn?: string;
  assetPath: string;
  group?: FoodGroup;
  icon?: boolean;
  /** node 型食材（SO 目标是 bundle 内 OrderDefinitionNode 而非 .prefab）：
   *  无实体 prefab，食材箱/食材生成器无法生成，只能经加工或专属机器产出。 */
  nodeOnly?: boolean;
}

export interface RecipeEntry {
  guid: string;
  id: string;
  nameZh: string;
  nameEn?: string;
  assetPath: string;
  cookingStep?: string;
  /** 装盘容器 id（Plate / Glass / …），用于关卡编辑器推断容器堆。 */
  platingStep?: string;
  ingredients?: string[];
  /** Direct composition ids for custom recipes (sub-recipe ids and/or ingredient ids). */
  compositionIds?: string[];
  /** 工序分组（构建期生成；web/custom 菜谱用于菜谱卡与锅具装填）。 */
  cookingGroups?: RecipeCookingGroup[];
  ingredientCount?: number;
  cookingStepCount?: number;
  score?: number;
  isCustom?: boolean;
  group?: FoodGroup;
  /** Recipe family: burger / pizza / sushi / kebab / smoothie / … */
  type?: string;
  /** score-0 半成品（面糊/炸物部件/自选披萨部件），不可作为关卡菜谱 */
  intermediate?: boolean;
  /** Mixed 类型自定义菜谱：先搅拌（MixingBowl）再烹饪（卡片显示双步骤）。 */
  mixing?: boolean;
  icon?: boolean;
}

export interface CookingStepEntry {
  guid: string;
  id: string;
  nameZh: string;
  nameEn?: string;
  kind: "cooking" | "plating";
  assetPath: string;
  bundleName?: string;
  group?: FoodGroup;
}

export interface CookingStepCatalog {
  cookingSteps: CookingStepEntry[];
}

export interface DirectoryEvent {
  id: string;
  eventsZh: string[];
  desc: string;
}

export interface AudioTheme {
  key: string;
  directories: string[];
  ambiences: string[];
  bgm: string[];
  deathTheme: string;
}

export interface DeathTheme {
  key: string;
  effectIdHint: string;
  note: string;
}

export interface AmbienceLabel {
  name: string;
  zh: string;
}

export interface AudioItemRule {
  items: string[];
  theme?: string;
  directories?: string[];
  ambiences?: string[];
  labelZh: string;
}

export interface AudioKnowledge {
  baseBundles: string[];
  alwaysLoadedBundles: string[];
  mandatoryDirectoryIds: string[];
  /** 实际存在于至少一个 AudioDirectoryData 的 GameLoopingAudioTag；其余枚举值无音频资源，选了运行时会崩溃 */
  availableAmbiences?: string[];
  directoryEvents: DirectoryEvent[];
  themes: AudioTheme[];
  deathThemes: DeathTheme[];
  ambienceLabels: AmbienceLabel[];
  itemAudioRules: AudioItemRule[];
}

export interface AudioCatalog extends AudioKnowledge {
  music: MusicEntry[];
  audioDirectories: AudioDirectoryEntry[];
  deathEffects: DeathEffectEntry[];
}

export interface BundleAnalysis {
  base: string[];
  alwaysLoaded: string[];
  required: string[];
  current: string[];
  missing: string[];
  extras: string[];
}

export interface LevelRecipes {
  levelInfoAssetPath: string;
  levelName: string;
  recipeGuids: string[];
  /** 与 recipeGuids 对应的菜谱 id（文件名），前端据此按 id 兜底匹配勾选状态。 */
  recipeIds?: string[];
}

// ---------- Level admin (sets / levels / audio) ----------

export interface MusicEntry {
  guid: string;
  id: string;
  assetPath: string;
  bundleName: string;
  nameZh: string;
}

export interface MusicCatalog {
  music: MusicEntry[];
}

export interface AudioDirectoryEntry {
  guid: string;
  id: string;
  assetPath: string;
  bundleName: string;
  nameZh: string;
}

export interface AudioDirectoryCatalog extends AudioKnowledge {
  audioDirectories: AudioDirectoryEntry[];
}


export interface AmbienceCatalog {
  ambiences: string[];
  ambienceLabels: AmbienceLabel[];
}

export interface DeathEffectEntry {
  guid: string;
  id: string;
  assetPath: string;
  nameZh: string;
}

export interface DeathEffectCatalog {
  deathEffects: DeathEffectEntry[];
}

// ---------- Audio export manifest ----------

export interface AudioExportManifest {
  generatedAt: string;
  bgm: AudioExportBgm[];
  sfx: AudioExportSfxDir[];
  ambiences: AudioExportAmbience[];
}

export interface AudioExportBgm {
  guid: string;
  id: string;
  nameZh: string;
  filename: string;
}

export interface AudioExportSfxDir {
  id: string;
  nameZh: string;
  clips: AudioExportSfxClip[];
}

export interface AudioExportSfxClip {
  tag: string;
  type: "oneshot" | "looping" | "looping_start" | "looping_end";
  filename: string;
}

export interface AudioExportAmbience {
  tag: string;
  found: boolean;
  filename?: string;
  dirId?: string;
}

export interface LevelSetInfo {
  setName: string;
  assetPath: string;
  dataDir: string;
  levelSetName: string;
  levelSetNameZH: string;
  author: string;
  version: string;
  uid: string;
  levelCount: number;
}

export interface LevelSetList {
  sets: LevelSetInfo[];
}

/** 关卡集导出任务状态（GET /api/set/export/status，构建期间由桥接监听线程直答）。 */
export interface SetExportStatus {
  status: "idle" | "running" | "done" | "error";
  setName: string;
  /** queued | prepare | clean | build | package | zip | done */
  phase: string;
  message: string;
  error: string;
  zipFileName: string;
  fileCount: number;
}

export interface LevelSummary {
  assetPath: string;
  dataDir: string;
  levelName: string;
  levelNameZH: string;
  sceneName: string;
  sceneAssetPath: string;
  hasScreenshot: boolean;
  /** Asset path of the screenshot texture ("" when none), serve via api.imageFloorUrl. */
  screenshotPath?: string;
  hasScene: boolean;
}

export interface LevelList {
  levels: LevelSummary[];
}

export interface PerPlayerConfig {
  exists: boolean;
  orderLifeTime: number;
  timeBetweenOrders: number;
  plateReturnTime: number;
  survivalTimeMultiplier: number;
  roundTime: number;
  oneStarScore: number;
  twoStarScore: number;
  threeStarScore: number;
  fourStarScore: number;
}

export interface AudioConfig {
  inLevelMusicGuid: string;
  inLevelMusicId: string;
  ambiences: string[];
  audioDirectoryGuids: string[];
  audioDirectoryIds: string[];
  onDeathEffectGuid: string;
  onDeathEffectId: string;
}

export interface LevelDetail {
  levelInfoAssetPath: string;
  levelName: string;
  levelNameZH: string;
  sceneName: string;
  sceneAssetPath: string;
  hasScreenshot: boolean;
  /** Asset path of the screenshot texture ("" when none), serve via api.imageFloorUrl. */
  screenshotPath?: string;
  debugRecipeCount: number;
  disableDynamicParenting: boolean;
  minOrderCount: number;
  maxOrderCount: number;
  dependencies: string[];
  configs: PerPlayerConfig[];
  audio: AudioConfig;
}

// ---------- Custom Recipe Management ----------

export interface CustomRecipeCategory {
  id: string;
  zh: string;
  en: string;
}

export interface CustomRecipeConfig {
  uidPrefix: number;
  nextSequence: number;
  categories: CustomRecipeCategory[];
}

export interface CustomRecipeSummary {
  guid: string;
  id: string;
  assetPath: string;
  recipeName: string;
  nameZh: string;
  nameEn: string;
  uID: number;
  score: number;
  category: string;
  type: string;
  /** Direct composition ids (ingredient ids and/or sub-recipe ids). */
  compositionIds: string[];
  /** Leaf ingredients, recursively expanded through sub-recipes. */
  ingredients: string[];
  /** Composition-aware cooking groups for the recipe-card UI. */
  cookingGroups?: RecipeCookingGroup[];
  /** True when score <= 0 (半成品), used for badges and the sub-recipe picker. */
  intermediate: boolean;
  /** "core" / "custom" / "dlc02" / "dlc05" / "levelset". */
  group?: string;
  cookingStepId: string;
  cookingStepIconId?: string;
  platingStepId: string;
  mixingIconId?: string;
  hasIcon: boolean;
  hasModel: boolean;
  /** 模型在游戏中的缩放/旋转/位置。 */
  modelScale: number;
  modelRotationY: number;
  modelRotationX: number;
  modelRotationZ: number;
  modelPositionX: number;
  modelPositionY: number;
  modelPositionZ: number;
  /** 模型原点偏移（模型节点 localPosition，Unity 单位）：旋转/缩放绕偏移后的原点。 */
  modelPivotX?: number;
  modelPivotY?: number;
  modelPivotZ?: number;
  /** Unity 导入后的模型包围盒（含配置变换；minY 为底面高度），
   *  用于按 Unity 实际尺寸校准自动适配（three.js 预览尺寸可能与 Unity 不一致）。 */
  boundsMinY?: number;
  boundsSizeX?: number;
  boundsSizeY?: number;
  boundsSizeZ?: number;
}

/** One ingredient group of a recipe card: ingredients cooked together in the
 *  same step (step = "" means a raw group with no utensil). */
export interface RecipeCookingGroup {
  step: string;
  utensils: string[];
  ingredients: string[];
}

export interface CustomRecipeEdit {
  setName?: string;
  assetPath?: string;
  recipeName: string;
  nameZh: string;
  nameEn: string;
  category: string;
  score: number;
  type: string;
  compositionIds: string[];
  cookingStepId: string;
  cookingStepIconId: string;
  platingStepId: string;
  mixingIconId: string;
  modelPrefabId: string;
  cookingProgress: number;
  mixingProgress: number;
  modelScale: number;
  modelRotationY: number;
  modelRotationX: number;
  modelRotationZ: number;
  modelPositionX: number;
  modelPositionY: number;
  modelPositionZ: number;
  modelPivotX: number;
  modelPivotY: number;
  modelPivotZ: number;
}

export interface CustomRecipeReferenceEntry {
  guid: string;
  id: string;
  /** Source recipe id owning this model ("" for standalone entries). */
  recipeId?: string;
  nameZh: string;
  nameEn: string;
  assetPath: string;
}

export interface CustomRecipeReferences {
  cookingSteps: CustomRecipeReferenceEntry[];
  platingSteps: CustomRecipeReferenceEntry[];
  /** 装盘容器（盘子/杯子等）。 */
  platingContainers: CustomRecipeReferenceEntry[];
  icons: CustomRecipeReferenceEntry[];
  reusableModels: CustomRecipeReferenceEntry[];
  ingredients: string[];
}

// ---------- Counter Appearance ----------

export interface CounterAppearanceOption {
  guid: string;
  id: string;
  assetPath: string;
  nameZh: string;
  nameEn: string;
  theme: string;
  themeName: string;
}

export interface CounterAppearanceCatalog {
  generatedAt: string;
  schemaVersion?: number;
  typeNames: Record<string, string>;
  themeNames: Record<string, string>;
  byType: Record<string, CounterAppearanceOption[]>;
}

export interface SwitchMaterialOption {
  guid: string;
  id: string;
  assetPath: string;
  nameZh: string;
  nameEn: string;
}

export interface SwitchMaterialCatalog {
  generatedAt: string;
  schemaVersion?: number;
  materials: SwitchMaterialOption[];
}

// ---------- Icon status ----------

export interface IconStatusItem {
  id: string;
  nameZh: string;
  nameEn: string;
  hasIcon: boolean;
  group: string;
  type: string;
}

export interface IconStatusList {
  missingRecipes: IconStatusItem[];
  missingIngredients: IconStatusItem[];
  totalRecipes: number;
  totalIngredients: number;
  recipesWithIcon: number;
  ingredientsWithIcon: number;
}
