# Web 关卡俯视图编排器

在浏览器中俯视图编排 `Design` / `Art` 下的 prefab 占位，通过 Unity Editor HTTP 桥接写回 `.unity` 场景。

## 日常使用（无需 Node / npm）

仓库已包含预构建静态前端：`layout-editor/web/dist/`（随 Git 提交）。**Windows 上只需 Unity + 浏览器。**

1. 用 Unity **2017.4.8f1** 打开工程  
2. **Tools → Layout Editor → Open Bridge**（或 **Open Web UI**）  
3. 点击 **启动服务**  
4. 点击 **在浏览器中打开编排页**，或访问 **http://127.0.0.1:8765/**  
5. 写回时会自动 **Prepare For Building** 并 **Reload Pseudo Assets**（等同 Tools 菜单），再在 Unity **Ctrl+S** 保存场景  
6. 顶栏 **菜谱…** 可编辑当前关卡 `LevelInfoSO.recipes`；**右键** 食材箱 / 食材生成器可配置参数  

同一地址同时提供：

- 静态页面、`/catalog.json`  
- REST API：`/api/health`、`/api/level-sets`、`/api/scene/layout`、`/api/grid`  

### 操作说明

- **场景**：顶栏下拉选择关卡场景并加载  
- **层（Tab）**：顶栏「📦 物品层」/「🎀 装饰层」/「🗺️ 地板·背景层」切换；非活动层以半透明鬼影显示、不可点击  
  - 物品层：只编辑核心玩法物品（桌台/厨具/机关/厨师），调色板只显示核心分组  
  - 装饰层：只编辑装饰物（art/NPC 道具），调色板只显示装饰分组；装饰物不再与核心道具混杂  
  - 地板层：编辑场景内 `Floor` 平面（拖动移动、拖角点缩放、右键改尺寸/材质、`Del` 删除、`+ 新增地板` 放置）；显示当前尺寸（格/米）；相邻地板接缝画淡虚线；空洞/水域按死亡类型着色（水=蓝、坠落=暗）；冰面标记「❄」
  - 地板材质来自 `LevelSets/<set>/materials/`（`mat_*floor*` 等），改尺寸时自动按 `*_<W>x<H>` 匹配材质
  - 地板/背景类 prefab（ice_floor、alien_floor、Sky…）拖入画布即归入地板层
- **目录**：左侧按 **核心**（桌台 / 器具 / 机关 / 厨师）与 **装饰**（含 NPC）分组；中文名 + 英文 ID。桌台按工作流排序（备餐→烹饪→出餐/回收），厨具按**配套设备**聚类（锅具↔灶台、炸篮↔煎炸台、搅拌碗↔搅拌台、搅拌杯↔搅拌机、烤签↔烧烤架、烤叉/风箱/煎烤盘↔篝火、盘杯↔桌台），条目下方标注「配套XX · 高度」
- **菜谱**：顶栏 **菜谱…** 按**菜谱类型**分组选择（汉堡/卷饼/披萨/意面/寿司/沙拉/汤/炸物/蒸菜/蛋糕/松饼/冰沙/烤串/早餐/棉花糖饼干/面糊），DLC/自定义来源以徽标标注  
- **叠放**：盘、杯、搅拌碗等拖到桌台格上会自动对齐桌心（`Design/Utensils`，本地 Y≈1）；锅/蒸笼/煎锅对齐灶台（Y≈0.6）；画布上叠放物为半透明小方块，桌台/灶台等为实色块（按分类配色）  
- **半格 (0.6)**：默认开启，用于两格宽桌台对齐  
- **旋转**：选中后 **R**；**删除**：**Del**  
- **网格**：勾选「显示网格」；蓝框为 `QuadGridManager` 范围  

## 与 LevelSets 工作流

1. 编排 `guojia` / `test` / `oc1_story` 等场景  
2. Unity **Play** 验证  
3. **Toggle Prepare For Building** → 保存场景 → **Build AssetBundles**  

不修改 `LevelInfoSO`、AssetBundle 标记或 `common*` 资源。

## 关卡管理（独立页面）

顶栏 **关卡管理…** 进入独立的关卡管理页（URL `/#/manage`），与俯视图编排页相互独立，可随时通过页内按钮互跳。基于 `LevelSetInfoSO / LevelInfoSO / LevelConfigSetupPerPlayerCountSO` 这套 SO 管理关卡集与关卡：

- **关卡集**：列表查看 / 新建关卡集（自动创建 `LevelSets/<set>/data`、`scenes/` 与 `LevelSetInfo.asset`）/ 编辑英文名·中文名·作者·版本（改 version 自动重算 uid）。**关卡集不可删除**。
- **关卡**：在每个关卡集下列出，可**新建关卡**（自动生成 `config_1p~4p`（复制模板默认值）、`LevelInfo_<id>.asset`，并复制 `Template/s_template` 到 `scenes/s_<id>.unity`、绑定 `levelInfo`），**编辑基础信息**（表单提交，含 sceneName、dependencies、disableDynamicParenting、debugRecipeCount 等），以及**删除关卡**（连同其场景、配置与关卡目录内自定义资源，二次确认）。
- **人数配置 (1P/2P/3P/4P)**：弹窗内 4 个 Tab 编辑订单超时/间隔、关卡时长、各星分数等，**统一一次性提交**写入 4 份 `LevelConfigSetupPerPlayerCountSO`。
- **音频配置**：编辑场景 `PseudoPrefabManagerStub` 上的 BGM(`InLevelMusicSO`)、氛围音(`InLevelAmbiences`)、音效集(`AudioDirectorySOs`)、死亡特效(`OnDeathEffectSO`)；保存时自动打开并保存该场景。截图 `screenshot` 暂只读。
- **菜谱**：复用编排页的菜谱选择写入 `LevelInfoSO.recipes`。
- **Reload**：仅在表单提交时触发（刷新页面数据 + Unity Reload Pseudo Assets），编辑过程中不实时 reload。

> 说明：音频字段位于场景的 `PseudoPrefabManagerStub` 组件（非 LevelInfoSO）。许多 BGM 所在 bundle 不在默认加载列表，需在关卡基础信息的 dependencies 额外添加（木筏主题 BGM 需 `bundle11`）。

## 菜谱清单列表（独立页面）

顶栏 **菜谱清单列表…** 进入独立的只读页面 **`/recipes`**（Vite 多入口构建，Unity 桥按 clean URL 映射到 `dist/recipes.html`），陈列全部菜谱并按菜谱类型（汉堡/卷饼/披萨/…）分组，卡片采用菜谱书 UI（成品背景 + 食材分组背景 + 锅具图标，背景图取自 `sprite-dump/` 的 `UI_DLC07_Recipe_Background_Main_01.png` 与 `Recipe_Background_2.png`）：

- **筛选**：搜索（菜名/英文/ID/食材）、类型 chips、来源（基础/自定义/DLC2/DLC5）、「含半成品」开关（默认隐藏 score≤0 的半成品）。
- **食材分组**：每个菜谱按烹饪步骤分组成组展示，有步骤的组底部居中显示锅具图标（`icons/catalog/<utensil>.png`）与步骤名（`cooking-steps.json`）；无锅具的食材（汉堡胚、生菜等）合并为生食材组。分组算法见 `recipes.json` 新增字段 `cookingGroups`（`LayoutEditorRecipeKnowledge.ComputeCookingGroups` 与 `build-catalog.mjs` 的 `computeCookingGroups` 镜像实现）：煎锅仅覆盖肉排类中间产物（面包胚保持生），其余烹饪步骤整锅入组；面糊→煎锅等两段式烹饪会在组尾追加一次最终锅具标记。自定义菜谱若为**组装型（Composite）且无整体烹饪步骤**，则按其直接组成（子菜谱）分组成组——子菜谱以自身烹饪步骤成组并展开叶食材，普通食材归生组（`compositionIds` 字段 + `deriveCompositionGroups` 前端镜像）。旧数据缺字段时前端自动回退推导（`web/src/recipeGroups.ts`）。
- **数据来源**：桥在线走 `/api/recipes`，离线回退静态 `recipes.json`。

## 自定义菜谱管理（独立页面）

顶栏 **自定义菜谱…** 进入 **`/#/custom-recipes`** 页：先选关卡集，再管理该关卡集 `custom_recipes/` 目录下的自定义菜谱（CustomRecipeSO）：

- **统一模型**：中间产物（score=0，如煎鸡蛋）也是**完整菜谱**——图标、3D 模型、装盘容器全部可配，仅作为可被引用的工序而不会直接上桌。列表：顶部搜索（菜名/ID/食材）+ 成品/中间产物筛选 + 分类侧栏；每张菜谱以「菜谱清单列表」卡片展示组装效果，卡片底部信息条显示分类/装盘容器/UID/组成项数，操作按钮（👁 3D 预览 / 编辑 / 删除）置于底部；旧桥接数据自动用组成 id 反查食材名兜底。
- **编辑/新建表单**：
  - 顶部**组装效果实时预览**（同卡片样式），随组成选择即时刷新；
  - **组成**：点击「＋ 添加食材 / 中间产物」弹出选择器（搜索 + 全部/食材/中间产物筛选）——同一项可**多次选用**（卡片与已选列表均有 −/＋ 数量调整，如双肉排汉堡）；中间产物含本关卡集自定义菜谱与官方中间产物（半成品徽标 + 来源标注）；
  - 「**+ 新建中间产物**」直接进入完整新建表单（预填分数 0），保存后回到列表即可被引用；
  - 烹饪设置（步骤/程度/搅拌）与**装盘容器**（盘子/杯子，运行时映射 `PlatingStepData`）配置；
  - 模型区：复用已有模型 / 上传图标 / **上传 3D 模型（FBX/OBJ 可同时选择 PNG 贴图一并上传，主模型统一命名 `<recipeName>.fbx|.obj`，贴图保留原名写入 models 目录供 Unity 导入关联）**；
  - **3D 在线预览**：列表卡片「👁 预览」或编辑页「预览当前模型」按钮，浏览器内用 three.js 渲染 FBX（自动加载内嵌贴图）/OBJ，支持旋转/平移/缩放（模型文件经 `/api/custom-recipes/model-files` 目录式端点服务）。
- 锅具自动分配（主编排页「🧩 自动中间产物」）除内置硬编码表外，对用户自建的中间产物按「叶食材 ⊂ 菜谱叶食材 + 步骤匹配」泛化兜底（`main.ts computeIntermediatesForUtensils`）。

---

## 维护者：更新目录或前端（需要 Node）

仅在修改 `layout-editor/web` 源码或需要刷新资源目录时，在**有 Node.js 的机器**上执行：

```bash
# 刷新全部资源目录（会同步到 web/public 与 web/dist）
node layout-editor/scripts/build-catalog.mjs

# 重新打包前端
cd layout-editor/web
npm install
npm run build
```

### 提取食材 / 菜谱图标（需要 Python）

图标（食材 `*_Icon`、菜谱 `ui_*` 成品图）存在游戏 AssetBundle 内，用一次性 Python 脚本批量解包：

```bash
pip install UnityPy Pillow
python3 layout-editor/scripts/extract-icons.py            # 解包到 web/public/icons/{ingredients,recipes}/
python3 layout-editor/scripts/extract-icons.py --check    # 只统计覆盖率，不写文件
```

- **食材**：按 `<id>` 命名，通过 `<base>_Icon`（大小写无关）自动匹配；不规则的（如 `SushiRiceSO→Rice_Icon`、`DLC05_Wood→Firewood_Icon`）写在 `scripts/data/ingredient-icons.json`。
- **菜谱**：菜谱资源不直接引用成品图，故用人工对照表 `scripts/data/recipe-icons.json`（`id → ui_*` 精灵名，`null`=无合适图）。
- **自定义 / 纠错**：在 `scripts/data/icon-overrides.json` 里加 `{ "ingredients": {...}, "recipes": {...} }`（精灵名或 `null`），优先级最高。
- 游戏更新后重跑脚本；末尾会列出未匹配的 id，提示去对照表里补。
- 跑完后再执行 `node layout-editor/scripts/build-catalog.mjs`，会在 `ingredients.json` / `recipes.json` 上盖 `icon: true` 标记，前端挑选器随即显示缩略图。

将 `layout-editor/web/dist/` 一并提交，供无 npm 环境使用。

### 资源目录（统一脚本输出）

`build-catalog.mjs` 一次生成以下 JSON（前端在 Unity 桥离线时自动 fallback 到对应静态文件；桥在线时由 C# 动态扫描输出相同 schema）：

| 文件 | 内容 | 来源 |
|------|------|------|
| `catalog.json` | prefab 布局目录 | `common01/02/prefabs/**` |
| `ingredients.json` | 食材（含 dlc02/dlc05，按 `group` 分组） | `common01/02/food/Ingredients` |
| `recipes.json` | 菜谱（原始/自定义/DLC，含食材组成与烹饪步骤） | `common01/02/food/Recipes`、`CustomRecipes/**` |
| `cooking-steps.json` | 烹饪步骤 + 装盘步骤 | `common01/02/food/{CookingSteps,PlatingSteps}` |
| `audio-catalog.json` | music / audioDirectories / deathEffects | `common01/02/pseudo_prefab_so` |
| `floor-materials.json` | 地板材质（按关卡集分组） | `LevelSets/*/materials`、`common01/02/materials` |

菜谱组成数据来源：`layout-editor/scripts/data/recipe-knowledge.json`（Unity 端 `LayoutEditorRecipeKnowledge` 共用同一文件）。CustomRecipeSO 的组成由脚本直接解析 `compositionSOs` guid 引用；原始/DLC 菜谱查知识表；**组成信息不全的菜谱会被跳过**（脚本输出跳过清单）。新增 DLC 菜谱时在 `recipes[]` 中补一条 `{id, step, ingredients}` 即可；score-0 的运行时变体（Optional*/model_*）列入 `skip[]`。

中英文显示名来源：`layout-editor/scripts/data/names-dictionary.json`（Unity 端 `LayoutEditorManualLookup` 共用同一文件），查找顺序为 **字典 JSON → 使用手册.md → ID 回退**。覆盖全部资源：核心桌台/厨具/机关/厨师、装饰/NPC/地板 prefab、食材/菜谱/烹饪步骤、音频与音效集（1100+ 条）。**后续新增资源时只需维护该文件**：在 `names[]` 中补 `{id, zh, en}` 后重跑 build-catalog 即可；也可先运行 `node layout-editor/scripts/scaffold-dictionary.mjs` 为新 ID 生成草稿翻译（词元规则），再在字典中人工修订。

### 版本与升级提醒

每个生成的 JSON 与 `recipe-knowledge.json` / `names-dictionary.json` 都带 `schemaVersion`（当前 v2）。`/api/health` 会返回桥接端的 `schemaVersion`、`knowledgeLoaded`、`dictionaryLoaded`；前端发现桥接端版本落后或共享数据文件缺失时，会在状态栏提示「桥接端资源数据过旧，请升级」。两端 schema 变更时需同步提高 `build-catalog.mjs` 的 `SCHEMA_VERSION` 与 `LayoutEditorRecipeKnowledge.BridgeSchemaVersion`。

### API 速查

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/health` | `ok`、`port`、`static`（dist 是否存在） |
| GET | `/api/level-sets` | 关卡场景列表 |
| GET | `/api/scene/layout?assetPath=...` | 导出布局（items + floors + walkable + deathInfo） |
| POST | `/api/scene/layout?snap=0.01` | 写回布局（items + floors）；snap 为所选摆放精度 |
| GET | `/api/grid` | 网格参数 |
| GET | `/api/catalog/floor-materials?levelSet=...` | 地板材质列表 |

关卡管理 API（`/#/manage` 页面使用）：`GET /api/sets`、`GET /api/sets/<set>/levels`、`GET /api/level?assetPath=`、`GET /api/catalog/{music,audio-directories,ambiences,death-effects}`、`POST /api/{set/create,set/info,level/create,level/info,level/config,level/audio,level/delete,reload}`。

### 可选：Vite 开发模式

```bash
cd layout-editor/web && npm run dev
```

浏览器 **http://localhost:5173**（仅开发时；`/api` 代理到 `8765`）。

## 目录结构

```
layout-editor/
  scripts/build-catalog.mjs        # 统一资源目录生成（单入口）
  scripts/scaffold-dictionary.mjs  # 新 ID 草稿翻译生成（合并进字典）
  scripts/data/recipe-knowledge.json  # 菜谱组成知识表（Node 与 Unity C# 共用）
  scripts/data/names-dictionary.json  # 全资源中英文名称字典（Node 与 Unity C# 共用）
  web/dist/          # 预构建静态站（提交到 Git）
  web/src/           # 源码
Assets/Editor/LayoutEditor/
```
