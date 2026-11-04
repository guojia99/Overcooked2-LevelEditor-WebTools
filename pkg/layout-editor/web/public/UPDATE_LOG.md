# 更新日志


## 编辑器（未发布）
### 2026-11-04

- **多镜头分屏（SplitScreenCamera，运行时 4.1.0）**：相机弹窗新增「多镜头分屏」开关（`cameraInfo.splitScreen`）与**每镜头独立参考**（`cameraInfo.splitScreenCameras[]`：visible / 额外 offset X/Y/Z / FOV / 大小）；写回后在 Camera 子物体烘焙 `SplitScreen|2|C0:ox,oy,oz,fov,size,vis|C1:...` tag（v1 旧 C 段 offset 是基准位，迁移时按 0 忽略，避免双倍偏移）。运行时按场景内**本地厨师数量**自动分镜：单人双厨师 = 2 镜头、2/3/4 人 = 2/3/4 镜头；2 人左右 50/50，3 人上方 60% 宽居中 + 下方各 50%（左右黑边），4 人 2×2。**每个镜头直接克隆原版主相机 MultiplayerCamera，只把 m_avatars 限定为单个厨师**；`GetIdealLocation` 前后缀把 `m_basePos` 钉在厨师位置（始终居中），距离/高度沿用主相机 `m_minDistance`（前端画布参考也按此绘制），跟随平滑调到单玩家跟手；目标销毁时最后位置兜底并跳过原方法（修 MissingReference）。可选 offset/FOV/大小作为额外微调。**原版镜头世界画面屏蔽**（保留 Camera.main/AudioListener，cullingMask=0、clearFlags=Nothing），HUD Canvas 转 Overlay 全屏；分屏相机创建失败/厨师未就绪时自动回退原版相机，绝不留黑屏。每个镜头可单独调 offset/FOV/大小；取消「启用镜头」= 编辑器不画参考且运行时该玩家画面不生成（保留黑框）。**编辑器画布在分屏模式下按每个玩家位置绘制 4 个彩色镜头参考（主镜头参考隐藏，强制显示范围并在玩家处标注镜头编号），相机弹窗改为「镜头 1~4」分页单独调节并隐藏主镜头「出发点位置」**。模式为运行时单向，无切回单镜头入口。导出特征 `camera`；依赖包版本 4.1.0。

### 2026-11-03（二）

- **重写 commonW5 意面家族（v2，修复游戏内三处断裂）**：此前生成资产对原版 bundle 内资产做**直连 guid 引用**（BoiledPasta 节点 / 寿司鱼虾节点 / FryingPan 步骤 / 4 个 plated 模型），这些 guid 工程内不存在，运行时全部解析为 null——①`BoiledWrapper.asset=null` 且 `bundleName=""` 使 `PseudoPrefabManager.LoadAsset → GetAssetBundle("")` 抛 `KeyNotFoundException`，`LevelConfigSetup.SetupConfig` 中断（游戏内订单/食材全部不显示）；②`ComboLookup` 的 `m_prefab` 全 null，盘内组合视觉永远为空（鱼+意面等中间产物不显示）。v2 全部改为仓库既有铁律：原版资产一律经本地 `PseudoPrefabSO`（bundleName+assetPath 懒加载）或 `CustomRecipeSO`（`RecipeHelper` 运行时建真节点）引用；`ComboLookup.m_content` 用带原版 uID 的**本地克隆节点**（游戏匹配按 uID 值比较：`IngredientAssembledNode.IsMatch` 比 `m_uID`、Cooked 节点另比 cookingStep uID+progress），`m_prefab` 用拷贝进 `commonW5` 的 4 个原版 plated 模型（mesh→OBJ 走 kettle 同款导入管线 + 本地 `WebPlatedFood` shader + 本地贴图）。生成脚本 `layout-editor/scripts/gen-commonw5-pasta.mjs`（数据采集 `layout-editor/scripts/pasta/collect_pasta_assets.py`，校验 `validate_pasta.py`）。
- **意面菜谱语义与新增两道菜**：`Web_Seafood_Pasta`（海鲜意面 80 分）改为**煮意面+煎寿司鱼+煎寿司虾全必选**；新增 `Web_Fish_Pasta`（鲜鱼意面 60 分 = 煮意面+煎鱼）、`Web_Prawn_Pasta`（鲜虾意面 60 分 = 煮意面+煎虾），图标暂用海鲜意面同款占位（guid 固定，后续直接替换 PNG）。中间产物 `Web_Seafood_Pasta_{BoiledWrapper,FishNode,PrawnNode}` 改为 `CustomRecipeSO{type=Cooked}`（score=0，组成 = common01 `PastaSO`/`SushiFishSO`/`SushiPrawnSO` 懒加载 + common02 `Pot`/`FryingPan` 步骤），订单卡 tiles 由 `RecipeHelper` 运行时自动生成；旧 `pseudo_prefab_so/pasta/` 目录删除（`BoiledWrapper` 迁至 `custom_recipes/pasta/orderdefinitions/`，**换用全新确定性 guid**——跨路径搬移复用旧 guid 会与 Unity Library 的 guid→旧路径缓存映射冲突，触发 `Failed to unload` 断言）。
- **菜谱页 Optional 新增「意面料」一键填充**：`/api/level/optional-presets` 返回 `pastaToppingGuids`（煎寿司鱼/煎寿司虾中间产物），一键注册进关卡 `optionalRecipeMatchListItems`（recipeMatchList optional 通道）后，煎好的鱼/虾可**单独装盘上菜**（`GetOrderPlatingPrefab` 命中其 `m_platingPrefab`，视觉 = 盘装煎鱼/煎虾）。

### 2026-11-03

- **一键网格对齐（核心层 / 地板层）**：工具栏「👁 图层显示」右侧新增「🧭 一键对齐」按钮，点击即把**当前层全部物件**（无视选中状态）对齐到**最近的格子**、填满整格——奇数跨距中心落整格点（如 1×1 食材箱 6.00/-1.20，而非 6.60/-1.80 半格位），偶数跨距中心落两格之间（2×1 水槽与游戏摆法一致），<1 格小件（0.9 格锅/盘/杯）中心直接贴整格点（核心层 = 当前 P 全部核心物品含玩家，地板层 = 全部非背景地板含主题/图片/空气/木筏，木筏按拼板整格点对齐）；火锅灶台/大锅的 stub 偏移自动保持，锅具对齐后仍会吸附回灶台。整批一条 undo 记录可 Ctrl+Z，不受「🧲 吸附格子」开关影响；装饰 / 背景 / 动画 / 灯光层按钮置灰禁用。

- **修复 `Web_Seafood_Pasta` Play 报 `CookedCompositeOrderNode.Convert` NullReferenceException**：自定义鱼/虾 OrderDefinitionNode 的 `m_composition` 误引用了 `SushiFishSO`/`SushiPrawnSO` 包装 SO（`PseudoPrefabSO`），运行时转换为 `OrderDefinitionNode[]` 后为 null；已改引实际 `SushiFish`/`SushiPrawn` OrderDefinitionNode（GUID `183b14d0…` / `20167b8e…`），生成脚本同步修正。

- **修正蒸笼自动填充与寿司米饭锅具**：寿司饭（`SushiRiceSO`）菜谱步骤由 `Steamer` 改为 `Pot`，Web 菜谱卡与自动填充不再把米饭放进蒸笼；旧场景打开/写回时自动移除蒸笼里残留的米饭。蒸笼里的虾面团只在 Web 显示面团本身，面粉/寿司虾仍写入运行时 `allowedIngredientSOs` 供 `CookableContainer` 的 MixedComposite 校验，但导出时隐藏这些技术组成项。

### 2026-11-02

- **新增 commonW5 意面菜谱**：`Web_Seafood_Pasta`（80 分，意面 + 可选寿司鱼/寿司虾），盘内模型按实际可选组合自动切换（意面 / 意面+鱼 / 意面+虾 / 意面+鱼+虾），uID 58323015。三个中间产物包装放在 `pseudo_prefab_so/pasta/`（不进入 Web 菜谱列表），Web 菜谱卡直接显示原材料 `PastaSO` / `SushiFishSO` / `SushiPrawnSO`；包装为本地 `asset` 直引 OrderDefinitionNode，不依赖 `commonW5` bundle 加载。生成脚本 `layout-editor/scripts/gen-commonw5-pasta.mjs`。
- **新增 commonW5 蒸菜菜谱**：`Web_Steamed_Fish`（Web 蒸鱼 40 分，组成 `SushiFishSO`）与 `Web_Steamed_Prawns`（Web 虾烧麦 80 分，组成 `FlourSO`+`SushiPrawnSO` 的自定义面团中间产物 `Web_Steamed_PrawnPrep`）；寿司、蒸菜、烧麦统一使用 `SushiFishSO` / `SushiPrawnSO` 作为唯一鱼虾。成品模型复用原版 `plated_steamedfish` / `recipe_dimsum_03`，新增共享库分类「蒸菜」，uID 58323012–58323014。生成脚本 `layout-editor/scripts/gen-commonw5-steamed.mjs`。
- **写回自动 Prepare For Building**：保存场景前由 `LayoutEditorPseudoReload.ForcePrepareForBuildingForExport` 对活动场景所有 `PseudoPrefabManager` 强制 `prepareForBuilding=true` + `DeInit()`，不再弹「先点击 Tools - Toggle Prepare For Building」提示，也不会把临时伪 prefab 子物体写进场景。
- **修复写回/导出保存场景时的 Missing Script 报错**：转换得到的 Jia 等场景含大量 `m_Script` 为空的旧 MonoBehaviour（如 `lightmapIndex` 残留），`SaveScene` 会刷 `Could not extract GUID / Broken PPtr`。`SceneLayoutApplier` / `LayoutEditorSetExporter` 在保存前调用 `LayoutEditorMissingScriptCleanup` 移除这些空脚本组件，`jia_level_kevin_3` 写回后不再产生该批报错。
- **修复自定义搅拌子菜谱的终锅输入**：自定义 Mixed 中间产物（如虾烧麦面团）现在会作为终锅组食材显示（蒸笼/煎锅不再留空标记）；自动填充与 `LayoutEditorSteamerFill` 除了写入面团节点，还会把其组成食材（`FlourSO`/`SushiPrawnSO`）写进终锅 `allowedIngredientSOs`——`CookableContainer.AllowItemPlacement` 对 MixedComposite 会逐个检查组成食材，只有面团节点时会被判为不可放入。搅拌碗同步补 `FlourSO`/`SushiPrawnSO`；场景打开/域重载时自动执行，手动放置的蒸笼也能直接放入寿司鱼和虾面团。
- **修复开关/压力地板调色把原版按钮高光压掉的问题**：`CustomStub.SwitchTint` 现在改为乘法染色（OC2 gloss 写 `_MaskColor`，Standard 写 `_Color`），保留原贴图、法线以及 `_Metallic`/`_Roughness`/`_RimIntensity`/`_Brightness`/`_highlight_colour` 等光照参数——调色后的开关与未调色原版按钮保持同一亮度、金属度、边缘光与高光，避免纯色覆盖/压光造成的过亮或哑光。**依赖包需重新导出一次**才能让真机吃到新运行时。
- **默认开关外观不再显式写入材质**：外观材质与 prefab 默认一致时导出为空 guid，右键面板显示「— 默认 —」；两个调色都未设置时不生成 `SwitchTint` helper，直接使用原版开关预制体。

### 2026-11-01

- **修复未启用「按钮控速」的传送带/步带写回报错**：Unity GET 会把 null 的 `speedControl` 导出成 `{enabled:true,levels:[]}` 占位对象，旧版右键面板渲染时又自动补默认档位，导致复制出来的传送带全部带上未绑定开关的控速配置并报「部分写回失败」。现在加载/序列化会清理不足 2 档的占位配置，面板不再在渲染时自动启用；后端只烘焙明确启用的目标，未绑定开关改为写回告警（写回成功）。
- **修复食材生成器附件空槽导致 `PseudoPrefabManager` NRE**：写回 `AttachingFoodSpawner` 的 `attachmentPrefabSOs`/`weights` 改走 `SerializedObject`（数组增长时 Unity 2017 prefab override 会漏写新元素，留下 null 槽触发 `PseudoPrefabManager.LoadAsset` NRE），并过滤无法解析的 guid；导出跳过空槽，避免空附件再次回传。
- **补齐原版装饰的动画音频接收器**：`flyingpigeon` / `exterior_car_blue_01` 等原版 prefab 的根 Animator 播放含 `AudioTrigger` 事件的剪辑，但根物体没有 `AnimatorAudioComponent`，Play 时刷 `AnimationEvent 'AudioTrigger' has no receiver`。编辑器现在进入 Play 后自动扫描 Animator 控制器剪辑事件，给缺失接收器的物体补挂 `AnimatorAudioComponent`（仅编辑器 Play，不改场景资产/原版 bundle）。

### 2026-10-30

- **压力地板 / 开关调色**：右键「基础」页新增未按下/按下两组颜色（普通开关、大炮开关、压力开关、DLC13 莲花压力地板）。颜色采用乘法染色（保留原贴图与光照/法线明暗）；编辑器场景视图显示未按下色（初始关闭的开关显示按下色），Play/真机按占用/按压状态实时切换。写回在 `Design/Switch Tint Logic` 下生成 `CustomStub.SwitchTint` helper（tag `SwitchTint|T:…|I:…|P:…` 自愈），不改原材质、不生成材质资产。**依赖包需重新导出一次**（运行时版本仍 4.0.0，旧包无 `SwitchTint` 组件时染色不生效、关卡本身不受影响）。
- **修复压力开关重载 MissingReferenceException**：根因是「外观」下拉曾把 `Switch.asset`/`PressureSwitch.asset`（指向 prefab 的非材质 SO）当材质选中；宿主 `LoadAsset<Material>` 失败会触发 `DeInit/Init` 重入，在 `Setup` 中销毁 child 的 `Pad`。现在外观列表只保留 `.mat`，场景中已有的非材质 SO 自动回落 prefab 默认/标准材质；另加 `Setup` Harmony 前缀与 `[pseudo-repair]` 兜底重连 `m_buttonBit`。真机不经过 LevelEditor 伪 prefab，不受影响。
- **修复关卡时长 0 导致 Play 报错**：`roundTime=0` 会让 `Value/GetTimeLimit()` 产生 NaN，`ProgressBarUI.UpdateFill` 写 `SubImage.localScale` 报错；关卡配置保存/路由层现在把非法时长兜底为 240s，运行时 `LevelConfigSetup` 也做同样兜底，并修正 `jia_level_1_6` 的 1P~4P 配置。
- **新增 commonW5「上菜传送带站」**：调色板「核心 · 机关」新增 `web_conveyor_station_serve`（传送带站拷贝，独立 tag `ConveyorServe|`）。可识别运动方向末端的上菜台并把物品送上去：盘子正常上菜；非盘子视为无效上菜，断小费/连击并消耗物品。编辑端写回/导出自动维护 `CustomStub.ConveyorServe` 组件与 tag（与方向同步组合时 `ConveyorServe|dir`），运行时按需安装 Harmony 前缀；普通传送带行为不变。
- **压力开关移入核心层**：`PressureSwitch` 与 DLC13 莲花压力开关（大/小）从地板层移到核心「机关」分组，仍按表面物铺地；`PressureSwitch` 默认占地修正为 2×2（2.4m×2.4m），大莲花同为 2×2，小莲花保持 1×1；写回改用「仅核心物品」或全量。
- **DLC5 自动门移入核心层**：`p_dlc5_door_01` 从装饰层移到核心「门 / 自动门」分组，默认父级 `Art/Scenery`。
- **新增联合组合「压力地板 + DLC5 自动门」**：按住式联动——站上压力板门打开并保持，离开后关闭；动画组固定两个 rotate 事件（站上开 / 离开关），压力开关 Enter/Exit 直连门组根 Animator，不经过按钮 helper。
- **压力开关「联动」快捷页**：右键压力开关 →「联动」→ 选择场景中的任意门（DLC / 主题自动门）即可一键创建或追加按住式动画组；已联动门可单独移除，也可直接打开动画组编辑。画布与 3D 视图新增压力板→门橙色连线。
- **门旋转参数放到门自身**：右键门 →「门 · 旋转参数」，可设开门方向（默认顺时针）、角度、时长；门不强制吸附格心，模型根/旋转轴即摆放点（可放格子左上角 0,0）。参数以单门覆盖写入按住式动画组（`doorMotions`），同一压力开关绑定多扇门时互不影响。关门始终从开门终态反向归位（修复关门跳 180°）。
- **门扇碰撞**：写回时对门 prefab 自动烘焙 `BoxCollider`（Walls 层，尺寸优先取 `exports/decor/models/<guid>/model.obj` 实测 AABB，DLC5 门有精确兜底），碰撞体随动画组旋转——关门挡住门洞、开门旋到侧面让路。
- 按住式组新增回读保障：纯旋转组也会写入成员 `hierarchyPath`，重新加载/导出后不会退化为片段分析丢失 `advanceMode="hold"`。


## Web 资源导出（未发布）
### 2026-10-29
#### 重大更新
- **Web 导出产物统一迁移到 `exports/`**：音频、桌台图标/3D、装饰物不再写入 `layout-editor/web/public`，改由后端接口读取。
- **装饰物双视图 + 高清详情 + 3D**：导出正面/背面 192×192 缩略图（`exports/decor/front|back/`）、高清详情图（`exports/decor/detail/front|back/`，默认 1024，大件按包围盒 ≥5m 用 1536、≥12m 用 2048）与 3D 模型（`exports/decor/models/<guid>/model.obj|mtl` + 共享贴图）。详情面板左右并排展示；选中装饰物时画布右上角预览也并排显示；双视图、高清详情与 3D 统一在一个预览弹窗：默认正面，点击「3D」页签才请求并渲染模型；图片/模型请求期间显示加载动画与进度条。右键装饰物只有一个「🖼 预览」按钮（独立一行），卡片仍提供 🔍 / 🧊 快捷入口。
- **一键导出 Web 资源**：关卡编辑器顶栏「🌐 导出web预览资源依赖」可按类别导出/强制重导，长任务轮询状态；Unity Bridge 与菜单也提供入口。
- **移除旧静态地址**：`/icons/decor-preview/*`、`/audio-exports.json` 不再提供；桌台图标改走 `/api/counter-skins/icon/*`。
- **导出稳定性**：安全实例化移除编辑态运行时组件，消除 `SetupCustomPrefab.Awake` NRE 与 `Camera.targetTexture` 渲染纹理告警；两个超大 Timeline prefab（`Timeline_DynamicStage_04` / `Timeline_Tutorial_01`）因 Unity 2017 克隆断言，保留既有正/背预览、不参与强制重导。

#### 接口
- 新增 `GET /api/web-assets/manifest`、`GET /api/web-assets/decor/manifest.json`、`GET /api/web-assets/decor/front|back/<guid>.png`
- 新增 `GET /api/counter-skins/icon/<file>.png`、`GET /api/counter-skins/icons/manifest.json`
- 新增 `POST /api/web-assets/export`、`GET /api/web-assets/export/status`
- `GET /api/audio/exports` / `/api/audio/stream` 数据源改为 `exports/audio/`

## v0.11.0
### 2026-10-28
#### 重大更新
- 新增commonW5
- 预适配GUA v0.11
  - 本版本与v0.10 存在部分不兼容

#### 新增commonW5
- 感谢派大星老师、rw老师提供的相关资源材料、文档!!!
- 新增OC1 Music、好吃 Music
- 新增rw卷饼菜谱
- 新增好吃的茶叶、辣椒
- 新增一些装饰桌台

#### 功能
- 支持装饰物预览

#### 适配
- 适配GUA最新代码
- 支持各种自定义
  - 自定义锅具、食材、桌台等

#### 修复
- 修复安装器无法自动安装commonW3后面的其他commonW*
- 修复汇总页web菜谱归纳为未分类的问题
- 修复可移动火锅无烧糊画面的问题
- 修复菜谱清单中 FriedChicken 缺少图标与「煎」步骤的问题（Recipes_Patch 未纳入菜谱知识库扫描，Bridge 也硬编码了空步骤）


## v0.10
### 2026-10-13
#### 重大变更
- 新增 commonW4
- 新增 Web 背包，支持放入、取出
- 前置依赖升级至 v3.10.x
- 支持分P管理功能
#### AI
- 支持 AI MCP 接口，便于 AI 接入与扩展
#### 新增功能
- 支持智能换 P 关卡
- 新增厨师头灯
- 新增随机触发器
- 支持更多转场动画
- 支持关卡部分导出
- 支持摇杆控制地板
- 装饰层支持批量屏蔽部分装饰物
- 装饰层支持装饰物分组
- 喷火器支持可编程配置
- 关卡截图支持直接截取 Unity 游戏画面
- 支持可编程的随机信号
- 新增灯光层，支持更精细的灯光管理
- 新增 Web 背包
  - 支持放入和取出物品
  - 可用于搬运食材
- 支持开关控制移动步带的速度
- 支持开关控制食材生成器触发送出食材
- 支持鬼魂
#### 修复与优化
- 弹窗支持层级记忆与回退
- 优化关卡导出流程
- 优化截图上传方式及截图 UI
- 修复部分 Web 内置汤的模型错误和菜谱配置错误
- 修复卷饼标识中米饭使用错误的问题，原为寿司米
- 修复锅具自定义时间卡死问题
- 修复餐盘和盘子共存问题
- 支持关卡编辑器左侧显示素材卡片详情
- 优化开关的联动UI

## v0.9.4
### 2026-10-06
#### 重大变更
- 前置更新到v3.7.0
#### 修复
- 修复Loader的一些加载bug

## v0.9.3
### 2026-10-02
#### 修复
- Optional功能修复
- 修复打包流程管理

## v0.9.2
### 2026-10-02
#### 功能
- 新增桌台管理
- 新增桌台预览模式

## v0.9.1
### 2026-10-01
#### 重大变更
- commonW3更改菜谱
#### 修复
- 修复橄榄披萨的错误
- 修复炒饭、布丁错误
- 修复背景区界面
#### 更改
- 修改关卡列表排序弹窗样式
- 修复填充菜谱错误

## v0.9.0
### 2026-09-30
#### 致谢
- 感谢🍐老师贡献的庞大菜谱列表
- 感谢呆喵老师贡献的菜谱列表
#### 重大变更
- 页面支持四个主题
  - 黑金
  - 粉红
  - 天蓝
  - 纯白
- 动画组功能和流程重构
- Loader.dll版本更新到v3.6.0
- 新增commonW3
  - 菜谱和蟑螂、老鼠模型
#### 菜谱
- 新增commonW3
  - 新增各类沙拉和果汁
- 新增各类菜谱
  - 冰沙
  - 沙拉
  - 冰淇淋
  - 炒饭
  - 布丁
  - 果汁
  - 汤
#### 动画
- 重构动画组UI
- 重新设计动画组流程
- 重新完成所有动画组联动工作
#### 关卡
- 支持多个关卡集打包到一起
- 支持关卡截图直接生成导入
- 支持修改关卡level名称和文件名
- 关卡不再自带s_
- 支持关卡集修改集合名称
- 汇总页支持说明和多玩家菜谱分工
#### 音频
- 支持音乐导入和压缩
#### 模型与自定义
- 支持修改菜谱模型面数，减少打包的文件大小
- 支持老鼠可自定义模型
- 老鼠支持内置蟑螂模型
#### 开关与机关
- 支持开关组合
- 共轭开关
- 互锁开关
#### 场景与编辑
- 支持天花板高度调整
- 支持可移动斜坡
- 支持当前页面拷贝可跨页面拷贝
- 支持三个维度的旋转
#### 其他
- 支持工作量推测(实验版本)
- 默认精度设置为0.1，支持更多精度

## v0.8.4
### 2026-09-21
#### 重大变更
- Loader.dll更新到v3.0.0
- 新增debugLog.dll， 用于辅助定位日志
#### 功能
- 优化菜谱一些ui交互体验
#### 修复
- 修复部分导致卡顿的问题
- 修复多张使用web图集合时导致无法打开的问题


## v0.8.3
### 2026-09-18
#### 重大变更
- 本版本需要更新前置
- commonW2 新增老鼠、肉排意面（之前是肉馅意面）
#### 修复
- 修复加农炮空按钮可触发导致的bug
#### 功能
- 自动编译前置DLL依赖
- 3D模式（beta版本）
- 老鼠（beta版本）

## v0.8.2
### 2026-09-18
#### 修复
- commonW2 统一鸡肉
- 修复地板无法为负高度

## v0.8.1
### 2026-09-17
#### 修复
- 修复了同步关卡布局的一些z轴bug
- 支持一键换自定义菜单的面包皮
- 补充了一部分皮肤

## v0.8.0
### 2026-09-15
#### 重大变更
- commonW2汉堡重构，使用了commonW2汉堡的老师需要注意重新导入和重改食材箱等。
- 重写loader机制，关卡集不再使用内置runtime
- 将commonW1、commonW2迁移到单独的文件夹维护

#### 功能
- 支持传送带地板定时反向
- 支持页面更多皮肤
- 优化导出页面样式

#### 修复
- 修复传送门单向错误
- 修复部分饮料机、酱料机错误的无法选择酱料等


## v0.7.7
### 2026-09-13
#### 修复
- 修复汉堡大全的汉堡错误

## v0.7.6
### 2026-09-12
#### 注意
- 本次更新涉及commonW2, 打包后的产物需要更换
- 本次更新涉及 OC2LevelRuntimeLoader（v1.7.1），打包后的产物需要更换（zip 内 loader.dll 随包自动更新）
- Loader 生效机制改为按需加载：v0.7.6 早些时候导出过且调整过相机位置的关卡包，需重新打开该地图微调一次相机位置再导出，相机偏移才会在新机制下生效

#### 功能
- 装饰层支持分类
- 补充了一些装饰物
  - commonW2更新
- 支持材质地板查看材质
- 支持调整工作台交互范围
- 修复相机「出发点位置」调整后，Play/真机里被还原回原始位置的问题：相机跟随改为以场景摆放位置为中心（保留开场运镜与跟随微调，未调整过机位的关卡行为不变）
- 修复相机偏移补丁导致所有关卡帧率下降的问题：相机偏移改为按需生效——只有调整过出发点位置的关卡（导出时自动携带 runtime 关卡代码）才启用，其他关卡（含官方关卡）完全不受影响
- 相机出发点位置支持长按方向键连续微调，X/Z 可直接输入数字
- 所有物品支持修改缩放（右键菜单 ±0.1、详情面板输入精确值，最小 0.1×，写回后游戏内生效）；新建物品缩放统一默认 1/1/1（食材箱不再默认 1.2；旧关卡里 1.2 的食材箱保持原值，可手动改回）
- 修复自动填充的食材箱在画布上比工作台大一圈的问题（根因是新建食材箱默认带 1.2 缩放，现已统一默认 1/1/1）
- 修复动画层无法按住空格拖动画布的问题：空格改为「按住拖动 = 平移画布，点按（未拖动）= 播放/暂停预览」
- 菜谱弹窗打开前增加加载动画过渡（与其他弹窗一致）

## v0.7.5
### 2026-09-11 上午
#### 注意
- 本次更新涉及commonW1和commonW2, 打包后的产物需要更换
  - 不影响旧包

#### 功能
- 支持相机修改初始固定的视角
- 新增随机箱可拿出随机空气

#### 修复与优化
- 优化随机箱算法
- 修复最大菜单大于5时，UI不显示的问题
- 优化火锅行为
- 优化commonW2的汉堡模型映射


## v0.7.4
### 2026-09-09
#### 修复
- 修复部分菜谱和食材无法映射到食材箱的bug


## v0.7.3
### 2026-09-09
#### 新增
- commonW2
  - 目前支持预设的汉堡
    - 感谢Rw的Burger100
    - 感谢呆喵喵的超级无敌雷霆大汉堡

#### 功能
- 完善自定义菜单
- 初步支持汉堡菜单制作，单独的汉堡拼接制作
  - 支持统一的optional写回

#### 优化
- 完善Stub目录结构
- 完善Stub日志体系
- 优化了一些自定义菜谱体验问题

## v0.7.2
### 2026-09-07
#### 功能
- 支持历史记录恢复
- 新增锅具烹饪时间管理
- 支持optional、match list单独管理
- 完善了CustomStub
- 完善了打包导出功能

#### 修复
- 修复自定义菜单、菜谱等的一些遗留bug
- 锅具管理的写回参数错误
- 菜谱管理中关于成品菜的使用组合问题


## v0.7.1
### 2026-09-07
#### 关键说明
- 本版本支持锅具时间配置。**只改「煮熟/混合」时间无需 runtime**；配置了特殊「煮糊/过度混合」时间（≠2×）或任一时间参数的关卡，导出 zip 会自动携带 runtime bundle（应用逻辑在 CustomStub.UtensilTiming，随关卡包分发）。
- 不改动 LevelEditorStub / LevelEditor：时间参数走「CustomStub 组件（权威）+ UtensilTiming| tag（自愈）」双通道（TimedCookingSwitch 同款），游戏侧无需更新 LevelEditorStub.dll。

#### 新功能
- 锅具管理（弹窗/右键）支持时间参数，按锅具类型区分文案：
  - 烹饪类（煎锅/汤锅/蒸锅/烤盘/蛋糕模/油炸篮/火锅/铁板/烤串/烤叉等）：煮熟时间 / 煮糊时间
  - 搅拌类（搅拌机/搅拌杯）：混合时间 / 过度混合时间
  - 搅拌碗（可进烤箱烤蛋糕）：双组时间（混合/过混 + 烤熟/烤糊）
  - 可推动火锅同样支持（煮熟/煮糊）
- 时间留空 = 原版默认（煮熟约 10-12s、煮糊 = 2× 煮熟）；「同步给其他」按钮会连同时间一起同步
- 煮糊/过度混合支持独立于 2× 的特殊值：服务端判定（火警/订单烧糊）、状态机（Completed/OverDoing/Ruined）、客户端进度条橙色预警区全部按新阈值换算（OverDoing 预警起点 = 煮熟 + 30%×(煮糊−煮熟)，与原版 1.3× 语义对齐）


## v0.7.0
### 2026-09-04
#### 关键说明
- 本版本依赖于GUA 编辑器v0.10版本，旧版本不适配！
- 本版本新增了核心插件，用于动态载入c#代码，以提高游戏灵活度， OC2LevelRuntimeLoader.dll, 使用了下述道具时，不安装游戏无法正常运行
  - web可移动火锅
  - 随机食材箱
- 本版本重新修正了编译，将c#代码打包进入游戏依赖
- 本版本单独使用了一些commonW1的内置自定义资源，方便统一维护。

#### GUA
- 支持v0.10[-1]版本全部代码
- 引入Loader机制，支持动态载入关卡集单独的runtime代码。

#### 新功能
- 支持随机道具箱
- 动画组支持旋转和并行，支持时间轴控制
- 支持更多的装饰物，比如使用食物装饰
- 便捷的换场景功能
- 动画组新增动态镜头抖动特效
- 动画组新增闪电特效

#### 优化
- 修复分数配置的偏差和错误
- 修复一些菜谱配图错误


## v0.6.0
### 2026-08-31
#### 新功能
- 新增马克杯、餐盘、饮料机、酱料机、断头台、大火锅、奶油喷枪等核心道具
- 新增约80道菜谱，包含布丁、沙拉、汤、甜甜圈、水果拼盘、汽水冰淇淋、水果派、月饼、热可可、汉堡拼盘等
- 新增开关事件组功能，一个按钮可触发多个事件
- 支持酱料机、饮料机、断头台和开关联动
  - 附带合并的联动机关可一次性加入地图
- 支持相机修改
  - 支持背景颜色调整
  - 支持调整相机视距
- 支持灯光修改
  - 修改灯光暖度、强度等
- 锅具管理
  - 支持一键补充非主线食材
- LevelInfo管理
  - 支持回填时自动补充dlc资源集
- 新增功能说明
#### bug和优化
- 合并重复的物品，支持更改物品来源于哪个DLC
- 优化自定义菜谱渲染卡片
- 调整自定义菜谱错误的icon逻辑
- 自定义菜谱支持选用自定义菜单
- 优化背景使用
- 优化地板的调整说明

#### common
- common03已提交给GUA
- commonW1
  - 支持更多地板材质
  - 支持食材、菜谱作为装饰物


## v0.5.2
### 2026-08-15
**支持**
- 支持GUA最新代码分支v0.9版本
- 支持min count、max count参数配置
#### **功能**
- 新增空气地板、空气墙
- 优化菜谱功能
- 支持莲花压力开关
#### **bug**
- 修复玩家碰撞问题

## v0.5.1
### 2026-08-13
- 优化菜谱3D模型，UI上传配置等
- 修复大量模型问题


## v0.5.0
### 2026-08-12
- 完善自定义菜谱
- 支持了自定义移动事件
- 支持工作台的抬起、落下、等待事件等

## v0.4.0-beta
### 2026-08-08
- 适配 gua 老师的新代码
- 完成大部分自定义菜谱功能

## v0.3.4.1
### 2026-08-07
#### **修复**
- 修复关卡箱子精度问题

## v0.3.4
### 2026-08-06
#### **修复**
- 修复一些菜单 bug

**新增**
- 新增关卡汇总页

## v0.3.3
### 2026-08-05
#### **修复**
- 修复锅具最低高度被限制的问题
- 修复默认模板玩家碰撞 bug
- 修复菜单的错误食材、锅具

**新增**
- 支持输入坐标、高度坐标
- 支持音频查看
- 支持菜单内容自动回写，支持编辑器 v0.9 的自动补全功能
- 优化食材菜单 UI
- 支持锅具可指定中间产物

## v0.3.2
### 2026-08-04
#### **修复**
- 修复装饰物回写碰撞冲突和消失、移位问题
- 修复物品重叠问题
- 修复框选时左键弹窗的问题

**新增**
- 支持分层单独回写
- 支持物品 z 轴控制
- 支持关卡截图上传和配置
- 支持部分核心物品的个性化配置
- 优化布局编辑页 UI
- 支持物品检视
- 支持锅具食材搜索

## v0.3.1.1
### 2026-08-03
#### **修复**
- 修复菜谱相关的 bug

**新增**
- 优化菜谱界面

## v0.3.1
### 2026-08-03
#### **修复**
- 修复披萨菜谱添加食材箱无法添加芝士和番茄
- 修复返回空参数的食材箱导致的指针越界问题
- 修复关卡菜谱使用问题

**新增**
- 简单支持开关、多路控制终端的使用

## v0.3.0
### 2026-08-02
- 支持各类地板
- 修复音频问题
- 优化排版和 UI
- 预先支持 v0.9 Editor

## v0.2.2.1
### 2026-08-02
#### **修复**
- 修复中文 bug

## v0.2.2
### 2026-07-31
**新增**
- 新增 Nav 导航，方便后续功能植入
- 添加两个快捷键
- 离开编辑器时提醒回写到 Unity
- 新增一键同步
- 重叠的物品、地板现在可以方便选中
- 新增上菜口和脏盘的双向绑定
- 新增 AI 自动评分功能
- 新建关卡、关卡集时自动写好打包所需参数
- 食材生成器支持出菜方向
- 完善玩家管理

#### **修复**
- 增加禁止中文文件名的限制
- 微调背景水的默认位置错误，避免漏出空洞位置
- 奇数宽度地板不再在回填时有空隙

## v0.2.1
### 2026-07-30 19:00
- 支持多角度旋转
- 核心组件支持界面修改参数

## v0.2.0
### 2026-07-30 17:45
- 支持背景、地板管理，以及关卡管理、菜单和食材校验

## v0.1.0
### 2026-07-30 02:29
- 完成基础功能研发
