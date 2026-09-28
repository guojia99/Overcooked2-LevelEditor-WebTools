# OC2LevelRuntimeLoader（Loader.dll）

统一运行时加载器（BepInEx 插件，PluginGuid `oc2.oc2diylevelruntimewloader`，v3.5.0）。
把依赖包 `webcustomstub_runtime` bundle 里的统一运行时程序集（`WebCustomStubRuntime`，
含随机食材箱等自定义玩法）在**关卡场景加载之前**注入游戏 AppDomain——**代码随依赖包
分发，不进 Assembly-CSharp / common 包**。

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
   - `[set] 依赖包版本过旧` = 需要更新依赖包。
1. `[Stub:...]` 段 = stub 桥接日志（经 `LogFromCrate`），无 = loader 原生；
   EntryPoint 侧逐关卡判定看 `[CustomStub] stub 清单命中/未命中`（Verbose）。
2. 关卡内玩法失效但清单命中 → 看 `[CustomStub] 核心激活（stub 清单命中 … 特征=…）`
   里特征是否齐全；缺特征 = 导出时场景内容未被 `CollectActiveSceneStubFeatures`
   扫到（新 stub 类型漏映射，见 docs/05 §3.3 特征表）。
3. `AssemblyResolve 未命中: Stub_*`（Warning，一次）= 关卡程序集没装上。
4. **版本警戒**：v1.5.0 坏包勿分发（日志助手自递归闪退）；3.3.4 吸附写错物体勿分发。

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
