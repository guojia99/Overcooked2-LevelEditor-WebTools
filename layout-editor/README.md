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
- **层（Tab）**：顶栏「📦 物品层」/「🗺️ 地板·背景层」切换；非活动层以半透明鬼影显示、不可点击  
  - 地板层：编辑场景内 `Floor` 平面（拖动移动、拖角点缩放、右键改尺寸/材质、`Del` 删除、`+ 新增地板` 放置）；显示当前尺寸（格/米）；相邻地板接缝画淡虚线；空洞/水域按死亡类型着色（水=蓝、坠落=暗）；冰面标记「❄」
  - 地板材质来自 `LevelSets/<set>/materials/`（`mat_*floor*` 等），改尺寸时自动按 `*_<W>x<H>` 匹配材质
  - 地板/背景类 prefab（ice_floor、alien_floor、Sky…）拖入画布即归入地板层
- **目录**：左侧按 **核心**（桌台 / 器具 / 机关 / 厨师）与 **装饰**（含 NPC）分组；中文名 + 英文 ID  
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

---

## 维护者：更新目录或前端（需要 Node）

仅在修改 `layout-editor/web` 源码或需要刷新 prefab 目录时，在**有 Node.js 的机器**上执行：

```bash
# 刷新 catalog（会同步到 web/public 与 web/dist）
node layout-editor/scripts/build-catalog.mjs

# 重新打包前端
cd layout-editor/web
npm install
npm run build
```

将 `layout-editor/web/dist/` 一并提交，供无 npm 环境使用。

### API 速查

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/health` | `ok`、`port`、`static`（dist 是否存在） |
| GET | `/api/level-sets` | 关卡场景列表 |
| GET | `/api/scene/layout?assetPath=...` | 导出布局（items + floors + walkable + deathInfo） |
| POST | `/api/scene/layout?snap=1.2` | 写回布局（items + floors） |
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
  scripts/build-catalog.mjs
  web/dist/          # 预构建静态站（提交到 Git）
  web/src/           # 源码
Assets/Editor/LayoutEditor/
```
