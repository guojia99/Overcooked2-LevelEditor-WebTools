# OC2LevelRuntimeLoader

关卡运行时程序集加载器（BepInEx 插件）。随关卡集分发的 C# 代码（如随机食材箱
`CustomStub.RandomCrate`，编译为关卡集专属程序集 `Stub_<set>.dll`）由本插件在
游戏启动后加载，使关卡场景里的脚本引用得以解析——**代码随关卡包分发，不进
Assembly-CSharp / common 包**。

## 机制

1. 游戏启动后首帧，扫描 `BepInEx/plugins/OC2DIYLevel/levels/<set>/runtime`
   （关卡集 zip 里的普通 bundle，与 `info_<set>` / `s_*` 同层）；
2. `AssetBundle.LoadFromFile` → `LoadAllAssets<TextAsset>()`，取名为
   `*.dll.bytes` 的资产 → `Assembly.Load(bytes)`（按程序集名去重）；
3. 带 `AppDomain.AssemblyResolve` 兜底处理引用顺序问题。

场景组件随场景加载自行激活，无需任何额外入口。一次性安装：本插件 + 含
`question_mark/` 图标库的新版 commonW1 bundle 都就位后，所有含随机食材箱
的关卡即可游玩。

## 打包（mac 本机一键构建）

```sh
./build.sh
# 等价于：dotnet build OC2LevelRuntimeLoader/OC2LevelRuntimeLoader.csproj -c Release
```

产物：`OC2LevelRuntimeLoader/bin/Release/OC2LevelRuntimeLoader.dll`（net35）。

引用来源（csproj 自动回退，均可用 `-p:` 覆盖）：

| 引用 | mac 回退路径 | Windows |
|---|---|---|
| `BepInEx.dll`（5.4.22） | `~/Downloads/[前置]BepInEx/BepInEx/core` | `-p:GameDir=...` → `<GameDir>/BepInEx/core` |
| `0Harmony.dll`（HarmonyX，仅编译期） | `deps/BepInEx/0Harmony.dll`（= `Assets/Plugins/0Harmony.dll`，BepInEx core 204KB 版） | `<GameDir>/BepInEx/core` |
| `UnityEngine.dll`（老式整包） | Unity 2017.4.8f1 编辑器安装目录 `Managed/` | `<GameDir>/Overcooked2_Data/Managed` |

> 注意：BepInEx 5.4.22 的 `BaseUnityPlugin` 编译自老式整包 `UnityEngine.dll`，
> 必须引用**同名程序集**做类型统一（勿改成 CoreModule 等模块 DLL，会 CS0012）。

## 打包（Windows）

```bat
dotnet build -c Release -p:GameDir="D:\Games\Overcooked! 2"
```

## 安装与分发（位置务必放对）

本插件是 **OC2DIYLevel 的配套能力扩展**（为其加载关卡内额外 C# 代码）。

**web 导出的关卡集 zip（2026-09-07 起）已一步携带本插件与 commonW* 依赖包**：zip 顶层为
`OC2LevelRuntimeLoader.dll` + `commonW1/commonW2/...` + `levels/<set>/`，将整个 zip **解压到
`Overcooked! 2/BepInEx/plugins/OC2DIYLevel/`** 即完成全部安装（`runtime` 与
`info_<set>`/`s_*` 同层；不要把 `.meta`/`.manifest` 拷进去）。

zip 内的 loader DLL 由研发手动维护：编辑器仓库 `layout-editor/web/public/OC2LevelRuntimeLoader.dll`，
更新流程 = 改 Loader.cs → `./BepInExPlugins/build.sh` → 拷贝 `bin/Release/OC2LevelRuntimeLoader.dll`
覆盖 public 下的文件（导出日志会打该文件时间戳供追溯）。

| 文件 | 游戏内位置 | 说明 |
|---|---|---|
| `OC2LevelRuntimeLoader.dll` | `Overcooked! 2/BepInEx/plugins/OC2DIYLevel/OC2LevelRuntimeLoader.dll` | 与 `OC2DIYLevel.dll`、`LevelEditorStub.dll` 同层（BepInEx 递归扫描 plugins/，子目录插件正常加载） |
| 关卡 bundle（`levels/<set>/`，按需含 `runtime`） | `Overcooked! 2/BepInEx/plugins/OC2DIYLevel/levels/<set>/` | `runtime` 必须与 `info_<set>`、`s_*` 同层 |
| `commonW*` bundle | `Overcooked! 2/BepInEx/plugins/OC2DIYLevel/commonW1`、`commonW2`、`commonW3`… | Loader 自动读取自身目录下所有严格命名为 `commonW` + 数字的 bundle，按数字顺序加载；与 `common`/`common01`/`common02` 同级，**不放 StreamingAssets** |

### 真机不生效的排查顺序（症状：随机箱只出第一个食材）

0. **v1.5.1+ 环境探测日志**（排障先看这段）：所有日志行带统一前缀
   `[HH:mm:ss.fff][主机|客机|单机|未知]`（角色=反射 `ConnectionStatus.IsInSession()/IsHost()`，
   实时求值，随进/出房间变化）——联机排障可直接区分两台机器、对齐时序。
   **v1.5.2 起**：经 `LogFromCrate` 桥转发的 stub 日志保证带 `[Stub:...]` 段
   （StubLog 打 `[Stub:Stub_<set>]`；旧版 stub 没带则 loader 兜底补 `[Stub]`）——
   有 `[Stub:` 段 = stub 桥接日志，没有 = loader 原生日志。启动首帧输出
   - `[环境] PluginPath/GameRootPath/dataPath/streamingAssetsPath` 路径解析结果；
   - `[环境] plugins/OC2DIYLevel` 与 `StreamingAssets/OC2DIYLevel` 两级目录清单
     （存在即列出内容，不存在明确打"不存在"）——levels 装没装、装哪了直接可见；
   - `[环境] AppDomain 相关程序集`——OC2DIYLevel / LevelEditorStub / Stub_* 是否就位；
   - 所有 levels 候选目录都不存在时打 **Warning** 并逐个列出候选路径；
   - 主路径缺失但备选路径（StreamingAssets）命中时会扫描并打 ⚠ 提示；
   - 每次场景加载打 `场景加载: <name>（已加载关卡程序集 N 个…）`；
   - `AssemblyResolve 未命中: Stub_*`（Warning，只报一次）= 关卡程序集没装上。

1. 看 `Overcooked! 2/BepInEx/LogOutput.log`：
   - 有 `[OC2LevelRuntimeLoader] 已加载关卡程序集: Stub_xxx` → 加载器正常，问题在场景/代码；
   - 完全没有 `[OC2LevelRuntimeLoader]` → DLL 没放对位置（应在 `plugins/OC2DIYLevel/` 内）或 BepInEx 未加载；
   - `runtime bundle 加载失败/未找到` → `levels/<set>/` 下缺 `runtime` 文件。
2. 确认 zip 是**最新导出**的（旧包里可能是过期 DLL）：编辑器 Console 应有
   `[CustomStub] 已自动打包 N 个关卡集的 Stub DLL` 或导出时的 staging 日志。
3. 确认 `plugins/OC2DIYLevel/commonW1` 已更新（否则问号贴图缺失，但随机仍应生效——
   若随机也不生效则与 commonW1 无关）。

- 自用测试：按上表放入 `BepInEx/plugins/OC2DIYLevel/`；
- 玩家：随「[前置]BepInEx + OC2DIYLevel」模组包整包分发（更新版 commonW1 同批）。

## 注意

- **v3.1.0 旧版关卡包向下兼容护栏**：玩家把「旧编辑器导出的关卡集」与「新版依赖包」
  混装时，自定义汉堡菜谱（`CustomRecipeOptionalBurgerSO.optionalSOs`）的跨 bundle
  食材引用可能解析失败（`GetOrderDefinitionNode` 返回 null），原版
  `RecipeHelper.GetOrderToPrefabLookup` 会在 `Dictionary[null]` 抛
  `ArgumentNullException`，把 `StartEmptySession` 炸成「游戏运行时出现严重错误」。
  v3.1.0 起 loader 用 Harmony 前缀滤除 null 节点并打 `[兼容]` 告警（含菜谱名），
  该菜谱少一个夹心候选但可正常进关；彻底修复 = 用最新编辑器重新导出该关卡集。
  修复只在 loader 侧（游戏内运行时打补丁），不改上游 OC2DIYLevel.dll，也不改
  编辑器侧 `Assets/Scripts/LevelEditor/RecipeHelper.cs`。
- **v3.2.1 护栏修正（重要）**：v3.1.0 的拦截方式（前缀改 `__args`）在 BepInEx
  5.4.22 的 HarmonyX 上**不回写**——护栏触发了但 null 仍进原方法照崩。v3.2.1 改为
  检出 null 即跳过原方法、反射重建滤空查找表（含 Equals 去重/m_amountAllowed/
  m_lookupArray 原语义），重建异常退空查找表保会话。告警同时给出定位上下文：
  「关卡 X（关卡集 Y，菜谱 Z）跳过 N 个失效食材引用」——关卡名/关卡集名来自
  `SetupConfig` / `SetupSceneDirectoryData(LevelSetInfoSO)` 前缀；同（关卡,菜谱）
  组合每会话只完整告警一次，重复归 verbose。

## 日志与排障文件

Loader 与 debugLog 会在 `Loader.dll` 同级创建 `logs/`，每次游戏启动建立一个
`logs_info_yyyyMMdd_HHmmss/` 会话目录，默认保留最近 21 次启动。目录内文件为：

- `info.log`：正常运行、警告和错误；
- `debug.log`：配置 `level=debug` 时的详细诊断；
- `player.log`：面向玩家的中文重大问题提示；
- `BepInExDebugLog.log`：通过 BepInEx 日志监听器捕捉的独立日志。

配置文件为同目录的 `log_config.txt`，采用 `xxx=yyy` 格式，空行和 `#` 注释行会被忽略。
默认开启 `info` 级别、Unity 日志和 BepInEx 日志捕捉。日志目录无法创建不会阻止游戏启动，
但应将该问题和游戏目录权限一并反馈给开发者。

- `level=warning`：只保存警告和错误；
- `level=info`：保存信息、警告和错误；
- `level=debug`：额外保存阶段断点和详细诊断。

Loader 的启动、依赖加载、运行时程序集加载、关卡扫描、场景补扫和程序集解析失败均有
`[断点:...]` 或 `[PLAYER]` 标记，发生问题时优先查看同一次启动目录下的四个文件。

- **版本警戒：v1.5.0 是坏包（2026-09-08 事故）**——日志助手 `Info/Warn` 因批量改名
  事故变成无限自递归，首个日志调用即栈溢出闪退（连 ready 行都打不出）。若真机日志
  显示 `Loading [OC2 LevelRuntime Loader 1.5.0]` 后无任何该插件输出 → 立即换 1.5.1+。
  教训：批量重命名后必须重读被改函数本体；不信增量编译的"0 警告"（源码未变会跳过 Csc）。
- 不要 `Unload` 已加载的 runtime bundle——场景组件类型存活于其中的程序集；
- 排查：看 BepInEx/LogOutput.log 里 `[OC2LevelRuntimeLoader]` 前缀的日志
  （"已加载关卡程序集: Stub_xxx" 即成功）。
