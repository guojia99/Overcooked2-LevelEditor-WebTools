# Horde 与 Dynamic 原生模式指南

适用基线：用户提供的上游九提交已同步至 `4d6cdb2`，本仓库对应 cherry-pick 末端 `77e644a45`（dynamic template scene）。本指南描述当前后端/模式适配契约，不把上游同步、离线编译或 mock 测试视为真机验收。

## 选择模式

| 模式 | 原生数据/控制器 | 编辑入口 |
|---|---|---|
| Campaign | CampaignFlowController，原有 recipes / scriptedRecipes | 原有菜谱流程 |
| Dynamic | DynamicFlowController，recipeListSOs + roundTimes，有效原生转场 | 模式配置 phases[] |
| Horde | HordeFlowController，1P–4P 独立 HordeLevelConfigSO 与波次/菜单 SO | 模式配置 hordeConfigs[4]、生命与修复参数 |
| Web 分 P | partLevel、布局归属/动画、独立 sidecar | 原有分 P 编辑器；不是第四种原生 gameMode |

新建优先选择正确原生模板。REST `POST /api/level/create` 接收 `{setName,levelId,levelName?,levelNameZH?,gameMode?}`；gameMode 缺省 Campaign，可选 Dynamic/Horde。模板分别位于 `Assets/Template/campaign|dynamic|horde`。MCP 使用 `oc2_create_level` 并显式 confirm=true，自动从列表取得真实资产路径后读回。

创建会完整复制所选目录的私有 YAML 资产并重映射引用，私有材质与 Dynamic controller/clip 保存在本关 `data/<level>/template_assets/`，不再共享模板动画/材质；外部公共依赖保留。Campaign 默认菜谱、Dynamic 默认阶段/时长/parenting/respawn 和 Horde 四人数波次/参数均继承所选模板。上游 Dynamic levelinfo 的四个 config 错指 Horde，在副本计划中按 Dynamic 文件名纠正，源模板不修改，也不复制 Horde 依赖。Horde store 完成独立图后删除被替代的本次中间 config/波次/菜单，避免根 config 残留。

复制前校验所选目录、配置、引用及唯一 LevelInfo 场景绑定；不打开源模板。新场景打开前完成本关 GUID 绑定、模式 SO 初始化和 bundle 标签清理，非场景资产继承集 info，场景单独打包。缺失/不支持格式/跨模式私有引用会拒绝创建，关卡集根 bundle 须为 `<set>/info_<set>`；发生后续错误则恢复列表并清理新路径，清理失败会在错误中列明残留，不能因收到失败就直接重试。

Web 入口：画布顶栏「关卡配置」→「模式」；管理页关卡详情顶栏「关卡模式」直接打开相同页签。模式页签使用其内部的保存/切换按钮，底部通用「保存全部」在此页签隐藏。画布或其它配置页签有未保存修改时阻止模式切换，成功后重新加载当前场景，避免旧画布覆盖新模式。

敌群道具在画布「核心层」→「核心 · 敌群」独立分类，收录 Horde 服务窗口和 Horde 铁门，不再分散在出餐/加工及普通门分类。敌人波次在模式面板选择，不作为可直接摆放的装饰物。

## 编辑 Dynamic

先读取 `GET /api/level/mode-config?assetPath=<LevelInfoSO>`，在完整 DTO 上编辑 phases。每阶段包含稳定 Web ID、整数时长和菜谱 GUID 菜单，顺序即运行顺序；2–64 阶段，每阶段 1–86400 秒、1–256 道菜。菜单保存会同步为本关专属 RecipeListSO 与 roundTimes，不改共享模板。

N 个阶段需要 N−1 个有效原生 AnimatedDynamicTransition。修改阶段数不会凭空生成地形转场；必须检查本场景 FlowManager 下的原生 Animator/trigger/IsTransitioning 绑定。Web 分 P transition 或普通 Web 动画组不能替代这些原生组件。MCP 参数、限额和读回步骤见[原生模式接口](mcp/10-原生模式接口.md)。

## 编辑 Horde

按 1P、2P、3P、4P 顺序分别配置 hordeConfigs，不能只写当前人数那一项。每份含 targetRepairCostMax 和 1–128 波；每波需 ID、intervalSeconds、recipeGuids、enemyGuids、spawnTimes。敌人与生成时间按下标一一配对，可让多个同种敌人在同一秒出现，不要对数组去重。

敌人只从 GET 返回的 common04 六种 SO 目录选择：horde_biscuit_a、horde_candy_b、horde_puddin_c、unbread_a/b/c。关卡级 targetHealth、targetRepairSpeed、targetRepairThreshold、kitchenHealth 和 min/maxOrderCount 也在完整 DTO 保存。保存上限 2 MiB；MCP `oc2_save_level_mode_config({config,confirm:true})` 会自动 GET 逐字段核对。

敌群交付门使用 `items[].servingStation.orderUIOffset/healthUIOffset` 两个 xyz 向量调整订单/血条 UI；金币门使用 `items[].lockable.unlockCost` 非负整数。保留门的原始 prefab/stub 类型，不能把普通交付口仅加两个 offset 字段当作 Horde 门。场景写回后另 GET 布局核对这些字段；模式配置保存不会替代场景写回。

## 切换已有场景

1. 读取详情、模式配置和场景布局，确认 LevelInfoSO 路径与场景对应。
2. 保存所有已打开场景与相关资产，激活目标场景，关闭其他伪预制体场景；目标需唯一有效的 Manager/FlowManager/Debugger，不能有 Missing Script 或额外的未支持控制器引用。
3. 如需转换已支持的普通 ServingStation wrapper 与 Horde Service_Window wrapper，先取得用户明确同意，切换时传 `convertHordeObjects:true` 进行原子双向转换；保留原 GO、位置、名称和回收绑定，外观与组件改变。**不要在切出前预删最后一个 Horde 窗**，保留该窗由切换事务转换。金币 gate 不转换，切出前须先删除并保存；正常 Horde 允许无金币 gate。未启用转换时仍须满足目标模式交付口守卫，进入 Horde 的最终场景至少有一个激活的有效交付窗且无普通口。
4. 切入 Dynamic 前配好 N−1 原生有效转场；Web partLevel.enabled 的关卡拒绝切入 Dynamic/Horde。已保存的 Web 分 P 不提供直接关闭入口，请新建独立的原生模式关卡。
5. 调用 `POST /api/level/mode`，body `{assetPath,gameMode,convertHordeObjects?}`，或 MCP `oc2_change_level_mode({assetPath,gameMode,convertHordeObjects?,confirm:true})`。该 flag 为精确布尔值、默认 false；仅用户明确同意后设 true，不因守卫报错自动开启。模式配置 POST 不切控制器。
6. 成功后 GET 模式与关卡详情；MCP 自动完成数值读回。再重新读取场景检查转换后物件类型、外观、原 GO/位置/名称和回收绑定；连接中断先 GET 核对，不能直接重复 POST。

编译/导入/导出/Play 切换期间拒绝迁移。后端负责原生事务与失败恢复，MCP 不绕过场景干净、唯一 Manager、Horde 门具、转场和分 P 守卫。

## 保存与导出

`mode_config~/mode_config.json` 保存 Web 阶段/波次 ID 等编排数据，不随 bundle；`mode_assets/r_<revision>/` 保存本关专属原生 SO 图并进入关卡 info bundle。保存、场景写回及导出需同步模式菜谱、食材、烹饪步骤、匹配表、敌人及 bundle 引用；原有订单顺序功能只适用于 Campaign，不能覆盖 Dynamic/Horde 的原生出单。

`all/deps` 依赖交付闭包包含可达 `common01_textures`（尤其 common01 依赖的拆分贴图）；基础 `common01` 本体仍由前置外供。依赖包 `OC2DIYLevelRuntimeWLoader/bundle_dependencies.txt` 必须包含实际交付的 common01_textures，并按依赖先于引用者的顺序加载。levels 只交付关卡，需另配对应依赖包。实际构建 manifest、zip 内容、清单和 Loader 日志要一致，不能因本地旧包存在就判定通过。

## 验收记录边界

本次 MCP 离线验证可执行：

```sh
cd layout-editor/mcp
npm run build
npm test
```

接入真实 Unity 后仍需逐项执行并记录实际证据：

1. 新建 Campaign/Dynamic/Horde，重开场景核对实际控制器、Manager 和 LevelInfo 绑定。
2. Dynamic 至少两阶段改菜单/时长并保存、GET、重开；原生转场数量/绑定有效且实际 Play 切换。
3. Horde 四个人数配置独立改波次/费用，重复敌人同秒生成得以保留；保存、GET、重开，Play 检查血量、修复、UI offset 与金币门。
4. 验证脏场景、多 Manager、未授权转换时不兼容交付口、转换后缺 Horde 激活窗、切出残留金币 gate、无效转场和 partLevel.enabled 均拒绝，且数据未被部分写入；获授权 true 时双向转换受支持 wrapper，最后一个 Horde 窗无需预删，且原 GO/位置/名称/回收绑定保留；无金币 gate 的正常 Horde 仍可保存。
5. all/deps 构建成功并核对 common01_textures 的 zip/清单/加载顺序；安装实际依赖包后分别测试游戏单机及主客机。

这里列的是待执行验收，不代表真实 Unity 构建、游戏单机或联机已经通过。stdio 的 verified 仅证明当次 REST 数值读回，不能替代这些证据。

2026-11-19 模板复制修复的离线验证入口为 `python3 layout-editor/scripts/check-level-template-copy.py`（编译完整生产 helper，读取真实三模板 YAML，在临时目录用文件系统替身验证复制/重映射/隔离/标签/清理）、`python3 layout-editor/scripts/check-level-create-transaction.py`（执行生产 CreateLevel 方法，对 setup、backup、目录、复制、store、场景、登记后失败注入）及 `python3 layout-editor/scripts/check-template-mode-initialization.py`（执行生产 store 的原生导入/建图方法，用真实 Dynamic/Horde YAML 检查默认值和两关/四人数隔离）。本次分别通过 8735、99、82 个断言；另通过 native-mode-validation 89、level-mode-change 43、mode-recipe-consumers 27、set-bundle-ownership 28 项。完整当前源码检查 `python3 layout-editor/scripts/check-current-host-backend.py` 通过，C#4/net35 重编 firstpass 429、Stub 61、宿主 2423 和完整独立后端 137 个文件，不引用旧宿主 DLL。本轮不运行真实 Unity 创建/导出，不把替身测试结果追加为下方历史 Unity 验收记录。

### 本次 Unity 接口验证记录

Bridge 已刷新，health 实际返回 schemaVersion=12，`GET /api/mcp/tools` 返回四个新增原生模式工具。独立 QA 集 `qa_native_modes_20261118` 及 Horde、Dynamic 两关均已创建；Horde 首次创建超时后未重发，后续列表确认其最终登记成功，日志记录 22/22 预览 child 初始化成功。

Horde GET 返回四份、每份两波的配置与六种敌人。将 1P 修门费用改为 123、首波改为同种敌人两次及时间 `[0,7]` 后，POST 返回 ok；GET 确认 1P 修改生效、2P 保持原值、重复敌人与生成时间一一保留。Dynamic 三阶段首段时长保存为 17 秒，另外两段仍为 15 秒；POST 曾在预览 reload 阶段超时，但后续 GET 确认数据已保存。模式配置保存现已调整为先返回结果，再仅对目标活动场景延迟重建预览。

真实 deps 导出返回 done、error 为空，文件 `LayoutEditorExports/OC2DIYLevelRuntimeWLoader_v4.0.0_20261118.zip` 共 16 项。直接读取该 zip 确认 `common01_textures` 存在，加载清单第一项为它、后续为 commonW2/commonW1 等，基础 common01 本体未交付，清单中的文件均存在于包内。

真实 QA Dynamic 场景已执行 Dynamic→Campaign→Dynamic 两次切换：接口均返回 ok，详情读取分别确认 Campaign、Dynamic。对没有原生站台的 QA Dynamic 请求转 Horde，后端明确拒绝，未自动新增道具。QA Horde 删除三扇可选金币门的布局写回返回 ok、warnings 为空；随后带 convertHordeObjects 的切出请求被脏场景守卫拒绝，未强制保存，因此原子交付窗转换尚未获得真实成功证据。

尚未完成：原子交付窗转换、Play、单机/主客机运行及前端视觉验收。全量 TypeScript 检查仍有既有错误；本次前端生产构建和完整当前源码 C#4/net35 编译通过。QA 资产为 Unity 工具生成，保留用于后续验收。
