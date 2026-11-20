# 原生序列与重生边界接口契约

供 MCP 代理同步。沿用现有端点，无新增 HTTP 路由。C#4 DTO 与 Web 类型内也有字段语义注释。

## GET /api/level?assetPath=...

- `enableRespawnBounds: boolean`：LevelInfoSO 原值，不按模式重置。
- `configs` 维持 1P、2P、3P、4P 顺序；每项新增 `scriptedRecipes: string[]` 和只读 `scriptedRecipeErrors: string[]`。
- `scriptedRecipes` 保留每个引用的位置、重复和顺序。缺失/无资产 GUID 的位置返回 `""`；错误类型但仍有 GUID 的位置保留 GUID；同时返回带 1 起始位置的诊断。缺人数配置返回 `exists=false`、两个空数组。
- Dynamic/Horde 同样安全回显休眠原生序列，不删改其内容。

## POST /api/level/info

在原有完整基础信息请求中新增可选字段：

```json
{"updateEnableRespawnBounds": true, "enableRespawnBounds": false}
```

只有 `updateEnableRespawnBounds=true` 才写入布尔值。标志缺省或 false 时保留原值；标志 true 而值缺省按 JsonUtility 的 false 处理。MCP 应要求标志为 true 时显式提供布尔值。现有其他标量的更新语义不变，此端点不是通用 PATCH。

## POST /api/level/config

- `assetPath: string`：现有关卡 LevelInfoSO 路径。
- `config_1p`…`config_4p`：缺省或 null 表示该人数完全不修改。
- 已提供的人数对象仍须携带其原有完整时长/分数字段；这些旧标量继续使用原语义，不是逐字段 PATCH。
- 人数对象新增 `scriptedRecipes?: string[] | null`：缺省/null 保留；`[]` 显式清空；非空为菜谱资产的 32 位十六进制 GUID 数组，保留重复与顺序。
- `scriptedRecipeErrors` 为只读，POST 不消费。
- 仅 Campaign 接受非 null 的 `scriptedRecipes`（其他模式连 `[]` 也拒绝，应省略或 null）。
- 所有人数的存在性、配置所有权、引用类型/可解析性和有效最终序列冲突，均在任何人数写入前预校验。任何失败整批拒绝，不先改 1P 再报 4P 错误。
- 序列变更调用完整 `LayoutEditorLevelModeStore.SyncRecipeData`，包括食材、steps、special Optionals、matchlist、依赖与加载场景锅具；普通 `LevelInfoSO.recipes` 不扩写。消费者通过统一 `CollectAllRecipes` 获取原生序列引用。
- `/config` 的预校验后写入由 `LayoutEditorConfigTransaction` 备份四人数、LevelInfo、本关数据目录、set 级 UID 配置及命名 sidecar、已加载场景消费者。同步或保存抛错时恢复请求前的磁盘字节和内存值、移除新派生文件。恢复失败会明确返回“恢复未完成”；Unity 2017 回退后的场景可能保留 dirty 标志，不为清标志而保存用户场景。reload 在提交后失败则明确报告“配置已保存，但场景 reload 失败”，调用方必须读回。

## Web OrderSequence 互斥

沿用 `GET /api/level/order-sequence`、`POST /api/level/order-sequence-save`、`POST /api/level/order-sequence-clear`。

- 保存 `enabled=true` 仅限 Campaign，且有效最终 1P–4P 原生数组必须全部为空。任何非空数组（包括坏引用占位）都算冲突。
- 原生序列 POST 反向检查已保存 Web 配置，同样拒绝双序列状态；不会隐式清除任意一方。
- `enabled=false` 草稿可保存，保留原生序列；clear 仅显式删除 Web JSON。空 JSON 保存拒绝，删除须走 clear。
- 启用的 Web 菜谱必须仍可解析，并属于 `CollectAllRecipes` 的本关有效菜单。
- 写回、导出与烘焙共享 `LayoutEditorOrderSequencePolicy`。冲突/损坏/坏引用停止操作；UID 解析失败也停止，不跳项生成截断序列。缺失或显式禁用的 Web 配置才清理其烘焙载体。
- JSON 读取失败不再视为文件缺失，避免静默清载体。GET 原文可用于诊断/显式覆盖修复。
- 写回在 `PrepareSceneForApply` 及删改场景前执行 `OrderSequenceBake.Prepare`，检查 Campaign 原生序列和 Web 序列的实际 UID 可解析性。末尾烘焙消费本次请求的已解析计划，不再重新查 GUID/bundle 或读取 JSON，避免晚期 UID 失败留下已修改的场景。此预检不代表整个 SceneLayoutApplier 已具备通用事务回滚。

## Web 保存与验证

关卡配置新增重生边界勾选项及独立「原生序列」页。四人数有独立草稿，支持重复追加、上下重排、删除、清空；只提交改动过的人数，提交前读取最新基础配置。保存后 GET 详情逐项验证长度、GUID 顺序与重复；失败保留草稿。未保存序列阻止模式切换。

未保存序列草稿和正在保存状态也阻止分数保存、取消、Escape、遮罩、程序化关闭及 close-all。用户可在原生序列页显式放弃四人数草稿后关闭。分数按钮标为「保存分数与基础配置」，先 `/config` 后 `/info`，二者是独立提交，不宣称跨端点原子性。`/config` 拒绝时不发 `/info`；任何失败保留表单、GET 详情显示实际值与可能的部分成功；读回失败明确要求核实，不自动重放。

Web OrderSequence API 保存前做反向冲突预检，成功后 GET 核对 JSON。重生边界修改成功后 GET 核对布尔值。MCP 应复制相同读回流程，不能把 POST 成功当成数据已读回验证。

## MCP 保存工具（stdio / Unity HTTP）

- `oc2_save_level_respawn_bounds({assetPath, enableRespawnBounds: boolean, confirm: true})`：先读上述详情，合并完整基础信息标量、网格半宽及 dependencies，调用 `/api/level/info` 并显式设置 `updateEnableRespawnBounds=true`，再读取详情核对布尔值。三种模式均可保存。
- `oc2_save_native_scripted_recipes({assetPath, playerCount: 1|2|3|4, scriptedRecipes: string[], confirm: true})`：仅 Campaign；先读详情，仅合并指定人数的完整旧标量到 `config_Np` 并提交 `/api/level/config`，其他人数不发送。不发送只读诊断；保留 GUID 顺序和重复，`[]` 清空。此最小保存工具要求显式数组，缺省/null 拒绝；底层 REST 的缺省/null 保留语义不变。Dynamic/Horde 连 `[]` 也拒绝，不回传休眠数组。
- 两端均拒绝未知参数、错误类型、非布尔确认、非 1–4 整数人数和无效 GUID。内置 RPC 在 JsonUtility 反序列化前检查原始参数；不自动修复坏位置、不去重、不切换模式、不清除 Web 序列，引用/所有权及最终双序列冲突由现有 Admin 统一预校验。
- 保存只执行一次。成功后独立读回，序列逐项核对长度、GUID 身份（大小写不敏感）、顺序、重复，并要求该人数 `scriptedRecipeErrors=[]`。结果 `{saved,readback,verified:true}` 不代表 Unity Play/导出/真机验收。
- 错误含 `writeOutcome`：本地参数失败为 `INVALID_ARGUMENT/not_started`，保存前 GET 失败为 `BRIDGE_ERROR/not_started`，写结果未确认为 `WRITE_UNCONFIRMED/unknown`，已确认但读回失败为 `READBACK_FAILED/acknowledged`。全部写错误 `retryable:false`；断线后先读详情及历史，不自动重放 POST。stdio 每次 GET 30 秒、POST 60 秒；整次建议等待 180 秒，无任务 ID 或写状态轮询。HTTP MCP 直接执行相同服务并独立读取详情。

详见 [MCP 原生模式接口](mcp/10-原生模式接口.md)。

## 验证

```sh
python3 layout-editor/scripts/check-native-scripted-recipes.py
node --test layout-editor/scripts/check-native-scripted-recipes.mjs
python3 layout-editor/scripts/check-current-host-backend.py
cd layout-editor/mcp && npm test
```

前两项执行生产方法/模块并使用 Unity API、DOM、HTTP 替身；不替代 Unity JsonUtility 真序列化、场景写回、导出与 Play 验收。未指定目标关卡时不自动修改用户场景。
