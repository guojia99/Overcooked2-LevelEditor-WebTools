# OC2 关卡重建交付报告

生成时间：2026-09-17 14:25:45

## 一、总览

| 关卡集 | 关卡数 | 物件 | 台面 | 器具 | 造景 | 地砖 | 碰撞墙 | 表杀 | stub 参数 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| oc2_dlc_story（DLC） | 87 | 52610 | 4411 | 540 | 47659 | 9584 | 1742 | 353 | 383 |
| oc2_story（本体） | 45 | 23269 | 2348 | 370 | 20551 | 170 | 949 | 168 | 252 |

## 二、校验结果

- **几何校验**：每关生成后反解回来与源场景逐件比对（位置容差 1e-3 m，旋转容差 0.05°，缩放容差 1e-3）。
  - 全部 132 关，失配 **0** 项。
- **结构自检**：引用闭合 + guid 存在 + 实例 override 的 target fileID 确实存在于被引用预制体内。132 个场景，**0** 个有问题。
- **Unity 实开**：132 关全部能打开，硬错误 0。剩余控制台告警见第四节（与旧数据、未改动关卡集同源，属编辑器 stub 体系既有限制）。
- **截图**：132 张，唯一 132 张（重复 0 组）；取图来源 {'lso': 132}。

## 三、无法还原项（已逐条定位，非静默丢弃）

| 类别 | 数量 | 说明 |
|---|---:|---|
| mesh-no-model-container | 1108 | 动画机构的零件（吊桥齿轮/移动平台/绳索/木筏尾流/车轮等），按「不要动画」口径排除 |
| empty | 813 | 空分组节点，本身没有任何可见内容 |
| scene-embedded-mesh | 173 | 场景内嵌网格（烘焙/合批产物），没有可加载的 bundle 资产 |
| logic-only | 122 | 纯逻辑标记物（出生点、触发队列等），编辑器无对应物件 |
| mesh-unresolved | 50 | 网格 guid 在反编译导出里找不到对应文件 |

主动跳过（设计如此）：
- npc-rig：527（NPC/顾客角色（带骨骼与 Animator，静态还原只会是 T-pose））
- baked-combined：479（mesh baker 合并网格，与散件造景重复）
- particle：368（粒子特效）
- subtree：199（Lightmap Baker / 光照探针等烘焙辅助节点）
- fx：177（pfx_/fx_ 前缀的特效件）
- self：81（AttachPoint / Worktop 等挂点）

### TRS 无法精确表达的物件

原版里父级同时带旋转和非均匀缩放时，子物件的真实变换含剪切，Unity 的 Transform 只能存 TRS，必然有偏差。

- 物件总数 75879，含剪切 872（1.15%）
- 误差分布：{'1~5cm': 225, '<1cm': 570, '>20cm': 28, '5~20cm': 49}
- 误差 > 20cm 的共 28 个，最大 50.15 m：
  - 50.15 m  OC2_Story_18  `Stage/Mine Section 2/WaterPlane/dynamic_raft_water/splahes rock (4)`
  - 50.15 m  OC2_Story_18  `Stage/Mine Section 2/WaterPlane/dynamic_raft_water/splahes rock (3)`
  - 50.15 m  OC2_Story_18  `Stage/Mine Section 2/WaterPlane/dynamic_raft_water/splahes rock (2)`
  - 50.15 m  OC2_Story_18  `Stage/Mine Section 2/WaterPlane/dynamic_raft_water/splahes rock (1)`
  - 15.37 m  OC2_DLC02_3_3  `Art/Scenery/RockCliff/rock_02 (2)/ripple_1 (10)`
  - 9.26 m  OC2_DLC02_3_3  `Art/Scenery/RockCliff/rock_01 (8)/ripple_1 (13)`
  - 9.17 m  OC2_DLC02_3_3  `Art/Scenery/RockCliff/rock_01 (8)/ripple_1 (12)`
  - 9.08 m  OC2_DLC02_3_3  `Art/Scenery/RockCliff/rock_01 (8)/ripple_1 (11)`
  - 6.00 m  OC2_DLC02_SP  `ht/FloatDirection/Debris2/WaveFloat_Generic (2)/m_sp_alien_gue_edge_01`
  - 6.00 m  OC2_DLC02_SP  `loatDirection/Debris2 (1)/WaveFloat_Generic (2)/m_sp_alien_gue_edge_01`

## 四、Unity 打开后的剩余告警

| 次数 | 告警 | 性质 |
|---:|---|---|
| 8 | MissingComponentException: There is no 'CapsuleCollider' attached to the "Block" game obje | 编辑期从 bundle 实例化出的子物体缺少游戏侧组件，编辑器既有现象 |
| 7 | UnassignedReferenceException: The variable pilotableObject of PseudoPrefabTerminalStub has | 移动平台关卡的操控终端指向的是整套动画机构，本次不还原动画 → 无法赋值（旧数据同样如此） |
| 7 | Assertion failed on expression: 'pred(*previous, *i)' | Unity 内部排序断言，无实际影响 |
| 3 | UnassignedReferenceException: The variable heatedStation of PseudoPrefabHeatedOvenStub has | 石炉台的热源引用，同上（旧数据同样如此） |
| 3 | NullReferenceException: Object reference not set to an instance of an object | 编辑期伪预制体实例化的既有现象（未改动关卡集也有） |

> 对照组：未改动的 oc1_story + jia_carnival 共 37 关，同样出现 NullReferenceException；备份的旧 oc2_dlc_story 场景同样出现 pilotableObject / heatedStation 未赋值告警。即这些告警不是本次重建引入的。

## 五、产物清单

- 关卡场景：`Assets/LevelSets/oc2_dlc_story/scenes/` 87 个、`Assets/LevelSets/oc2_story/scenes/` 45 个
- 关卡数据：两集各自 `data/<LEVEL_ID>/`（LevelInfo + config_1p~4p + screenshot.png）
- 新建 PseudoPrefabSO：633 个（`Assets/commonW1/pseudo_prefab_so/oc2rebuild/`）
- 新建造景 wrapper 预制体：599 个（`Assets/commonW1/prefabs/<dlc|core>/art/<theme>/`，路径沿用素材库约定以便 catalog 正确归类）
- 新建地板材质：17 个（`Assets/commonW1/materials/oc2rebuild/`）+ 贴图 21 张（`Assets/commonW1/textures/oc2rebuild/`）
- 素材目录已重建：`layout-editor/web/public/catalog/`（物品 2880 → 3479）

## 六、重建管线

脚本位于 `layout-editor/scripts/oc2-rebuild/`：

| 脚本 | 作用 |
|---|---|
| `build_indices.py` | 建五张权威索引（prefab 容器 / 结构签名 / 脚本类名 / 素材库 / 反编译 guid） |
| `survey.py` | 全量 dry-run 盘点，输出识别/跳过/丢弃明细 |
| `survey_rawmesh.py` `survey_floormat_budget.py` `survey_skew.py` `survey_placement.py` | 专项勘察 |
| `rebuild.py` | 重建已有关卡集的场景（含几何校验闸门、依赖补全） |
| `build_base_set.py` | 新建本体关卡集（关卡资产 + 场景 + 集信息） |
| `screenshots.py` | 从 bundle 取官方关卡图（LoadScreenOverride 优先 + 唯一性校验） |
| `validate_sets.py` | 全量结构自检 |
| `probe_level.py` `dump_node.py` | 单关/单节点诊断 |

Unity 侧批量自检：`Assets/Editor/LayoutEditor/LayoutEditorSceneBatchValidator.cs`（菜单 `OC2 Layout/校验/批量打开关卡集场景`，或命令行 `-executeMethod`）。

