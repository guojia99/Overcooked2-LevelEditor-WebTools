# OC2LevelRuntimeLoader（Loader.dll）

统一运行时加载器（BepInEx 插件，PluginGuid `oc2.oc2diylevelruntimewloader`，v4.0.0）。
把依赖包 `webcustomstub_runtime` bundle 里的统一运行时程序集（`WebCustomStubRuntime`，
含随机食材箱等自定义玩法）在**关卡场景加载之前**注入游戏 AppDomain——**代码随依赖包
分发，不进 Assembly-CSharp / common 包**。

## v4.0.0（2026-11-10 首个发布版）：收敛内部迭代 4.0.0–4.8.0

- 发布号统一为 4.0.0；原 4.0.1–4.8.0 全部并入本版，不再单独发号。
- 包含：commonW5 增量库、清单键改名免疫（内部号 4.0.0.16，原 4.5.0）、
  屏幕点探针、上游 `ICustomRecipeOptional` / `CustomRecipeOptionalSandwichSO` /
  `IngredientSplitter.outputPrefabSO`（内部号 4.0.0.17，原 4.6.0）等。
- 依赖包需重新导出（`LevelEditorStub.dll`、`IngredientSplitterRuntime.dll`、
  `webcustomstub_runtime`）。

## 清单键改名免疫（内部号 4.0.0.16，原 4.5.0，2026-11-08 修复）

stub 清单条目的「集」键 = **内部集名**，由集文件夹内 `info_<set>` bundle 文件名推断
（`DetectManifestSetName`）——它与 bundle 场景内固化路径 `assets/levelsets/<set>/scenes/…`
同源（导出时同名写出，玩家改名后不变）。原 4.4 及以前用磁盘文件夹名做键，玩家给
`levels/<set>/` 改名（加版本号/标注，社区 18 集 17 个中招）即与 EntryPoint 的场景
路径匹配断链 → 火锅/随机箱/传送门/锅具时间等 stub 玩法**静默失效**。内部号 4.0.0.16
（原 4.5.0）起：

- 无 `info_*`（或名字含 `|`、`/`、带扩展名）→ 回落文件夹名（原行为）；
- `info_*` 多命中（混装多集）→ 告警取第一个；
- 两个文件夹指向同一内部名（新旧版本并存）→ 告警（查表先到者命中，建议清理）；
- 改名集打常开汇总 Info：`4.0.0 清单键改名免疫：N 个关卡集的文件夹名与内部集名不同…`；
- **清单条目格式与 EntryPoint 查表逻辑零变更**——已分发关卡集无需重导出，
  更新 `Loader.dll`（或整包 OC2DIYLevelRuntimeWLoader ≥ 4.0.0）即修复。

## 零介入铁律（v3.5.0）

未使用 web 导出的关卡（官方图/旧导出集/无自定义关卡），本插件及其注入的全部运行时
对游戏**零介入**——不 hook 任何函数、不装任何补丁、不做任何场景扫描/探测；web 关卡集
里未用 CustomStub 的关卡同样零监控零对局钩子。

机制（两道闸门 + 逐关卡清单）：

1. **启动闸门**：后台扫描汇总 `levels/<set>/stub_levels.txt`（导出器逐场景写出，
   每行 `关卡|特征,...`）。清单为空 → **完全休眠**：不加载 commonW*/统一运行时/
   自定义 runtime、不挂 AssemblyResolve，一个字节都不加载。
2. **逐关卡闸门**（CustomStub.EntryPoint.ProcessScene）：`sceneLoaded` 按
   `scene.path` 解析 `(集, 关卡)` 查清单——未命中（官方图/主菜单/世界地图/旧集/
   未用 CustomStub 的 web 关卡）直接休眠归零；命中按**清单特征**挂载：
   - KillPlane 补丁仅 `pushable` 关卡；
   - ticker 子系统轮询各按特征（HotPot←hotpot|pushable、VoidFall←pushable、
     UtensilTiming←timing、TerminalGuard←terminal、CannonGuard←cannon）；
   - 触发区占用同步 + 联机诊断随核心（清单命中即装）；
   - TickProbe 30 秒探测通道真机退役（仅编辑器 Play 无约束模式保留）。
3. 清单经 `public static string[] WebManifest` 注入，EntryPoint 反射读取
   （null = 无约束 = 编辑器 Play，维持旧行为）。

版本门控：`requires.txt`（依赖包 SSOT 版本）semver 比较，过低则该集跳过 stub 支持
（关卡本体仍加载）。

**旧版导出集兼容（3.5.0 ≤ 版本 < 4.0.0，用户决策 2026-09-28）**：有 `requires.txt`
无 `stub_levels.txt` 的集（如 3.2.1 导出）→ loader 生成 `"集|*|legacy"` 兼容条目，
EntryPoint 维持 v3.4 行为（探测 + tag 自愈 + 特征全开）——**已分发旧集无需重导出
即可继续工作**。v4.0.0 起严格按 `stub_levels.txt` 清单（届时旧集需重新导出一次；
新导出的集始终走严格清单路径）。

## 打包（mac 本机一键构建）

```sh
./build.sh            # 版本号从 Loader.cs 自动提取
# Windows: dotnet build Loader.csproj -c Release -p:GameDir="D:\Games\Overcooked! 2"
```

产物：`bin/Release/Loader.dll`（net35）→ 手动拷贝覆盖
`layout-editor/web/public/Loader.dll`（导出依赖包 zip 时打进 `OC2DIYLevelRuntimeWLoader/`）。

引用来源（csproj 自动回退，均可用 `-p:` 覆盖）：

| 引用 | mac 回退路径 | Windows |
|---|---|---|
| `BepInEx.dll`（5.4.22） | `deps/BepInEx/` 或 `~/Downloads/[前置]BepInEx/BepInEx/core` | `-p:GameDir=...` → `<GameDir>/BepInEx/core` |
| `UnityEngine.dll`（老式整包） | Unity 2017.4.8f1 编辑器安装目录 `Managed/` | `<GameDir>/Overcooked2_Data/Managed` |

> ① BepInEx 5.4.22 的 `BaseUnityPlugin` 编译自老式整包 `UnityEngine.dll`，必须引用
> **同名程序集**做类型统一（勿改成 CoreModule 等模块 DLL，会 CS0012）。
> ② **无 0Harmony 引用**——v3.5.0 删除 RecipeHelper 兼容护栏后，loader 回归纯
> 「扫描 + 加载 + 日志」，不含任何 Harmony patch。

## 安装与分发（位置务必放对）

依赖包目录：`Overcooked! 2/BepInEx/plugins/OC2DIYLevelRuntimeWLoader/`
（`Loader.dll` + `webcustomstub_runtime` + `commonW1/commonW2/...`）；
关卡集仍在 `plugins/OC2DIYLevel/levels/<set>/`（模组固定读取路径）。
web 导出 zip 整体解压即全部就位（不要把 `.meta`/`.manifest` 拷进去）。

## 真机排障

0. **状态行**（LogOutput.log，排障先看）：
   - `未发现任何 web 导出的 stub 关卡（stub_levels.txt 无条目）——加载器休眠` =
     清单空（未装 web 关卡或全部未用 CustomStub 且无旧版集）；
   - `stub 关卡清单命中 N 关…，另含 M 个旧版导出集（兼容模式：探测+自愈）——激活` = 正常激活；
   - `[set] 旧版导出（无 stub_levels.txt）→ 兼容模式（探测+自愈，同 v3.4 行为…）` =
     旧集兼容条目已生成（Verbose/启动清单）；
   - `[set] 依赖包版本过旧` = 需要更新依赖包；
   - `[断点:闸门] 场景 … → 集=… 关卡=… 清单命中/未命中（特征=…）` = 进入 web
     关卡时的常开诊断（不依赖 Verbose）：先看这行区分「该关本就不适用 stub」与
     「导出清单漏项」；卡加载时尤其先看它。
1. `[Stub:...]` 段 = stub 桥接日志（经 `LogFromCrate`），无 = loader 原生；
   EntryPoint 侧逐关卡判定看 `[CustomStub] 核心激活` / `关卡未命中 stub 清单`。
2. 关卡内玩法失效但清单命中 → 看 `[CustomStub] 核心激活（stub 清单命中 … 特征=…）`
   里特征是否齐全；缺特征 = 导出时场景内容未被 `CollectActiveSceneStubFeatures`
   扫到（新 stub 类型漏映射，见 docs/05 §3.3 特征表）。
3. **文件夹改名集**（内部号 4.0.0.16 / 原 4.5.0 前的静默失效根因）：启动日志应有
   「4.0.0 清单键改名免疫」汇总行；若 Verbose 里仍见 `[CustomStub] 关卡未命中 stub
   清单` 且文件夹名 ≠ `info_<内部名>`，说明 Loader.dll 旧于 4.0.0——更新依赖包即可，
   无需改回文件夹名、无需重导出关卡集。
4. `AssemblyResolve 未命中: Stub_*`（Warning，一次）= 关卡程序集没装上。
5. **每帧刷 `Screen position out of view frustum (screen pos …) (Camera rect 0 144 …)`**
   （2026-11-08 呆喵喵图 Dmm/s_dmm_10 首例，非 16:9 显示器 41 秒 2.9 万条）：
   相机 rect 是原版 `FixedAspectRatioCamera` 的 16:9 letterbox（正常行为）；
   错误来自**某个逐帧把全屏坐标喂给 letterbox 相机的调用方**——已排除本
   loader/运行时与游戏本体（均无此调用），疑为上游 OC2DIYLevel 或第三方模组
   （同机装有 OC2StageThumbnailPreview）。原生错误**无托管堆栈**，定位方法：
   BepInEx 配置 `[Logging] Verbose=true` → 运行时（≥4.0.0）自动装【屏幕点探针】
   （core/ScreenPointProbe.cs，只观察不拦截），越界调用会在日志报
   `[CustomStub][屏幕点探针] … 调用方堆栈：` 点名责任方。
   **debugLog 2.0.1 起**：该类原生错误与 `Material doesn't have a texture
   property '_MainTex'` 不再逐条写 `player.log`（避免「游戏运行时出现严重错误」
   误报），`info.log` 只留首条样本 + 30 秒计数汇总；真正的托管异常仍完整记录。
   **Loader（4.0.0 构建）新增 `[Diagnostics] ScreenPointProbe`（默认开启）**：
   进入 web 关卡后给 Camera 的屏幕/视口点 API 挂只观察前缀，同一调用点累计
   ≥50 次即打印**调用方托管堆栈**（最多 3 个调用点，90 秒后自动停止采样）——
   用于把「一直刷 frustum」的元凶从噪音里揪出来；不拦截、不改变游戏行为，
   设为 `false` 可关闭。
6. **版本警戒**：v1.5.0 坏包勿分发（日志助手自递归闪退）；3.3.4 吸附写错物体勿分发。

## 注意

- **v3.5.0 移除了 v3.1.0/v3.2.1 的 RecipeHelper 兼容护栏**（零介入铁律：不主动
  hook 非 web 关卡路径的函数）。旧导出集与新依赖包混装时的「严重错误」崩溃会回归，
  解决方式 = 用最新编辑器重新导出该关卡集。
- **兼容期（3.5.0 ≤ 版本 < 4.0.0）**：旧版导出集（无 stub_levels.txt）走
  `"集|*|legacy"` 兼容条目（探测+自愈，同 v3.4 行为），**无需重导出**；新导出的集
  始终带清单走严格逐关卡路径。v4.0.0 起移除兼容条目（届时旧集需重新导出一次）。
- 不要 `Unload` 已加载的 runtime bundle——场景组件类型存活于其中的程序集；
- loader 自身不含任何 Harmony patch；真机日志里的 `Patched: ...` 来自 OC2DIYLevel
  或 stub 程序集内部（由 EntryPoint 按清单特征/按需安装）。

## 日志与排障文件

Loader 与 debugLog 在 `Loader.dll` 同级创建 `logs/`，每次启动一个会话目录
（默认保留 21 次）：`info.log` / `debug.log` / `player.log` / `BepInExDebugLog.log`。
配置 `log_config.txt`（`xxx=yyy`，`#` 注释）。诊断级日志由 BepInEx 配置
`[Logging] Verbose=true` 开启（stub 侧 StubLog 同一开关）。
