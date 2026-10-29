# 04 · 素材库 Assets/common 系列

> 目录：`Assets/common`、`common01`、`common02`、`common03`、`commonW1`、`commonW2`
> 一句话定位：编辑器的**游戏底层素材依赖层**——本地只放轻量"索引 SO + 空壳 wrapper prefab"，真实美术资源运行时从 7.6GB 原版游戏 bundle 加载；编辑器自制内容经 `Assets/AssetBundles/` 打包分发。
> 返回 [00-架构总览.md](00-架构总览.md)。本文所有 YAML/代码片段均为**文件原文摘录**，guid 均已对账验证。

---

## 1. 总体架构：pseudo + 原版包双层

```mermaid
flowchart LR
    subgraph LOCAL["本地工程（git 追踪，轻量）"]
        SO["PseudoPrefabSO（索引 SO）<br/>prefabName / bundleName / assetPath<br/>三字段三元组"]
        WRAP["wrapper prefab（空壳占位）<br/>Transform + Stub 派生(数据) + PseudoPrefab(行为)<br/>无 Mesh / 无材质"]
    end
    subgraph RUNTIME["运行时（按需加载，重量级）"]
        SA["Assets/StreamingAssets/Windows<br/>原版游戏 bundle 7.6GB<br/>（bundle47 单文件 50.9MB…）"]
        REAL["真实 prefab（模型/碰撞/游戏逻辑组件）"]
    end
    SO -- "bundleName 定位 bundle<br/>assetPath 定位资产" --> SA
    WRAP -- "ResetChild()：Instantiate 为 childGameObject" --> REAL
    REAL --> SA
```

### 1.1 PseudoPrefabSO：三字段索引（体系的核心）

脚本本体 `Assets/Scripts/LevelEditorStub/PseudoPrefabSO.cs` 全文仅 26 行：

```csharp
public class PseudoPrefabSO : ScriptableObject {
    [SerializeField] public string prefabName;
    [SerializeField] public string bundleName;
    [SerializeField] public string assetPath;

    private void OnValidate()
    {
        assetPath = assetPath.Trim('"').Replace("\\", "/");
        string prefix = @"E:/dev/test/ReversedGame/ExportedProject/";
        if (assetPath.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
            assetPath = assetPath.Substring(prefix.Length);
    }
}
```

| 字段 | 用途 | 消费方 |
|---|---|---|
| `prefabName` | 实例化后子物体命名 + 目录/画布显示名 | `PseudoPrefab.ResetChild()`（`Assets/Scripts/LevelEditor/PseudoPrefab.cs:41`） |
| `bundleName` | 原版包字典键 | `PseudoPrefabManager.GetAssetBundle()` |
| `assetPath` | 包内资产路径 | `bundle.LoadAsset(assetPath)` |

两个关键物证：
- **`OnValidate` 硬编码剥离 `E:/dev/test/ReversedGame/ExportedProject/` 前缀**——整套 SO 是作者直接在 AssetRipper 导出工程上批量制作的直接证据（这也解释了存量文件里遗留的 Windows 反斜杠）。
- **名实分离是合法的**：`PlayerSO.prefabName: player` 但 `assetPath: .../MultiplayerAvatar.prefab`——prefabName 不可从 assetPath 反推，`LayoutEditorCatalogApi.IngredientCatalogId`（:77-91）为此专门写了一套 canonical id 规则。

派生类：`PseudoPrefabSORecipe`（仅加 `public int score`，菜谱 SO 用）；`PseudoPrefabSOArray`（组件，挂 SO 数组，用于随机箱候选/换肤/多原料机器）。

### 1.2 wrapper prefab：三组件空壳

以 `Assets/common01/prefabs/counters/Bin.prefab`（67 行）为例，完整结构：

```yaml
--- !u!1 &1554132891853676
GameObject:
  m_Name: Bin
  m_Component:
  - component: {fileID: 4341917854781290}        # Transform (0,0,0) 无旋转
  - component: {fileID: 114096038827637246}      # PseudoPrefabStub（数据）
  - component: {fileID: 114967938430519454}      # LevelEditor.PseudoPrefab（行为）
--- !u!114 &114096038827637246
MonoBehaviour:
  m_Script: {fileID: 11500000, guid: 0f66cc8b36034eb4c8eec31e1994e471, type: 3}  # PseudoPrefabStub
  pseudoPrefabSO: {fileID: 11400000, guid: 8cb0c2b73ce3c284aada830bf2d4cf6a, type: 2}  # → BinSO.asset
--- !u!114 &114967938430519454
MonoBehaviour:
  m_Script: {fileID: 11500000, guid: d58b99f9c4313714e9c4b11f1534ae6f, type: 3}  # PseudoPrefab
  childGameObject: {fileID: 0}
```

要点：
- wrapper = **空 GameObject + Stub 派生组件（数据）+ PseudoPrefab 派生组件（行为）**，无 Mesh/材质。真实模型、碰撞、游戏逻辑全在原版 bundle 的真 prefab 上，`ResetChild()` 时 `Instantiate` 成 `childGameObject`。
- **复用的内部 fileID**：抽查 27 个 wrapper，`m_RootGameObject` 只有两个值——`1554132891853676`（18 个）与 `1382526047942262`（9 个）。即全部 wrapper 是从**两个种子模板**批量复制出的（配合 §9.1 的 BatchCreatePseudoPrefab 菜单）。
- **Y 位置约定**：476 个 wrapper 中仅 28 个 `m_LocalPosition` 非零（`AttachingFoodSpawner` 与两个气闸门 `y: 0.5` 挂点抬半格；`Sky.prefab` 等美术件带绝对坐标）。占位体永远在格点，子物体由 `EditorGridSnap` 吸附。

`Stub` 基类一行注释道破这套体系的契约（`Assets/Scripts/LevelEditorStub/Stub.cs`）：

```csharp
public class Stub : MonoBehaviour
{
    // for BepInEx plugin to find all istub and add the actual functional class
}
```

即 Stub 是**数据宿主标记**：编辑器侧由 `PseudoPrefab`（ExecuteInEditMode）实例化真 prefab 做预览；真机侧由 BepInEx 插件链（OC2DIYLevel + OC2LevelRuntimeLoader）找到 Stub 补挂真实功能类。**几十 KB 的场景文件因此能驱动 GB 级内容**。

### 1.3 运行时加载机制

`Assets/Scripts/LevelEditor/PseudoPrefabManager.cs:429`：

```csharp
string path = Path.Combine(Application.streamingAssetsPath, "Windows/" + assetBundleName).Replace("\\", "/");
...
AssetBundle assetBundle = AssetBundle.LoadFromFile(path);
```

先加载 `Windows`（AssetBundleManifest），再按 `LevelInfoSO.dependencies` 递归拉依赖 bundle。**pseudo SO 的 bundleName/assetPath 全部指向这套原版包**——编辑器里所见即玩家机器上已有资源，自建包里不需要任何美术。

### 1.4 为什么这么设计（证据）

1. **体积**：原版包 936 项 7.6GB（git 忽略，tutorial 第 2 步让用户自己从游戏目录拷入）。若把 mesh/贴图拷进工程自建 bundle，等于重打包整个游戏。
2. **工作流**：作者直接在 AssetRipper 导出树上操作（OnValidate 前缀剥离是物证），SO 的 assetPath 就是从那里复制的。
3. **跟随上游**：原版包永远是"玩家机器上已有资源"，游戏更新/换版本时索引库不需要重做美术，只调三元组。
4. **代价**（数据清洗成本）：SO 必须精确命中 bundle 内实名——`assetPath` 是**协议字段**，大小写敏感（Unity `LoadAsset`），工程里为此付出大量白名单与自愈代码（§10）。

---

## 2. 六个目录的定位与差异

| 目录 | 定位 | bundle 标记 | 规模 |
|---|---|---|---|
| **common** | 最小公共引导包（自定义关卡会话底座，4 资产） | `common` | 772KB |
| **common01** | 原版本体代理库（+ 主线主题 + 早期 DLC；唯一含 Player/game_environment/animations） | `common01`（textures 子目录独立成 `common01_textures`） | 47MB / 523 prefab / 580 SO |
| **common02** | 季节 DLC 追加代理库（dlc02 沙滩、dlc05 露营为主；独有 RecipeMatchList/story_meals/大量音频索引） | `common02` | 9.3MB / 404 prefab / 638 SO |
| **common03** | 后期 DLC 大合集（**上游正式版只读镜像**，2026-09 对齐上游重构） | `common03`（仅根 meta） | 35MB / 1296 prefab / 1376 SO |
| **commonW1** | Web 版 + 编辑器自制**增量层**（火锅/问号/背景/材质/RandomDispenser/decor） | `commonW1`（五个顶层 meta 显式标记） | 24MB / 352 prefab / 241 SO / 309 mat |
| **commonW2** | Burger 大全共享库（纯自定义菜谱数据） | `commonW2` | 5.7MB / 69 配方 / 14 模型 prefab |
| **commonW3** | Web 扩展菜谱共享库（沙拉 / 果汁 / 炒饭 / 汤粥 / 冰淇淋 / 布丁 / 冰沙） | `commonW3` | 67 道（39 已有 + batch1 28） |
| **common04** | 上游自定义内容模板与 supplies（食材/工作台/锅具/托盘，含 UI/PFX/PlatingStepData） | `common04` | 上游 2026-10 新增；随依赖包分发 |
| **commonW4** | Web 储物背包等机制增量 | `commonW4` | 本仓库增量层 |
| **commonW5** | Web 增量库：OC1/AYCE 主题 BGM · 桌台皮肤（丛林/生日）· 食材（辣椒/茶叶/葱）· 卷饼菜谱（Web_Burrito_*） | `commonW5` | 本仓库增量层 |

> 分工速记：**common01/02 = 原版一代代理**（category-first）；**common03 = 上游正式版镜像**；**commonW1 = 上游没有/已删的编辑器增量**（dlc-first）；**commonW2/W3 = 纯数据包**。"镜像 vs 增量"的分离让同步上游变成机械操作（`b499bd15e` + `a66d607b5` 两个提交完成一次全量对齐）。

### 2.1 commonW3 —— Web 扩展菜谱

| 分类目录 | 生成脚本 | 说明 |
|---|---|---|
| `custom_recipes/salad/` | `layout-editor/scripts/gen-commonw3-salads.mjs` | DLC11 食材全排列沙拉，`Web_Salad_*` / `Web_LSalad_*`，type 1 Composite |
| `custom_recipes/smoothie/` | `gen-commonw3-smoothies.mjs` + `gen-commonw3-mixed-smoothies.mjs` | 单果汁 6 款 + 混果汁 12 款（`Web_Smoothie_Mix_*`，共用官方什锦 OBJ + 分区换色贴图） |
| `custom_recipes/fried_rice/` | `gen-commonw3-migration-batch1.mjs` + `repair-commonw3-fried-rice-models.mjs` | 炒饭 6 道（每道成品独立 FBX + `*_base_color.png`） |
| `custom_recipes/soup/` | 同上 | 汤粥 7 道（2 官方 prefab + 自定义 OBJ + 鱼碗共用组） |
| `custom_recipes/ice_cream/` | 同上 | 冰淇淋 12 道（2×冰+牛奶+食材；10 果味+香草/巧克力；模型/icon 待定） |
| `custom_recipes/pudding/` | 同上 | 布丁 6 道（3 成品 + 3 中间产物） |
| `custom_recipes/milk_slush/` | 同上 | 牛奶冰沙 10 道（冰+牛奶+2×同款水果；覆盖 Web 果汁全果；模型/icon 待定） |

- 果汁 / 冰沙 / 冰淇淋（搅拌类）：**Mixed (3)** + **Glass** + **BlenderIcon**；冰沙清空 backup 误带的 `cookingStepSO`。
- 命名统一 **Web_** 前缀；显示名见 `custom_recipes/names.json`（zh + en）。
- batch1 模型纪律：**默认每道独立 FBX**；仅 manifest 审计确认同网格换贴图才可 `modelShareGroup` 共用（炒饭 2 组、鱼碗 1 组、冰沙杯 1 组）。
- 生成：`node layout-editor/scripts/gen-commonw3-migration-batch1.mjs [--apply]`；清单见 `backup_rebuild/.../自定义菜谱/_audit_docs/00-迁移清单-commonW3-batch1.md`。
- 烘焙：commonW3 模型已全部烘焙完成（原 `Bake commonW3 …` 菜单已下线）；如需重新烘焙走 web 菜谱管理流程 → Build AssetBundles（`commonW3`）。

### 2.1b commonW6 —— oc2_dlc_story 原版造景/地板补充（bundle 引用版）

- 定位：`Assets/commonW6`，bundle 名 `commonW6`，由 Loader 动态发现、导出依赖包自动携带。
- 架构（与 common01/02/03 一致）：
  - `pseudo_prefab_so/oc2dlc/*.asset`：只存 `prefabName + bundleName + assetPath`，`assetPath` 指向游戏原 bundle 内的 prefab 或 model（`.fbx/.obj`）。
  - `prefabs/oc2dlc/variants/`：单材质覆写变体（`PseudoPrefabMeshWithMaterial`，base prefab SO + material SO 均在 bundle）；多材质/隐藏子物体变体回退到基础 prefab 的 bundle SO（实例级多材质覆写不再保留）。
  - `prefabs/oc2dlc/supplement/`：源场景 `scene-embedded-mesh` / `mesh-no-model-container` 解析回 bundle prefab/model 后的普通 wrapper；解析不到的不再本地烘焙，直接丢弃。
  - `prefabs/oc2dlc/art/<theme>/`：调色板按 base id 去重后的可摆放造景（bundle 引用）。
  - **没有** `_sources/`、`meshes/`、`materials/`、`textures/`：commonW6 不再包含 `m_IndexBuffer`、本地网格、贴图或材质。
- 映射：`layout-editor/scripts/data/commonw6-bundle-map.json` 是人工校对表；未列出的条目由 `oc2r/bundle_refs.py` 按同名 prefab/model → 网格名称集合 → 关键字 → 保守模糊匹配解析。
- 迁移：`python3 layout-editor/scripts/oc2-rebuild/migrate_commonw6_bundle_refs.py --dry/--apply [--clean]`；原地改写 SO 并保留 guid，LevelSets 场景无需重生成。
- 生成：`python3 layout-editor/scripts/oc2-rebuild/rebuild.py --set dlc`（已不再静态烘焙）；审计：`python3 layout-editor/scripts/oc2-rebuild/audit_oc2dlc.py`。
- 依赖：`LayoutEditorCommonW6Dependencies` 在打开场景 / 导出 prepare / 写回时扫描场景引用，把 SO 指向的游戏 bundle（bundle163/167/428…）补进 `LevelInfoSO.dependencies`；只登记 StreamingAssets 中真实存在的 bundle。
- 中文名：`layout-editor/scripts/data/commonw6-names.json`（去掉 1~2 层 hash 后匹配；调色板装饰已覆盖）。
- 装饰双视图预览：Unity 菜单「素材导出 → 导出装饰物双视图缩略图」会包含 commonW6，产物写入 `exports/decor/front|back/<guid>.png`（192 缩略图）、`exports/decor/detail/front|back/<guid>.png`（高清详情：默认 1024；包围盒最大边 ≥5m 用 1536、≥12m 用 2048）、`exports/decor/models/<guid>/model.obj|mtl` + `_textures/`（3D 预览用 OBJ/MTL/贴图）和 `manifest.json`（git 忽略），由后端 `/api/web-assets/decor/` 只读提供；不再写入 `layout-editor/web/public`。首次导出后建议重跑 `build-catalog.mjs`，commonW6 调色板会优先保留有预览的变体。导出器通过 `LayoutEditorSafeInstantiate` 移除编辑态运行时组件（避免 `SetupCustomPrefab.Awake` NRE）；`Timeline_DynamicStage_04` / `Timeline_Tutorial_01` 两个超大 Timeline prefab 会触发 Unity 2017 原生克隆断言，导出时保留既有预览、跳过重新物化。
- 尺寸：改造前约 597 MB（meshes 370MB / textures 182MB / `_sources` 20.5MB）；改造后源码约 3 MB（1,117 wrapper + 1,095 SO），`du` 约 17 MB，bundle 体积随之大幅下降。

### 2.2 commonW5 —— Web 增量库（BGM / 桌台皮肤 / 食材 / 卷饼）

| 目录 | 内容 | 说明 |
|---|---|---|
| `audio/music/{oc1,ayce}/` | 30 首 OC1/AYCE 主题 BGM（`.ogg`） | 源曲 150MB → oggenc Vorbis q6/44.1kHz 瘦身至约 104MB（Bagel Level 的 `_0.._3` 与主版本逐字节相同，已去重）；Unity 导入 Vorbis quality 75 + Streaming |
| `pseudo_prefab_so/audio/music/` | `<id>.asset` | `PseudoPrefabSO.asset` 直引本地 AudioClip；`ScanMusic` 扫描此目录 → 关卡 BGM 下拉「OC1 主题 / AYCE 主题」两组 |
| `counters/{jungle,anniversary}/` | obj + png + `mat_*.mat` + 全功能桌台 prefab | 克隆 `common04/custom_counter/custom_counter.prefab`（保留 SetupCustomCounterStub/AttachStation/Worktop/Block），Mesh 子物体替换为参考网格 |
| `pseudo_prefab_so/counters/Counter{Jungle,Anniversary}SO.asset` | 皮肤 SO | `asset` 直引本地 prefab；`scanCounterAppearances` + `LayoutEditorCounterSkinIconExport` 已纳入 commonW5 |
| `ingredients/{spice,tea,springonion}/` | raw/chopped obj + 贴图 + 图标 + 材质 + prefab + Node | prefab 由参考工程完整组件栈重建 guid（`m_nextPrefab`→本库 unworkable、`m_ingredientOrderNode`→本库 Node）；Node uID 固定 100000001–100000003 |
| `pseudo_prefab_so/food/{SpiceSO,TeaSO,SpringOnionSO}.asset` | 食材 SO | `asset` 直引 workable prefab；`ScanIngredients`/`PseudoPrefabSearchFolders`/`build-catalog` 已纳入 |
| `custom_recipes/burrito/` | 6 个 `Web_Burrito_*`（2 成品 + 4 中间产物）+ 模型包装 + 图标 + `names.json` + `CustomRecipeConfig.asset` | uidPrefix **58323**（uid 58323001–58323006，以 web uid 体系为准）；成品模型用官方 `plated_burrito_{meat,mushroom}_01` |
| `kettle/` + `prefabs/{kettle,teacups,counters}/` + `teacup/` | 热水器视觉件（网格 obj/控制器+3 动画/材质/贴图/音频）+ `web_kettle_hotwater.prefab`（CustomStub.Kettle 轨道，原生 PlacementItemSpawner+TriggerTimer+IngredientProperties）+ `web_teacup`（茶杯）/ `web_teacup_stack_clean`（茶杯堆）（common04 模板克隆） | DLC100 原版素材 1:1（保 guid）；Mesh sv10→obj、YAML 头 2019→2017、Shader Forge 占位→自建 Custom/Web*；液体内容物 `custom_recipes/milk_tea/models/` ×5 |
| `custom_recipes/milk_tea/` + `ingredients/hotwater/` | 奶茶四件套（纯茶40/奶茶60/蜂蜜茶80/蜂蜜奶茶100）+ 热水中间产物 + `Node_HotWater`（uID **100000004**，纯节点食材，仅热水器接出）| uID 续 58323 段（58323007–58323011）；`HotWaterSO` 直引 Node；plating=common03 Mug；组成=HotWater+茶(本库)+牛奶(DLC03)+蜂巢(common01) |

**生成脚本**：`node layout-editor/scripts/gen-commonw5.mjs [--apply] [--skip-audio]`（确定性 guid：`md5("commonw5-<kind>:<id|rel>")`；不沿用参考工程 .meta）。
**热水器/奶茶生成脚本**：`python3 layout-editor/scripts/gen-commonw5-kettle.py [--apply]`；已生成资产用 **`--repair`** 幂等修补（YAML `%TAG` 末尾冒号、OBJ `m_Mesh` 的 `type: 3`、OBJ/ prefab/mat/asset importer meta、**AnimationClip 2019 keyframe sv3/weighted → 2017 sv2、AnimatorController 子状态补 serializedVersion: 1、OBJ 面补 vn 引用**），**不重写 prefab 的粒子兼容段**。
**完整 prefab 的 CleanPlateStack**：`web_teacup_stack_clean` 是 common04 完整 prefab（SetupCustomUtensilStub + CleanPlateStack + Stack，无 PseudoPrefabStub）；写回改挂 `CustomStub.CleanPlateStackSeeder`，用 prefab 的 `m_platePrefab` 做编辑预览、服务端同步后逐个补盘，**不要**再挂 `PseudoPrefabCleanPlateStack`（其基类 `ResetChild` 会 LoadAsset(null) → NRE）。
**热水器进度条/动画步进**：宿主 OC2 TriggerTimer 无 `m_CookingUIPrefab`（AYCE 新增），注入无效 → Kettle 自建 CookingUI（bundle47，反射 CookingUIController）：烧水 Progressing+SetProgress 步进、煮好 Completed（打勾+ImCooked 音效）、冷却 Idle；加热动画定长 5s，OnPress 设 animator.speed=5/heatSeconds 慢放步进。**开关只读**：Kettle 只读 `TriggerDisableScript.m_script(Interactable).enabled` 做按压沿检测，绝不再向开关补发 `Reset`/`Disable` trigger——`ClientSwitchCosmeticDecisions.Update` 一看到 `Interactable.enabled` 变化就换 `m_buttonBit` 材质并放 SwitchOn/SwitchOff 音效，任何 0.25s 节流的补发都会让按钮在开/关之间闪。
**脏茶杯合并**：单杯可拿（CarryableItem 全家桶）；单杯→脏杯堆走 Harmony 前缀（ServerDirtyPlateStack 接受单杯）；单杯+单杯由 DirtyTeacupMergeWatcher（服务端 0.5s 轮询贴近落定杯 → ServerSpawnPrefab 堆 + DestroyObject）合并。
**杯堆编辑器 Play 兜底**：seeder 等 IsSynchronisationActive 在编辑器 Play 恒 false → 预览先顶上，服务端补杯成功后才销毁预览；真机保持 Server AddToStack。**关卡摆放判据**用“本场景第一个 seeder 的 Awake 时刻”做锚点（`s_anchorTime/s_anchorScene`），不能再用 `Time.timeSinceLevelLoad <= seedWindowSeconds`——关卡是 LoadingScreenFlow 单场景 + additive 加塞加载，关卡本体 Awake 时 timeSinceLevelLoad 已 1~4s，会把摆放的杯堆当运行时生成而跳过补杯，空堆随即被 `ServerAutoDestructEmptyPlateStack` 整堆删除。补杯前必须压住 `AutoDestructEmptyPlateStack.enabled`，补到第一只杯子才放行。
**热水联动组（三壶一开关）**：默认烧水 5s（Kettle.m_heatSeconds / TriggerTimer.m_time / StubIO 默认 / web 面板四处镜像，0/负值运行时兜底 5s）；testhotwt 三壶为 5/10/12s 对照组；combo `kettle_group`（web combos.ts）一次摆三壶一开关，switchLinks 每壶一条；**组门禁已移除**（v3.12.0 后）：不再用 `RefreshGroupGate` 把开关置灰——原版开关自己维护“按下→灰→回弹”，Kettle 只做按压沿检测（`m_switchLastEnabled && !enabled` → OnPress），烧水期间再按无效由 `OnPress` 的 `state != Inactive` 早退保证，外观不做干预。壶的冷却只由各自接水完成驱动（服务端 `OnServerFilled`，客户端 `OnClientFillMessage`），不再用“开关回弹沿”推断（否则 Active 时误按会白丢一壶水）。
**Mug 装盘步统一（重要）**：奶茶链路全部引用 **common04 本地 Mug 实体**（eb494a42）：web_teacup 的 Plate/AttachedOrderCosmeticDecisions + 菜谱 platingStepSO（经 commonW5 `pseudo_prefab_so/utensils/MugPlatingLocalSO`，guid ecaf3c7d）+ Node_HotWater。禁止改回 common03 bundle Mug SO（cd51517c）——bundle 实例与本地实体引用不等，GetOrderPlatingPrefab 匹配失败、杯中显示走兜底。Node_HotWater 必须带 m_platingStep(本地 Mug) + m_platingPrefab(Web_TeaContents_Water)，且 HotWaterSO 须入 allIngredients（AutoFillIngredients 已放行 asset 直引的纯节点食材）——否则接水无匹配。
**奶茶自动填充**：选中 `Web_MilkTea_*` 菜谱时，`recipesDialogs` 自动补：热水器 + 开关（`kettle_switch` 组合/联动）、洗茶杯水槽（`web_sink_teacup`，2×1）、茶杯回收台（`web_teacup_return`，1×1）、茶杯堆（`web_teacup_stack_clean`）、茶杯（`web_teacup`）；`HotWaterSO` 由热水器产出，不建普通食材箱。热可可等非奶茶马克杯菜谱仍用原版 `workstation_sink_mug_01_wood` / `workstation_mug_return`。
**茶杯洗涤链路（gen-commonw5-teacup.py）**：`web_teacup_dirty`（脏茶杯：茶杯网格 + DLC02 污渍层叠加）→ `web_teacup_stack_dirty`（脏茶杯堆：DirtyPlateStack plateType=本地 Mug、washedPrefab=web_teacup_stack_clean、heightOffset 0.32）→ `web_sink_teacup`（洗茶杯水槽：m_sk_sink_DLC100 拆 wood/top 双网格双材质 + 3 展示脏杯 + WebLiquid 水面 + SetupCustomSinkStub.cleanPlateStackSO→TeacupStackCleanSO）→ `web_teacup_return`（茶杯回收台：PseudoPrefab 包装原版 PlateReturnStation，运行时由 `PseudoPrefabPlateReturn` 把 m_stackPrefab 接到脏茶杯堆）。确定性 guid 命名空间 `commonw5-teacup:<kind>:<id>`；新网格/材质/prefab 全部本地，不走 bundle。脏/净茶杯堆数量在 web 可编辑（stubKind=CleanPlateStack → `CustomStub.CleanPlateStackSeeder`：编辑态按 `PlateStackBase.m_platePrefab` 出预览，真机经 `ServerPlateStackBase.AddToStack` 补盘，支持 ServerCleanPlateStack / ServerDirtyPlateStack）；水槽展示脏杯由 `TeacupSinkRebuilder` 把原版 3 个脏盘槽位换成茶杯视觉克隆（只留网格渲染），三个杯子按 `m_slotPitch`（默认 0.34m）沿槽位主方向重排成一行、`m_cupScale`（默认 0.8，叠在原槽位缩放 0.70 上）缩放、`m_cupLift` 抬高出水、`m_rowShift`（默认 x+0.18，水槽根局部 x/z）把整行贴到盆内一侧边缘（第 1 只杯子钉在靠边一头，其余朝中间延伸）；四个字段可在 Inspector 实时调，改完 `OnValidate` 会重排。显隐由 `CustomStub.SinkDirtyPlateVisibility` 按**服务端权威计数** setActive 前 min(待洗,槽位) 个（1 个显示 1、2 个显示 2、≥3 个显示 3），并在 `ServerWashingStation.AddPlates` 后缀 / `ClientWashingStation.ApplyServerEvent` 后置补丁里即时校正，30 帧轮询兜底；不再依赖原版 ClientWashingStation 那份会滞后的客户端计数。
**脏茶杯禁止挂水槽台面**：`ServerAttachStation/ClientAttachStation` 前缀拦截茶杯水槽（洗涤台面/烘干位）上的本族脏茶杯（单杯或整堆），不再允许先挂到水槽上方；`ServerWashingStation` 前缀直接 `AddPlates(size)` 并销毁携带物，整堆一次全部进洗涤计数（单杯/堆字段异常时也有兜底识别）。**玩家放置动作直达**：`PlayerControlsHelper.PlaceHeldItem_Server` 前缀在玩家按放置时直接识别茶杯水槽，不依赖最终选中的放置处理器——手里脏茶杯（单杯/整堆）直接 `AddPlates(全部数量)` + `DestroyCarriedItem`，客户端经 `WashingStationMessage` 刷新水槽脏杯画面。
**茶杯回收台默认值与迁移**：`web_teacup_return` 默认 `returnClean=false`（返回脏茶杯；干净杯由水槽烘干位供应），仍可在回收台右键勾选“直接返回干净茶杯”。旧关卡曾默认 `returnClean=true`，写回/导出 prepare 时由 `LayoutEditorLayoutMigrations` 一次性迁移为 false（标记 `data/<level>/layout_migrations~/layout_migrations.json`；新关卡在 CreateLevel 时写基线，用户之后手动勾选 true 不会被再次迁移）。派生 stub 必须同时存在派生运行时 `LevelEditor.PseudoPrefabPlateReturn`（缺失时会跑基类空 Setup → 回收台仍用原版盘子堆，上菜后不回流）；`PromoteDerivedStub`/`PromoteAllDerivedStubsInScene` 负责继承 `pseudoPrefabSO` 并补运行时，写回重载/场景打开/导出 prepare 均会自愈。
**AYCE 本地化纪律**：热水器/奶茶/热水的模型、材质、shader、贴图、动画、音频、Node/SO 全部在 `Assets/commonW5` 本地，不走 AYCE bundle；仅核心 `CookingUI`（bundle47）与 common03 Mug（奶茶装盘）沿用原版 bundle。禁止为 AYCE 资产新增 `PseudoPrefabSO.bundleName`。
**Unity 侧补全**：`LayoutEditorCommonW5Setup`（`[InitializeOnLoad]` + AssetPostprocessor，幂等）补 `PseudoPrefabSO.asset` 直引、Node 图标/crateLid，并校验 uID 冲突；菜单「Layout Editor/commonW5 校验并补全直引」可手动重跑。
**诊断/修复菜单**：`Layout Editor/诊断/commonW5 热水器与开关审计`（输出 `logs/commonw5_kettle_switch_audit.txt`，含 OBJ 子资产 fileID、MeshFilter 空引用、Switch 材质 shader/isSupported、bundle38/88 加载状态）；`Layout Editor/诊断/重建伪 prefab child（修复洋红开关）`；`Layout Editor/诊断/修复 commonW5 茶杯堆（CleanPlateStack NRE）`；`Layout Editor/诊断/应用热水器粒子 2017 兼容修复`（一次性，不再随域重载自动改写 prefab）。
**热水器粒子原版参数**：早期 2017 兼容模板曾把 PFX_Steam/PFX_Drip 重置为 Unity 默认值（寿命 5s/速度 5/大小 1，蒸汽窜 20+ 米）；现由菜单 `Layout Editor/诊断/热水器粒子原版参数还原`（DiagKettlePfxRestore，API 设值 + ReplacePrefab 固化，幂等）按 AYCE s_birthday_1_3 实测值还原：Steam 寿命 1~2s/速度 5~10/大小 1~1.5/锥形 15° 半径 0.25/发射 10/尺寸增长曲线/三段透明渐变/阻尼 0.25+阻力 1；Drip 寿命 1s/大小 0.08/发射 0.25~1.5/受力下坠曲线/四段渐变。完成提醒 = KettleHot 姿态帧（PFX 三件切换 + `material._EmissiveStrength`=5 橙色发光），控制器三态与 Kettle.cs 触发（StartHeating/Active/Cooldown）链路完好。注意：**禁止**用文本把 2019 原版 198 文档写回 prefab（2017 无法反序列化，组件会被移除）；prefab 内部 fileID 已锚定旧编号（场景 override/开关联动依赖）。
**VM 重导入**：先关闭 VM 内 Unity → 在宿主执行 `--repair` → 重启 Unity；剩余 3 条 `Unexpected node type.` 来自 Kettle 控制器 3 个 child state 缺 `serializedVersion: 1`，`--repair` 已补；重导入 controller 后消失。打开场景后先执行 `Layout Editor/诊断/强制 Reimport commonW5 热水器资产`（或右键 commonW5 目录 Reimport）。若仍出现模型导入断言 `GetObject failed ... _0Mat/Avatar`，说明旧 Library 子资产还在：关闭 Unity，删除 5 个 OBJ GUID 对应的 `Library/metadata/<guid>` 与 `.info` 后重开（OBJ meta 已改为完整 `fileIDToRecycleName` + `importMaterials: 0` + `importAnimation: 0`，与 `m_space_rail_01.obj` 同款，不会再生成材质/Avatar 子资产）。若 Switch 仍洋红，跑 `commonW5 热水器与开关审计` 确认 shader 是否为 `<null>`/`isSupported=false`，再决定是否本地化 Switch 材质。
**导出**：Loader 动态发现 `commonW*`；`LayoutEditorSetExporter` 清理/校验/携带 commonW3+（含 commonW5）；场景或 LevelInfo 引用 commonW5 资产时自动标记 `web` 特征并注入 `LevelInfoSO.dependencies`。
**试听**：`/api/audio/exports` 会把 commonW5 音乐合成为 `project:Assets/commonW5/audio/...` 条目，`/api/audio/stream` 直出源 `.ogg`（Range 支持）——**无需先执行 Unity「导出音频」**；原版曲库仍依赖该导出。

---

## 3. common/ —— 引导包逐文件

| 文件 | 内容（实测） |
|---|---|
| **DIYLevelGameSession.prefab** | 121 行；根物体 `GameSession`（tag=GameSession）+ 子物体 `GameProgress`，挂 3 个反编译脚本组件：`GameSession`（m_DLCId: -1、WorldMapScene: StartScreen）、`PersistentObject`、`GameProgress`（m_sceneDirectory → 同包 EmptySceneDirectory，**同包内闭环引用**）。给 OC2DIYLevel 模组建立自制关卡会话骨架 |
| **LevelConfigTemplateSO.asset** | 三元组：`prefabName: LevelConfigTemplate, bundleName: bundle47, assetPath: Assets\Resources\datafile\levelconfigs\overcooked_2\Sushi_1_3_4P.asset`——**借用原版寿司关 1-3 的 LevelConfig 当模板**；运行时 `LevelConfigSetup.SetupConfig()` 从 bundle47 拉出它再按 LevelInfoSO 逐字段覆写 |
| **EmptySceneDirectory.asset** | `SceneDirectoryData` 脚本（guid `4f694f25...`）、`Scenes: []`——空的"关卡列表容器"，运行时被 `SetupSceneDirectoryData()` 动态生成的目录替换（其中 `entry.LoadScreenOverride = levelInfo.screenshot`） |
| **diylevelcover.png** | 1008×664 RGBA，spriteMode: 1（Sprite），spritePixelsToUnits: 100——关卡集默认封面图，引用方为外部模组 |

`common.meta`：`assetBundleName: common`；四个文件 .meta 均为空（**继承文件夹**）。构建产物 `Assets/AssetBundles/common.manifest` 证实恰好 4 资产、`Dependencies: []`——**引导包零依赖，模组可最先加载它**，再按 LevelInfoSO.dependencies 拉关卡所需包，启动顺序完全单向。

> 发现：common01/game_environment/ 里还有**第二份** DIYLevelGameSession.prefab（与 common/ 那份 diff 仅 1 行 guid）——前者进 `common` 包（外部模组用），后者被 `CampaignGameEnvironment.prefab` 内部引用进 `common01` 包。同构资产在不同 bundle 边界各司其职。

---

## 4. common01/ —— 原版本体代理库

### 4.1 目录规模细分（实测非 .meta 计数）

| 二级目录 | 文件数 | 明细 |
|---|---|---|
| prefabs | **523** | counters 13、utensils 9、mechanisms 7、Player 1、art **493**（city_sushi 101、npc 91、space 53、raft 30、wizard 31、dlc03_christmas 29、air_balloon 29、graveyard 25、dlc07_horde 19、dlc08_circus 9、dlc09_wonderland 9、dlc02_beach 7、throne 6、foodtruck 4+models、dlc05_camping 2、mine 1） |
| pseudo_prefab_so | **580** | counters 47、utensils 10、mechanisms 29（Teleportal 17/Switch 6/Marker 3/Lift 3）、art 408、audio 29（music 11 + AudioDirectories 18）、game_environment 19、PlayerSO 1 |
| food | 140 | Ingredients 31、Recipes 35、CookingSteps 8、PlatingSteps 1、CustomRecipes 25 + models 40 文件 |
| animations | 17 | Camera(.controller/.anim/CameraShake/PullBackIntro)、Raft_Scrolling_BG、Space_Earth_Rotate、Balloon_Background 等 |
| game_environment | 12 | CampaignGameEnvironment.prefab、GameConfig、Main.mixer、DebugGameConfig、Bootstrap 配置、冰面物理材质、第二份 DIYLevelGameSession |
| textures | 30 | 天空盒面片（Sunny3/Winter ×6 面）、t_*_d/_n 主题贴图、LightSoftCookie |
| materials / post_processing | 5 / 10 | 4 skybox mat + physicMaterial；7 profile + 3 LUT |

**bundleName 分布**（.asset 字段统计，共 666 条）：`bundle47×336`（shared_kitchen 主厨房）> `bundle29×87` > `bundle18×63`（data 域）> `bundle210×25` > `bundle297×23`…共 39 个不同 bundle。

### 4.2 PseudoPrefabSO 真实样本（四种形态）

```yaml
# ① 小写路径派（后期手工补录批次）
# pseudo_prefab_so/counters/BinSO.asset
  prefabName: workstation_bin_01
  bundleName: bundle47
  assetPath: assets/prefabs/shared_kitchen/workstation_bin_01.prefab

# ② 反斜杠派（AssetRipper 拷贝遗留，629 条）
# pseudo_prefab_so/utensils/PotSO.asset
  prefabName: utensil_pot_01
  bundleName: bundle47
  assetPath: Assets\prefabs\utensils\utensil_pot_01.prefab

# ③ 名实分离（PlayerSO）
  prefabName: player
  bundleName: bundle47
  assetPath: assets/prefabs/MultiplayerAvatar.prefab

# ④ 直译派（art 装饰，与文件同名）
# pseudo_prefab_so/art/space/alien_floor_tile_01.asset
  prefabName: alien_floor_tile_01
  bundleName: bundle47
  assetPath: Assets\prefabs\themes\space\alien_floor_tile_01.prefab
```

**assetPath 三形态统计**（python 全量扫描）：common01 666 条 = 小写 `assets/` 37 条（全在 counters/game_environment/PlayerSO——后期手工补录）+ `Assets\` 反斜杠 629 条；common02 743 条 = 正斜杠 683 + 反斜杠 60。`OnValidate` 的归一化**从未对存量批量执行**，反斜杠原样留存——Unity `LoadAsset` 对分隔符宽容才没炸，但**大小写不宽容**（见 §5.2 matchlist 事故）。

### 4.3 wrapper 家族的增量字段

带额外数据字段的 wrapper（在 Bin 三组件基础上加 Stub 派生）：

**Dispenser.prefab（食材箱）**——多挂 `PseudoPrefabDispenserStub`：

```yaml
  m_Script: {fileID: 11500000, guid: 156b7334933469e49b4f74ff333f27cd, type: 3}
  pseudoPrefabSO: {fileID: 11400000, guid: 3085f7abbd164904dbf7ff588c076f4b, type: 2}   # Dispenser01SO
  spawnerItemPrefabSO: {fileID: 11400000, guid: 8a928e5055585f34c89f8535c40fea3f, type: 2}  # = TomatoSO！
```

运行时 `PseudoPrefabDispenser.Setup()`（`PseudoPrefabDispenser.cs:12-53`）用 `RecipeHelper.GetIngredientPrefabForOptional(spawnerItemPrefabSO)` 换掉真 prefab 的 `PickupItemSpawner.m_itemPrefab`，再手改 crate 材质 UV 显示食材图标——**原版 prefab 一个字节没改，个性化全靠 Setup() 反射+材质覆写**。

| wrapper | 额外 Stub | 额外字段 |
|---|---|---|
| Teleportal.prefab | PseudoPrefabTeleportalStub | materialSOs（9 色）+ pfxMaterialSOs（9）+ pfxColors（9 组 RGBA） |
| Player.prefab | PseudoPrefabPlayerStub + FollowPseudoPlayer | playerID: 11（=Player.Count 占位）；1-10 号玩家、5 刀/6 帽可见性枚举 |
| art 装饰 | 可选 SpecificPseudoPrefabTag | prefabTag 字符串 → `HandleSpecificPrefabs()` 的 60+ case 按名特判修补（关灯/雪色变灰/粒子参数——原版 prefab 按主机烘焙光照调过，编辑器全实时光照下必须打补丁） |

### 4.4 food SO 家族解剖

```yaml
# Ingredients/TomatoSO.asset（简单）
  prefabName: Tomato
  bundleName: bundle18
  assetPath: Assets\prefabs\ingredients\Tomato.prefab

# Ingredients/CarrotSO.asset（跨包！胡萝卜竟在 OC1 legacy 包）
  prefabName: Carrot
  bundleName: bundle21
  assetPath: Assets\downloadablecontent\overcooked_legacy\dlc2\dlc_assets\prefabs\Carrot.prefab

# Recipes/Burger_Cheese_SO.asset（PseudoPrefabSORecipe，多 score 字段）
  m_Script: {fileID: 11500000, guid: 753d9e70603f6a140b05f30f176ec2dd, type: 3}
  prefabName: Burger_Cheese
  bundleName: bundle18
  assetPath: Assets\data\orderdefinitions\recipeitems\burgers\BeefBurgerCheese.asset
  score: 60

# CookingSteps/Pot.asset 与 PlatingSteps/Plate.asset
# 注意：步骤 SO 指向的是原版 data 资产（recipedata/platingstepdata），不是 prefab
```

**CustomRecipes（编辑器原生菜谱，CustomRecipeSO）**——`CustomRecipes/Burger/Burger_Lettuce_SO.asset`：

```yaml
  compositionSOs:
  - {fileID: 11400000, guid: 81232e654b602714098c1a440b66ddd5, type: 2}  # ChoppedBunSO
  - {fileID: 11400000, guid: 45f00e942534da143a3e94855455b50b, type: 2}  # LettuceSO
  - {fileID: 11400000, guid: 4ec8a736936e7744c91d254705783c2c, type: 2}  # FriedMeat（嵌套 CustomRecipeSO！）
  cookingStepSO: {fileID: 0}
  platingStepSO: {fileID: 11400000, guid: 02b04fdf..., type: 2}         # Plate
  modelSO: {fileID: 11400000, guid: b65af1b8..., type: 2}               # CompositeBurgerSO（成品模型）
  icon: {fileID: 21300000, guid: 9d384704..., type: 3}
  recipeName: Burger_Lettuce
  uID: 9990100      # 自定义 ID 段（原版 uID 远小于此）
  score: 60
```

其中 `FriedMeat.asset` 是"中间产物"配方：`compositionSOs=[MeatSO]`、`cookingStepSO=FryingPan`、`score: 0`——**嵌套组合 + score=0 标记半成品**是自定义菜谱体系的两个基本手法。

### 4.5 game_environment/ 与 animations/ 的角色

- **game_environment/**（12 文件）是"关卡运行环境真身"（非 pseudo）：`CampaignGameEnvironment.prefab`（约 2200 行巨型容器：ObjectivesManager / RendererSceneSettings / ScalingHUDCanvas / AudioManager / Player_01~04 / QuadGridManager / KitchenLoaderManager / BootstrapManager / RecipeUI / RatManager / KillPlane 等）。其 BootstrapManager 硬引用 common01 版 DIYLevelGameSession.prefab。模板场景 `Assets/Template/s_template` 与各 LevelSets 场景引用它；`PseudoPrefabManager.SetAssetRef()` 运行时向其中管理器反射注入原版 bundle 资产。
- **animations/**（17 文件）：相机动画与主题滚动背景（Raft_Scrolling_BG、Space_Earth_Rotate、Balloon_Background_Wander），被场景相机 Animator 与 art 主题背景 prefab 引用。

### 4.6 bundle 打标与 common01_textures 独立成包

- `common01.meta` → `assetBundleName: common01`；子目录 .meta 均为空（继承）。
- **唯一覆盖者**：`common01/textures.meta` → `assetBundleName: common01_textures`。
- 构建产物证实两包并存（`Assets/AssetBundles/` 下 `common01` + `common01_textures` 及各自 .manifest；common01.manifest 收录 863+ 行资产）。
- **为什么独立**：这 30 张贴图（天空盒 + 主题地板 _d/_n + glow）是本工程 materials/（4 个 skybox mat）唯一依赖的本地纹理，与 pseudo 资产零引用关系。独立成包 = 体积/更新频率隔离（贴图重、改得少），关卡集可只依赖贴图包。**这是"按依赖方而非按资产类型划 bundle 边界"的实践**。
- 对照：common02 只有 2 张 pooltiles、无 materials 目录，未复制该模式（textures.meta 为空）。

---

## 5. common02/ —— 季节 DLC 追加库

### 5.1 与 common01 的结构差异

| 维度 | common01 | common02 |
|---|---|---|
| 顶层子目录 | 8 个 | 5 个（无 animations/materials，textures 仅 2 png） |
| prefabs/art | 16 主题横铺 | 只按 dlc02_beach(173) / dlc05_camping(192) / npc(24) 组织 |
| food | Ingredients/Recipes 平铺 | 下再分 dlc02/dlc05；多出 **RecipeMatchList/(11)、story_meals/(10)、CookingSteps/icons/(10)** |
| audio 索引 | music 11 + AudioDirectories 18 | music 39（+overcooked_legacy/ 6）+ AudioDirectories 33 |
| textures 打标 | 独立包 | 空（并入 common02） |

bundleName 分布：`bundle167×218`（dlc02 主体）、`bundle250×212`（dlc05）、`bundle163×118`、`bundle18×40`…两主题三包撑起 3/4。

### 5.2 RecipeMatchList/：assetPath 协议字段的大小写事故（重点案例）

`DLC02_RecipeMatchList.asset` 内容：

```yaml
  prefabName: DLC02_RecipeMatchList
  bundleName: bundle162
  assetPath: Assets/downloadablecontent/dlc02/dlc_assets/data/recipes/DLC02_RecipeMatchList.asset
```

**问题**：bundle 内实名是全小写 `dlc02_recipematchlist.asset`（dump manifest 证实），驼峰 assetPath 在大小写敏感的 `LoadAsset` 下会失败。`Docs/zh/matchlist-mapping.md:54-67` 对账结论：common02 这 11 个 **当前零引用、不建议直接启用**，已被 `commonW1/pseudo_prefab_so/core/matchlists/` 的全小写版取代：

```yaml
# commonW1/.../matchlists/dlc02_recipematchlist.asset（现役版）
  prefabName: dlc02_recipematchlist
  bundleName: bundle162
  assetPath: Assets/downloadablecontent/dlc02/dlc_assets/data/recipes/dlc02_recipematchlist.asset  # 与 bundle 实名逐字符一致 ✓
```

**启发**：pseudo 体系容许"平行的索引库"共存，切换成本仅是改引用 guid；assetPath 是协议字段，正确形态 = **与 bundle 内实名完全一致**（commonW1 全小写版是标准答案）。

### 5.3 story_meals/ 与 audio 组织

- **story_meals/**（10 个 Composite*.asset）：故事模式"组合餐"成品模型代理（堆叠好的立体餐品，上菜/订单 UI 用）。`CompositeBurger.asset` → `bundle47 Assets/prefabs/overcooked_legacy/meals/CompositeBurger.prefab`。也被 common01/commonW2 的 CustomRecipe 当 `modelSO` 复用（**跨库复用成品模型**）。
- **music/**（39+6 个）：DLC BGM 索引，样本 `music/DLC_02_Generic.asset` → `bundle158 ...dlc_02/music/DLC_02_Generic.wav`（LoadAsset 泛型版按 AudioClip 加载 .wav）。
- **AudioDirectories/**（33 个）：音频目录索引（Shared/UI/VO/Step/各 DLC/Rats…），填进 `LevelInfoSO.audioDirectorySOs`；`SetAssetRef()` 加载后还会把 Flamethrower 条目音量压半（`PseudoPrefabManager.cs:183-185`）。
- **ambiences 不是目录**：氛围音 = `LevelInfoSO.inLevelAmbiences: GameLoopingAudioTag[]`（187 个 tag 枚举，`LevelInfoSO.cs:51-188`），音源本体在 AudioDirectory 里。
- **animators/ 子目录**：8 个 AnimatorController 也走 pseudo（如 `DLC07_NPC_Float.asset → bundle289 ...dlc07_NPC_Float.controller`）——**controller 与 prefab/wav 一样可以被三元组索引**。

### 5.4 独有 prefabs 与 models

counters：Barbeque/Blender/Campfire/GlassReturn/SinkGlass；utensils：Bellows/BlenderCup/CleanGlassStack/Glass/GriddlePan/Skewer/ToastingFork/WaterGun；mechanisms：Backpack/Burner（火焰发射器，tutorial 明示"火焰发射器（common02）"）。`Recipes/dlc{02,05}/model_so/` 存中间模型索引（如 `model_prep_pancake_01 → bundle185`），供 `RecipeHelper.GetOrderToPrefabLookup` 的 modelSOs 数组用。

---

## 6. common03/ —— 后期 DLC 大合集（上游正式版镜像）

### 6.1 现状结构（对齐上游后）

```
common03/
├─ prefabs/
│   ├─ art/{dlc03_christmas, dlc04_chinatown, dlc07_horde, dlc08_circus,
│   │       dlc09_festivemashup, dlc10_lunar, dlc11_summer, dlc13_moonfestival}
│   ├─ counters/   （8 个，平铺）
│   ├─ mechanisms/ （18 个，含 Hotpot/、dlc08_cannon/ 子目录）
│   └─ utensils/   （8 个，平铺）
├─ pseudo_prefab_so/（镜像同构，1376 个）
├─ food/{Ingredients,Recipes}/{dlc03,dlc04,dlc07,dlc08,dlc11,dlc13}
│   ├─ CookingSteps/{HotPot,RoastingTray}.asset
│   └─ PlatingSteps/Mug.asset
├─ skybox/            （5 个主题天空盒材质族）
└─ post_processing/   （12 组 LUT profile + png）
```

**art 各 dlc prefab 计数**：dlc07_horde **409**（battlements/city/courtyard/dressing/keep/throne 六子区）> dlc09_festivemashup **312**（四主题换皮区）> dlc11_summer 166 > dlc08_circus 122 > dlc03_christmas 94 > dlc13_moonfestival 87 > dlc04_chinatown 69 > dlc10_lunar 3；加 counters/mechanisms/utensils 共 **1296**。

**重要历史**：文档与代码注释里曾描述的「`prefabs/{dlcXX|core}/{category}` 三段式」是**旧布局**。git 提交 `b499bd15e`（2026-09-02，"common03 aligned to upstream formal version"）删除了旧结构（prefabs/{core,dlcXX}、materials/textures、version.txt 等），并修复了 36 个被编辑器重导入污染的目录 .meta GUID。现状即「对齐上游正式版」的 `prefabs/{category}`（art 下按 dlc 主题）布局。编辑器用**一个正则同时兼容两代**：

```csharp
// Assets/Editor/LayoutEditor/LayoutEditorCatalogLookup.cs:159-166
// 兼容两代目录结构：prefabs/{category}/（common01/02）与
// prefabs/{dlcXX|core}/{category}/（common03 通用内容按 dlc 分目录）。
var cat = System.Text.RegularExpressions.Regex.Match(assetPath,
    @"/prefabs/(?:[^/]+/)?(counters|utensils|mechanisms)/");
```

### 6.2 典型 SO 样本

```yaml
# ① dlc 食材（assetPath 指向 orderdefinitions 数据资产，无 prefab！）
# food/Ingredients/dlc08/DLC08_Ketchup.asset
  prefabName: ketchup
  bundleName: bundle354
  assetPath: Assets/downloadablecontent/dlc08/dlc_assets/data/orderdefinitions/ingredients/ketchup.asset

# ② dlc 厨具（dlc04 火锅大汤勺；注意路径含空格 "shared kitchen"）
# pseudo_prefab_so/utensils/utensil_big_ol_spoon.asset
  prefabName: utensil_big_ol_spoon
  bundleName: bundle226
  assetPath: Assets/downloadablecontent/dlc04/dlc_assets/prefabs/shared kitchen/utensil_big_ol_spoon.prefab

# ③ dlc 菜谱（PseudoPrefabSORecipe）
# food/Recipes/dlc08/DLC08_Hotdog_Onions_Ketchup.asset
  prefabName: HotdogOnionsKetchup     # camelize 去 dlc 前缀后的展示名
  bundleName: bundle354
  assetPath: Assets/.../orderdefinitions/recipeitems/hotdog_onions_ketchup.asset
  score: 100                          # optional/permutation 前缀则为 0
```

"无 prefab 食材"（ketchup/mustard/orangesoda…）在生成脚本里有专门黑名单 `NO_PREFAB_INGREDIENTS`——这类食材只进菜谱树、不能从食材箱生成。

### 6.3 prefab ↔ SO 对应关系（非严格 1:1）

按同名文件比对：1296 prefab 中 **1263 有同名 SO**；35 个 prefab 无 SO（pfx/组合件如 `noripple_m_dlc3_icecliff_270.prefab`）；SO 侧多出 113 个无 prefab 的 asset（上游 dump 重复件如 `p_dlc07_keep_flagstone_01 (11).asset`）。另有 **40 个"外观皮肤 SO"**（CounterXXXSO/SinkXXXSO）**按设计只有 SO 没有 prefab**——因为换肤 = 换整只真 prefab（玩法组件随 prefab 保留），见 `import-dlc-content.mjs` 的 `emitCounterAppearances` 注释。

### 6.4 迁移历史证据（注释原文）

`LayoutEditorHttpServer.cs:541-543`：

```csharp
// 统一保存处理：common03 引用校验（历史 Import/custom_web
// 引用告警）+ 收集 doc 引用的游戏 bundle，供后续依赖注册。
// common03 资产直接引用、随 common03 bundle 打包，不再拷贝 custom_web。
```

`LayoutEditorCustomIngredients.cs:39-40`：`public const string CustomDirName = "custom_web"; // 旧 custom_web 拷贝目录名（机制已废弃，仅为兼容读取历史数据保留）`。生成脚本头部同样注明「输出（Assets/common03/，由 Assets/Editor/LayoutEditor/Import 迁移而来）」。**旧的"拷贝进关卡集"机制已废弃，改为直接引用 common03**。

---

## 7. commonW1/ —— Web 版 + 编辑器自制增量层

### 7.1 web/hotpot/ 火锅专区（6 个 prefab）

| 文件 | SO 指向 |
|---|---|
| `web_utensil_large_pot_01.prefab` | → **common03**/pseudo_prefab_so/mechanisms/utensil_large_pot_01.asset（原版大锅 bundle226） |
| `web_utensil_large_pot_01_pushable.prefab` | → commonW1 core/utensils/utensil_large_pot_01_pushable.asset（prefabName=pushable_object） |
| `web_utensil_dlc10_large_pot_01.prefab` / `web_cooking_region_floorburner.prefab` / `web_dlc10_cooking_region_floorburner.prefab` / `web_dlc10_pushable_object.prefab` | 各自对应 dlc10/地热灶 SO |

**烹饪锅组件结构**（四组件）：

```yaml
  m_Component:
  - component: {fileID: 4341917854781290}      # Transform
  - component: {fileID: 114266618352629990}    # PseudoPrefabCookingUtensilStub
  - component: {fileID: 6598243100175246001}   # BoxCollider（!u!65）
  - component: {fileID: 114214987726898516}    # PseudoPrefab
--- !u!65 &6598243100175246001
BoxCollider:
  m_Size: {x: 2, y: 0.8, z: 2}
  m_Center: {x: -0.6, y: 0.4, z: 0.6}
```

**可推动锅**（五组件）——`b499bd15e` 所说「PushablePot| tag + SOArray carrier（slot0=锅 SO）」的落地：

```yaml
--- !u!114 &1145139284002000001
  m_Script: {fileID: 11500000, guid: 2b19728b6d0dfe44baf2f80df9bfc1e9, type: 3}  # SpecificPseudoPrefabTag
  prefabTag: PushablePot|
--- !u!114 &1145139284002000002
  m_Script: {fileID: 11500000, guid: 0232b03a35f0cc74c8326feb4f5b79cb, type: 3}  # PseudoPrefabSOArray
  pseudoPrefabSOs:
  - {fileID: 11400000, guid: 8c4d090a2f936008374d00c5df134879, type: 2}          # slot0 = 原版大锅 SO
```

目录归组：`LayoutEditorCatalogLookup.cs:149-152` 把 `/prefabs/web/hotpot/` 强制归 `Design/Counters`、pushable 归 `Design/Utensils`。

### 7.2 question_mark/（12 款）与绑定链路

清单：`question_mark_{blue, brown, chef_hat, food, gray, green, orange, pink, purple, red, theme, yellow}.png`（全部随 commonw1 bundle 打包，manifest 实测收录）。绑定链路四步：

1. 组件字段（`CustomStub/RandomCrate/RandomCrate.cs:48-50`）：`[SerializeField] public Texture2D m_questionMarkTexture;`（为空时保留游戏绘制的首食材图标，自然降级）；
2. 写回时装载（`LayoutEditorStubIO.cs:1711-1731`）：常量 `RandomCrateQuestionTextureDir = "Assets/commonW1/question_mark"`，默认款 `question_mark_chef_hat.png`；DTO 的 questionMarkGuid 失效时回落默认；
3. HTTP 端点 `/api/catalog/questionmarks` 列出样式（`isDefault = name == "question_mark_chef_hat"`）；
4. 运行时 `PaintQuestionMark(child, tex)`——渲染器查找顺序**镜像游戏** `ClientItemCrateCosmeticDecisions`（盖子 Skinned → 盖子 Mesh → 根 Mesh 兜底），兼容新皮肤结构。

### 7.3 backgrounds/（8 主题 13 prefab）

数据源 `scripts/data/commonw1-backgrounds.json`（每项 id/theme/zh/en/cellsX/cellsZ/orientation）：city(1)/core(3)/dlc02(1)/dlc05(1)/dlc07(3)/dlc10(1)/dlc13(2)/raft(1)。这些是**内置网格占位体**（不依赖 FBX）：`city_water.prefab` 的 Mesh 是引擎内置 Quad（`fileID: 10210, guid: 0000...e000...`），rotation.x=90°、scale 0.72（= 6 格 × 1.2 / 10 平面单位）——由 `gen-commonw1-prefabs.mjs` 程序化生成。

### 7.4 core/counters/RandomDispenser.prefab 解剖（随机食材箱专属道具）

根 GameObject 挂 **5 个组件**：

```yaml
  m_Component:
  - component: {fileID: 4341917854781290}      # Transform
  - component: {fileID: 114609642817661240}    # PseudoPrefabDispenserStub（156b7334…）
  - component: {fileID: 114843371774752328}    # PseudoPrefabDispenser（编辑器宿主）
  - component: {fileID: 1145139284001000001}   # SpecificPseudoPrefabTag → prefabTag: RandomCrate|
  - component: {fileID: 1145139284001000002}   # PseudoPrefabSOArray → pseudoPrefabSOs: []
```

生成方法（`CustomStubCopyTool.cs:136-176`，域重载后自动调用，幂等）：

```csharp
/// <summary>RandomDispenser 包装 prefab（commonW1）：随机食材箱的专属道具类型。
/// 基于 Dispenser 包装复制，自带数据载体空壳（tag "RandomCrate|" + 空 soArray）——
/// web 目录/调色板按独立道具展示，apply 按实例填充随机配置。幂等。</summary>
if (!AssetDatabase.CopyAsset(BaseDispenserPrefabPath, RandomDispenserPrefabPath)) { … }
var tag = go.GetComponent<SpecificPseudoPrefabTag>() ?? go.AddComponent<SpecificPseudoPrefabTag>();
tag.prefabTag = "RandomCrate|";
var soArray = go.GetComponent<PseudoPrefabSOArray>() ?? go.AddComponent<PseudoPrefabSOArray>();
soArray.pseudoPrefabSOs = new PseudoPrefabSO[0];
```

**设计要点**：复制而非手写模板（保真继承 Dispenser 的 Stub/宿主组件），再补两个"数据载体"——即使关卡集 stub 程序集还没编译，`RandomCrate|` tag + SOArray 也能让 web 数据往返不丢。

### 7.5 pseudo_prefab_so/core/（含现役 matchlists）

- `core/counters/`：blender + 外观皮肤 SO（ServingStationBlueSlimSO 等，皮肤 SO 落在 W1）；`core/utensils/`：blender、pushable 大锅；`core/art/{legacy,npc}`；`core/decor/recipes`（30 个成品菜视觉 SO）。
- **matchlists/（11 个，现役版）**：`combineddlc/dlc02/.../dlc13_recipematchlist.asset`，全部与 bundle 实名逐字符一致（§5.2）。消费方是 `LevelInfoSO.includeRecipeMatchLists`，web 的「菜谱管理 Matchlist tab」（key 白名单 dlc02…dlc13/combineddlc）。

### 7.6 materials/（309 个）与 textures/（125 png）

命名规律（首 token 统计）：`mat_*` 255（原版材质名直迁）、`FloorTiles*` 36、`dlc11_mat_*` 6、DLC2_* 3、其余零星。`.mat` 模板与 `extract_floor_materials.py` 的 `MAT_TEMPLATE` 完全一致（Standard shader，`_DiffuseMap`/`_MainTex` 双绑同一贴图 guid）。

**确定性 guid 实测验证**（§9.2 算法）：

```
md5("common03:Assets/common03/materials/FloorTiles_Chequered.mat") = 320c6f3be62978934a321ee2e4f00ecf  ✓
md5("common03:Assets/common03/textures/FloorTiles_Kitchen_2x2.png") = cf242433…  ✓
```

注意哈希输入仍是**旧 common03 路径**——materials/textures 目录后来整体迁到 commonW1（`09bfc916b`），`.meta` 原样保留，guid 稳定不破坏引用。textures 中 `FloorTiles_*` 28 张（Crossing/Desert/Kitchen_2x2/Pavement 系列/PoshRestaurant/Rainbow 1-3/Road_YellowBox/SnowRoad…）。扫描入口：`LayoutEditorFloorMaterialsApi.cs:99`（LevelSets → common01/02 → commonW1/materials 顺序 fallback）。

### 7.7 dlcXX/ 与 common03 dlcXX 的分工

commonW1 的 dlcXX 是 **dlc-first** 两级结构（`dlcXX/{art,counters,decor/{food,recipes},mechanisms,utensils}`），独有 `decor/`：

- `decor/food/`、`decor/recipes/`：**食材/成品菜的摆放包装 prefab**，pseudoPrefabSO 直接引用 common01/02/03 已有 SO（不新建）。如 `dlc13/decor/food/dlc13_chocolate.prefab`；`core/decor/food/` 31 个本体食材包装。
- `dlc09/food`、`dlc11/food`：**上游 common03 已删、按需保留**的食材包装（`build-catalog.mjs:1211-1214` 注释："上游已删、按需保留的用户食材包装（dlc11_ketchup/mustard…）"）。
- 与迁移提交 `a66d607b5`（"migrate upstream-absent common03 assets to commonW1 (pushable pot, dlc10 hotpot, dlc09 cannon, blender, dlc08 map dressing)"）一一对应。

各 dlcXX prefab 数：dlc08 99 > dlc02 27 > dlc05 26 > dlc09 23 > dlc03/13 15 > dlc10/11 14 > dlc07 13 > dlc04 12（另 core 75、backgrounds 13、web 6）。

### 7.8 bundle 打标实测

五个顶层 meta（materials/textures/prefabs/pseudo_prefab_so/question_mark）+ 根 meta 均显式 `assetBundleName: commonW1`。**148 个 folder meta 中有 38 个嵌套目录为空值**（Unity 自行生成，如 prefabs/web.meta）——实测 commonw1.manifest 收录 1039 条资产，web/hotpot 6 prefab 与 question_mark png 均在包内：**bundle 归属由最近的标记了 commonW1 的祖先目录继承，空值不阻断**。脚本自建目录则必然带标记（`ensureCommonW1FolderMeta`，guid 走确定性 `md5("commonw1-folder:"+相对路径)`，已复算验证）。

---

## 8. commonW2/ —— Burger 大全共享库

### 8.0 二级分类目录（2026-09-15）

`custom_recipes/burger/` 下按子目录细分，菜谱资产分为 8 类；`models/`、`icons/` 留在 `burger/` 根下共享
（另有同级 `custom_recipes/fry/` 4 个）。条数为 2026-09-18 实测：

| 子目录 | 显示名 | 条数 | 判定 |
|---|---|---|---|
| `assembly/` | 组装定义 | 6 | `CustomRecipeOptionalBurgerSO` 或 `*_filler` |
| `classic/` | 经典汉堡 | 26 | 含肉/芝士，40–80 分 |
| `deluxe/` | 豪华汉堡 | 19 | 含肉/芝士，100–140 分 |
| `mega/` | 巨无霸汉堡 | 6 | ≥160 分 |
| `breakfast/` | 早餐汉堡 | 4 | `Breakfast*` |
| `seafood/` | 海鲜汉堡 | 3 | 夹心含 Prawn/Shrimp/Fish |
| `veggie/` | 素食汉堡 | 10 | 夹心纯蔬果 |
| `filling/` | 夹心/中间产物 | 40 | 非 Composite 或 score≤0 |

为什么零破坏：`ScanCustomRecipes` 的 `category` 只取 `custom_recipes/` 下**第一段**目录名（仍是 `burger`），
`ScanAssetsByScript` 是 `AllDirectories` 递归，`CustomRecipeNamesPath` 向上回溯找 `custom_recipes`。
`.asset` 与 `.meta` 成对移动，guid 不变，所有按 guid 的引用不受影响。

⚠ 配套代码改动：`LayoutEditorBurgerApi` 原先硬编码 `burger/OptionalBurger.asset` 的两处已改为
`FindCommonW2Assembly(id)` 递归扫描，将来再调整分类无需改代码。

显示名与排序**三处同源**：`LayoutEditorLevelAdminApi.BurgerSubcategories()` ↔
`layout-editor/web/src/recipeTypes.ts` 的 `BURGER_SUBTYPE_ZH/ORDER` ↔
`layout-editor/scripts/split-burger-subcategories.mjs` 的 `SUBCATEGORIES`。

迁移脚本：`node layout-editor/scripts/split-burger-subcategories.mjs`（dry-run）/ `--apply`（执行 + 报告）。

### 8.1 全貌（burger 114 + fry 4 配方，15 模型 prefab；2026-09-18 实测）

- **成品汉堡（score>0）**：单料（Lettuce/Tomato/Cucumber/Pineapple/Chicken/Cheese 各 `XBurger`）、双料组合（`LettuceTomatoBurger` 等）、三料/四料、早餐系列（`Breakfast{Cheese,Lettuce,Meat,Onion}Burger`）、特色（`DoublePineappleBurger`（含 `_filler` 变体）、`Supreme`），以及**官方牛肉系汉堡的 DLC8 面皮副本 5 个**（`MeatBurger`/`CheeseMeatBurger`/`LettuceMeatBurger`/`LettuceCheeseMeatBurger`/`LettuceTomatoMeatBurger`，uID 58321069~58321073，对应官方 `Burger_Plain_SO`/`Burger_Cheese_SO`/`Burger_Lettuce_SO`/`Burger_CheeseLettuce_SO`/`Burger_LettuceTomato_SO`，牛肉夹心统一用 common01 `FriedMeat`）。
- **中间产物（score=0）**：`Fried{Cheese,Chicken,CornPone,FishPone,Mushroom,Onion,Pepperoni,Pineapple,PotatoCake,Prawn,ShrimpPone}`、`Panfried{MeatEgg,MeatMushroom,MeatOnion,Mushroom,Onion,Tomato}`、`Mixed{MeatEgg,MeatMushroom,MeatOnion}`、`EggSausage/BaconSausage/PineappleSlice` 等。
  ⚠ `ChickenPatty`、`PanfriedBeef` 已于 2026-09-18 合并删除（见下「鸡肉/牛肉统一」）。
- **组装定义（CustomRecipeOptionalBurgerSO）6 个**：`OptionalBurger`（主模板，capacity=20）、`VeggieBurgerAssembly`、`ChickenBurgerAssembly`、`MeatBurgerAssembly`、`PineappleMeatBurgerAssembly`、`DoublePineappleBurger_filler`（uID `1179688004`，不在 58321 号段，属手工追加残留）。
- **面皮统一**：全部成品的 `compositionSOs[0]` 与全部 6 个组装定义的 `bunSO` 一律是 **DLC8 面皮**（`Assets/common03/food/Ingredients/dlc08/dlc08_choppedbun.asset`，guid `965ff691e25e50b0c5151ea9a97899d4`），与 DLC8 套餐（`DLC08_MD_*`）同款。核心 `ChoppedBunSO`、`DLC02_ChoppedBun` 在 commonW2 中零引用。
- **鸡肉统一（2026-09-18，与面皮统一同级纪律）**：commonW2 内鸡肉只有一条轨道 `filling/FriedChicken.asset`（uID 58321020）——
  生食材 `common03/food/Ingredients/dlc08/DLC08_Chicken`（`60adb1f0…`，bundle359），
  夹心模型 `models/p_dlc8_recipe_chicken_burger_01`（`3308b577…`，同 bundle359）。
  **common01 `ChickenSO`（`f5e2ed0b…`，bundle47 OC1 legacy 鸡）在 commonW2 中零引用。**
  历史双轨来源：`migrate-burger-recipes.mjs` 的 `GUID_REMAP` 曾把 100 Burger 源数据里的悬空生鸡肉 guid
  `7b0da7f9…` 错顶成 common01 `ChickenSO`，造出第二条鸡肉 `ChickenPatty`（58321012）。
  合并时 `ChickenPatty` 的 4 处引用（`OptionalBurger` slot[16] + 3 道 deluxe）全部改指 `FriedChicken`，
  脚本 `GUID_REMAP` 已同步改为映射到 `DLC08_Chicken`（否则重跑迁移会回退）。
  连带删除组成撞车的 `deluxe/LettuceTomatoCheeseChickenBurger`（58321081，100 分），
  保留组成相同但分数更高的 `deluxe/ChickenLettuceCheeseTomatoBurger`（58321009，120 分）。
- **牛肉统一（2026-09-18）**：煎牛肉只有一条轨道 `common01/food/CustomRecipes/Burger/FriedMeat`（`4ec8a736…`）。
  commonW2 原有的平行夹心 `filling/PanfriedBeef`（58321053，呆喵 `FriedBeefPatty` 模型）与 `FriedMeat`
  **组成完全等价**（`MeatSO` + 煎锅 + progress=1），已删除；6 处引用改指 `FriedMeat`，
  `PineappleMeatBurgerAssembly` 的两个牛肉槽模型改用 `models/plated_meat`（`6c8a0c12…`，bundle47），
  与 `OptionalBurger` 的 `FriedMeat` 槽写法一致。
- **模型资产保留不删**：`FriedChickenPatty.prefab` / `FriedChicken*.fbx|mat|png` / `FriedBeef*.prefab|mat|png`
  即使已无菜谱引用也**必须留着**——`models/` 兼作汉堡工作台的「模板网格库」（见下方 models/ 条目），删了会砍掉模板池。
- **models/ 15 prefab**：CucumberSlice、FriedBeefNew、FriedBeefPatty、FriedCheese、FriedChickenPatty、FriedCornCake、FriedEgg、FriedFishCake、FriedPotatoCake、FriedSausage、FriedShrimpCake、PanfriedMushroom、PanfriedOnion、PineappleSlice、TomatoSlice（另 11 fbx、13 mat、12 视觉 SO）。icons/ 41 png（26 呆喵成品图标 + 10 张 `ui_*` 原版风格 + 5 张官方汉堡副本图标）。
- **models/ 兼作模板网格库**（2026-09-15）：这里的 `*.fbx` 是「模板网格模式」的**只读**素材源。
  工作台新建夹心时，后端从中**拷贝一份 FBX 字节**到目标菜谱的 models 目录并改名为 `<RecipeId>.fbx`
  （只拷 .fbx 不拷 .meta，新文件由 Unity 生成全新 guid），配前端 `textureEditor.ts` 产出的
  1024×1024 PNG 即可零建模做出夹心。默认模板 `FriedFishCake`（棱角球肉饼形；其 `FriedFishCake.png`
  实测就是 `#C06205` + 黑的纯色贴图，所以「纯色就够用」）。
  **拷贝而非引用**很关键：若 prefab 反向引用 commonW2 的网格，关卡集本地夹心会把整个 commonW2 bundle
  拖成依赖（`ReferencesCommonW2Deep` 会命中 `ingredientModels`）。
- **原版模型池**：`layout-editor/scripts/data/burger-model-pool.json`（524 条，`gen-burger-model-pool.mjs`
  从 `dump_bundle/manifest.json` 提取 `*/prefabs/{plated,ingredients,recipes,meals}/*.prefab` 且 bundle 已在
  StreamingAssets；12 个 bundle 全部可用）。选用时才在 `models/` 按需生成包装 `PseudoPrefabSO`，
  `assetPath` 逐字符照抄 bundle 内实名（大小写敏感）。

### 8.2 配方 asset 全字段

`custom_recipes/burger/BreakfastCheeseBurger.asset`：

```yaml
--- !u!114 &11400000
MonoBehaviour:
  m_Script: {fileID: 11500000, guid: 83fb008bcc8e793429b02c178c430815, type: 3}   # CustomRecipeSO
  m_Name: BreakfastCheeseBurger
  type: 1                        # RecipeType: Null/Composite/Cooked/Mixed → 1=Composite
  recipeName: BreakfastCheeseBurger
  uID: 58321002                  # 58321 号段自增
  score: 120
  platingStepSO: {fileID: 11400000, guid: 02b04fdf…, type: 2}   # → common01 PlatingSteps/Plate
  modelSO:       {fileID: 11400000, guid: 7361adeс…, type: 2}   # → common02 story_meals/CompositeBurger（跨库引用！）
  icon:          {fileID: 21300000, guid: 52f7618a…, type: 3}   # → commonW2 icons/ui_cheeseburger_01.png
  compositionSOs:                                                # 面皮 + 三个煎料
  - {fileID: 11400000, guid: 965ff691…, type: 2}   # common03 dlc08_choppedbun（DLC8 面皮，全库统一）
  - …（省略 3 项）
  optionalSOs: []
  cookingStepSO: {fileID: 0}      # Composite 无烹饪步骤；Cooked 型此处指向 HotPot/RoastingTray
```

### 8.3 names.json 与 CustomRecipeConfig

两者位于 `Assets/commonW2/custom_recipes/`（burger/ 同级）。`names.json`（带 BOM 的 UTF-8，`schemaVersion:1`，
118 项，id 与 asset 文件名一致；条目数可多于资产数——删菜谱时要手工同步删条目，2026-09-18 已删
`ChickenPatty`/`PanfriedBeef`/`LettuceTomatoCheeseChickenBurger` 三条）：

```json
{ "schemaVersion": 1, "names": [
  { "id": "BaconSausage",          "zh": "培根煎香肠",   "en": "Bacon Sausage" },
  { "id": "BreakfastCheeseBurger", "zh": "早餐芝士汉堡", "en": "Breakfast Cheese Burger" }, … ] }
```

`CustomRecipeConfig.asset`（同样在 `custom_recipes/` 层）：

```yaml
  uidPrefix: 58321        # uID = 58321*1000 + 序号
  nextSequence: 95        # 下一自增序号（新配方从 58321095 起）；删菜谱时**不回退**，uID 永不复用
  categories:
  - id: burger
    zh: "Burger\u5927\u5168"      # "Burger大全"（Unity YAML 非 ASCII 转义）
    en: Burger
  - id: fry
    zh: "\u70B8\u7269"            # "炸物"
    en: Fry
  modelTransforms: []     # 每菜谱模型 scale/rotation 微调，避免改宿主类定义
```

编辑器接入：`LayoutEditorCustomIngredients.ReferencesCommonW2()` 深层判定（含 composition/model 引用）决定是否注册 commonW2 依赖；`CommonW2RecipesDir` 是所有关卡集可见的固定菜谱分类扫描根。

### 8.4 食材口径一致性（已统一 / 遗留待办）

commonW2 的菜谱来自三批合并（100 Burger / 呆喵新菜谱 / 2026-09-17「补充汉堡」手工追加），
**每批对同一种食材选了不同的 SO**，是本库最主要的系统性缺陷来源。

**判定"等价撞车"的标准**：`compositionSOs` 多重集 + `cookingStepSO` + `type` + `cookingProgress/mixingProgress`
四项全同 ⇒ 游戏内是同一道菜（`CompositeOrderNode` 匹配与层序无关），订单匹配会出现歧义。

已统一：

| 食材 | 统一口径 | 备注 |
|---|---|---|
| 面包皮 | `common03` `dlc08_choppedbun` | 见 8.1 |
| 生鸡肉 | `common03` `DLC08_Chicken` | 2026-09-18；`common01 ChickenSO` 零引用 |
| 煎鸡肉夹心 | `commonW2` `FriedChicken`（模型 DLC08 `p_dlc8_recipe_chicken_burger_01`） | 2026-09-18 |
| 煎牛肉夹心 | `common01` `FriedMeat`（模型 `plated_meat`） | 2026-09-18；`PanfriedBeef` 已删 |
| 生菜/芝士/黄瓜/番茄 | `common01` | 本来就一致 |
| 菠萝 | `common02` `dlc02/BurgerPineapple` | 本来就一致 |
| 香肠/培根 | `common02` `dlc05` | 本来就一致 |
| 玉米 | `common03` `dlc11/DLC11_Corn` | 本来就一致 |

遗留待办（**本次未处理**，均为已存在的等价撞车或双源）：

| 级别 | 问题 | 详情 |
|---|---|---|
| 🔴 | `FriedPrawn`(58321028) vs `FriedShrimpPone`(58321029) | 均为 `PrawnSO` + `DeepFatFryer` + progress=1，完全等价 |
| 🔴 | `MixedMeatMushroom`(58321050) vs `MixedMeatMushroomRaw`(1001016) | `MeatSO`+`MushroomSO` 搅拌，完全等价；下游 `PanfriedMeatMushroom` vs `PanfriedMixedMeatMushroom` 同样成对 |
| 🔴 | `MixedMeatOnion`(58321051) vs `MixedMeatOnionRaw`(1001014) | 同上；下游 `PanfriedMeatOnion` vs `PanfriedMixedMeatOnion` |
| 🟠 | 鸡蛋双源 | `common01 EggSO` vs `common02 dlc05/DLC05_Egg`：`MixedMeatEgg`(DLC05) vs `MixedMeatEggRaw`(EggSO)；`EggSausage`(DLC05) vs `EggSausageGriddle`(EggSO，零引用) |
| 🟡 | uID 号段分裂 | 规范段 58321xxx；2026-09-17 批次用了 1000xxx~1004xxx，另有 `DoublePineappleBurger_filler` 的 1179688004 |
| 🟢 | 非缺陷 | `FriedMushroom`(油炸锅)/`PanfriedMushroom`(煎锅)、`FriedOnion`/`PanfriedOnion`、`FriedPineapple`(煎)/`PineappleSlice`(生切)——`cookingStepSO` 不同，是合法的不同产物，只是命名容易混 

---

## 9. 生成脚本与 guid 纪律（"为什么这么写"的核心）

### 9.1 一代批量生成器：BatchCreatePseudoPrefab（编辑器菜单）

`Assets/Editor/BatchCreatePseudoPrefab.cs`：选中目标文件夹 → 弹系统对话框选"原资源文件夹"（AssetRipper 导出目录）→ 遍历 *.prefab/*.asset/*.mat，`AssetDatabase.CreateAsset` 生成 SO + `PrefabUtility.CreatePrefab` 生成 wrapper。这解释了 §1.2 的**双种子模板 fileID** 与 SO/prefab 一一同名镜像布局。

### 9.2 二代生成器：import-dlc-content.mjs（确定性 GUID）

**核心算法一行**（`layout-editor/scripts/import-dlc-content.mjs:92-94`）：

```js
function guid(prefix, id) {
  return crypto.createHash("md5").update(`${prefix}:${id}`).digest("hex");
}
```

前缀按资产类型分命名空间：`ing` / `recipe` / `step` / `pseudo` / `prefab` / `food-decor-prefab` / `recipe-decor-pseudo` / `recipe-decor-prefab` / `commonw1-folder`——**哈希前缀天然避免不同类资产撞 guid**。三例复算全部吻合：

```
md5("pseudo:workstation_furnace_01") = ac5e7a05abd79dd690305213db33374e = SO meta guid ✓
md5("prefab:workstation_furnace_01") = 503c206df980d89cf9c3f56535c138bb = prefab meta guid ✓
md5("recipe:hotdog_onions_ketchup")  = 7dc4ac104712f59b966d187718a7d4e5 = 菜谱 meta guid ✓
```

（注意第三例：文件后来改名 `DLC08_Hotdog_Onions_Ketchup.asset`，meta 随之保留——**guid 锚定 id 而非文件名，改名不换 guid**。）

**为何 .meta 必须确定性生成**（脚本头注释）："每个 .asset/.prefab 同时生成确定性 .meta（md5(id) 派生，重复运行内容不变）"。因为这是**库外脚本直接写文件**：若让 Unity 首次导入随机分配 guid，重跑/换机器/删库重导都会得到不同 guid，场景与 web 文档里的 guid 引用全体断链。确定性 guid = 可重放构建（reproducible build）。

**查表机制**：读 `dump_bundle/manifest.json` 建小写 container 索引，`prefabContainer(name, {dlc})` 按 basename 精确/前缀匹配（`dlc` 参数做跨 DLC 同名消歧）；食材查不到再过别名表 `INGREDIENT_PREFAB_FIX`（如 `"corn"→"dlc11_corn"`），仍不行且在 `NO_PREFAB_INGREDIENTS` 黑名单则指向 orderdefinitions 资产。

**模板注释**（为何装饰物不用 MeshWithMaterial 变体）：

```js
/** Placeholder prefab for placeable props (mirrors Assets/common02 Blender/Airbed templates).
 *  装饰物也统一用普通 PseudoPrefabStub + PseudoPrefab：宿主 PseudoPrefab.Setup() 为空操作，
 *  不会因 materialSO 为 null 抛空引用（MeshWithMaterial 变体的 Setup() 会
 *  LoadAsset<Material>(materialSO)，materialSO 为空 → NullReferenceException）。 */
```

入口按模式分发：`REPAIR_DECOR / FOOD_DECOR_ONLY / RECIPE_DECOR_ONLY / DECOR_ONLY / PROPS_ONLY / 全量`——**策划清单驱动**（INGREDIENTS/RECIPES/COOKING_STEPS/PROPS(80)/PROPS_2(44)/COUNTER_APPEARANCES 均为手工校对白名单），`alreadyImported()` 扫 common01/02 已有 SO 的 m_Name 去重跳过。

### 9.3 三种 guid 策略的分层（不是一刀切）

| 脚本 | guid 策略 | 适用原因 |
|---|---|---|
| import-dlc-content.mjs / extract_floor_materials.py | **确定性 md5(prefix:id / 路径)** | 资产需要跨工具、跨机器重建（删库重跑 guid 不变；丢失后可凭 id 重建替身） |
| gen-commonw1-prefabs.mjs（backgrounds） | 随机生成 + **重跑保留**（`readGuid(metaPath) \|\| newGuid()`） | 只在本机增量维护；但 **fileID 确定性**（sha256(id) 派生 5 个 fileID，避免默认值撞车） |
| migrate-burger-recipes.mjs（commonW2） | **文件 + .meta 原样拷贝**（GUID 全保留，引用零改写） | 一次性迁移已有资产；仅打补丁（GUID_REMAP 修悬空引用、uID 统一 `UID_PREFIX*1000+i` 重排），引用闭包（BFS）决定 models/icons 收纳范围 |

### 9.4 repair-commonw1-pseudo-so.mjs：确定性 guid 的最佳注脚

背景：common03 对齐 upstream（`b499bd15e`）后，68 个 commonW1 prefab 引用的 SO guid 在工程里不存在 → `PseudoPrefab.ResetChild()` NRE。三路修复（**guid 全程不变，场景零改动**）：

```js
const detGuid = (id) => crypto.createHash("md5").update(`pseudo:${id}`).digest("hex");
// 路径 A：decor-entries.json 条目 → 按确定性 guid 重建 SO（写入 commonW1/pseudo_prefab_so/<镜像路径>）
// 路径 B：食材 SO 从 git 历史 b499bd15e^ 原样恢复 .asset+.meta（guid 不变）
const FOOD_SRC_COMMIT = "b499bd15e^";  // common03 对齐 upstream 前一版（食材 SO 尚在）
// 路径 C（回退）：剥掉 dlcNN_ 前缀后在 oc2-import/.cache/pidmap_*.json 里找基础版 prefab
```

**只有 guid 可预测（md5），才能在 SO 丢失后凭空重建一个引用兼容的替身**——路径 A 全靠算法可复算。

### 9.5 extract_floor_materials.py（六阶段）

manifest 过滤地板 Material（`EXCLUDE_RE = map_|background|sky|...`；`FLOOR_RE = floor|path|road|ground|blacktiles|pavement|woodslat|moss|tile`）→ 全量扫 StreamingAssets bundle 建 CAB→bundle 映射 → 解析 `_DiffuseMap` PPtr（texenv 优先级列表 `_DiffuseMap/_Diffuse/_Albedo/_MainTex/...`，兼容元组/字典两种 typetree 形态；跨 CAB 外部引用）→ UnityPy 解码 Texture2D 为 png（同名冲突加 `_<pathID % 100000>` 后缀）→ 写 Standard-shader .mat（`guid_for = md5('common03:'+rel)`）→ 汇总 `floor-materials-common03.json`。PPtr 解析失败的材质按名字在 dump PNG + common01/textures 里兜底找 `t_<stem>[_d].png`。

---

## 10. 脆弱点与自愈体系（专门承认"会脱同步"）

| 层 | 自愈机制 | 位置 |
|---|---|---|
| **meta ↔ AssetDatabase guid 脱同步** | `HealGuidDesync`：meta 直读 guid 与注册 guid 不一致时 ForceSynchronousImport 强制重导入 | `LayoutEditorCatalogApi.cs:10-25` |
| **运行时 bundle 加载失败** | `LoadAsset` 失败且非初始化期 → `DeInit(); Init();` 整体重载后重试 | `PseudoPrefabManager.cs:341-353` |
| **编辑器写回链** | `SafeReinit`（宿主对缺失 bundle 抛 KeyNotFoundException → try/catch + 预载 bundle + 补种子重试一次） | `LayoutEditorPseudoReload.cs:101-157` |
| **食材箱新皮肤** | `LayoutEditorDispenserIconHeal` 轮询守卫（域重载/退 Play 后反复补种子 + re-init） | `LayoutEditorDispenserIconFix.cs:396` |
| **真机 Missing Script** | loader 的 `HealScene`（tag 载体 → AddComponent 回填，见 05 §3.2） | `BepInExPlugins/.../Loader.cs` |
| **孤儿脚本引用** | `CustomStubOrphanRepair`（按字段签名分类，文本级 YAML 改写） | `Assets/Editor/LayoutEditor/CustomStubOrphanRepair.cs` |

**guid 稳定的通用纪律**：`.meta` 一旦生成就随资产入库、永不重生成；迁移时成对搬 .meta（`CustomStubCopyTool.cs:221` 的 `.cs` + `.cs.meta` 成对 File.Move）；`ForceReserializeAssets` 只重序列化内容不动 guid。

---

## 11. 启发性发现（反直觉/巧妙设计清单）

1. **1.132 空气墙魔法数 = "用几何尺寸当序列化标签"**：`SceneLayoutApplier.cs:2153`"1 格占地 = GridCellSize(1.2) × 1.132 高；1.132 为魔法数供导出识别"；导出侧用 `Mathf.Approximately(s.x, 1.132f)` 三轴计数识别空气墙。**不给隐形碰撞块加专用组件，尺寸本身就是协议**——与 pseudo SO"最小数据载体承载最大语义"同一种哲学。
2. **零宽字符绕编译器**：`PseudoPrefabPlayerStub.cs` 的 `Deprecated\u200c_Cap` 内嵌 U+200C（ZWNJ）——作者用不可见字符保留两个 "Deprecated" 前缀成员。能工作但 diff/grep 均不可见，危险。
3. **CarrotSO 的身世**：common01（"原版本体"库）的胡萝卜实际指向 `overcooked_legacy/dlc2`（bundle21）——OC2 大量复用 OC1 legacy 资产，pseudo SO 忠实记录真实出处，**不按主题归属美化**。
4. **三层 ID 体系各管一段**：原版菜谱小 uID / CustomRecipeSO `9990100` 段 / commonW2 `uidPrefix=58321` 自增段 / `LevelSetInfoSO` 的 `MD5(baseUID+version)` 版本化 uid。
5. **matchlist 双库并存**（§5.2）：同一目标资产两份代理（遗留驼峰零引用 vs 现役全小写），切换成本仅是改引用 guid。
6. **Stub 空类即协议**（§1.2）：一行注释的空 MonoBehaviour 成为编辑器↔真机两个运行时的契约点；`SpecificPseudoPrefabTag.prefabTag` 把 60+ 个 per-prefab 修补从资产层挪到代码层，避免 fork 原版 prefab。
7. **数据载体组件降级序列化**：随机箱候选/权重（`RandomCrate|<iconGuid>|w1,w2,...`）、PushablePot 槽位都编码进 tag 字符串/SOArray——stub 程序集缺失时数据不丢、自愈可回填。
8. **按依赖方划 bundle 边界**（§4.6）：common01_textures 独立成包不是按资产类型，而是按"谁是消费者"。
9. **两份 DIYLevelGameSession**（§3）：同构资产在不同 bundle 边界各司其职。
10. **common 引导包 Dependencies: []**：零依赖保证模组最先加载建立骨架，依赖方向完全单向。
11. **确定性 guid 生态**（§9）：md5 前缀命名空间 + guid 锚定 id 不锚定文件名 + 丢失后凭 id 重建——整套"引用稳定高于一切"的工程纪律，在脚本（File.Move 成对搬 meta）与资产两个世界一致表达。

---

## 12. 两套 bundle 的角色与加载关系

| bundle 集 | 位置 | 角色 |
|---|---|---|
| **原版游戏资源包** | `Assets/StreamingAssets/Windows/`（7.6GB，936 项，git 忽略） | 468 实体文件 = 269 个 `bundle\d+` + 199 个场景/主清单（s_* 144、throneroom/startscreen/worldmap 等 + Windows manifest）。**pseudo SO 的 bundleName/assetPath 全部指向这套包**，真实美术都在这里 |
| **本工程自建 bundle** | `Assets/AssetBundles/`（90MB，git 忽略） | `Tools/Build AssetBundles` 产出：common、common01、common01_textures、common02、common03、commonw1、commonw2 + 关卡集包（jia_carnival、oc1_story、test_level、tester_web…） |

**打标方式**：文件夹 `.meta` 的 `assetBundleName`（common01_textures 是唯一子目录覆盖者；commonW1 五个顶层显式标记、38 个嵌套空值靠最近祖先继承不阻断）；`AssetBundleTools.CleanFolderBundle` 批量清除标记。

**加载链路**：`LevelInfoSO.dependencies` 列 bundle 名 → `EnsureLoadAllAssetBundles()` 先加载 `Windows` manifest，再递归加载依赖闭包。自建包承载 stub/材质/自定义配方，原版包提供真实渲染资源，二者靠 pseudo SO 桥接：

```mermaid
flowchart LR
    LI["LevelInfoSO.dependencies<br/>（bundle 名列表）"] --> ENS["EnsureLoadAllAssetBundles()"]
    ENS --> M["AssetBundle.LoadFromFile(Windows)<br/>→ AssetBundleManifest"]
    M --> DEP["逐个加载 dependencies<br/>（含传递依赖闭包）"]
    DEP --> POOL["已加载 bundle 池"]
    SO2["PseudoPrefabSO<br/>bundleName + assetPath"] --> POOL
    POOL --> INST["PseudoPrefab.ResetChild()<br/>Instantiate 真 prefab 为 childGameObject"]
```
