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
| `UnityEngine.dll`（老式整包） | Unity 2017.4.8f1 编辑器安装目录 `Managed/` | `<GameDir>/Overcooked2_Data/Managed` |

> 注意：BepInEx 5.4.22 的 `BaseUnityPlugin` 编译自老式整包 `UnityEngine.dll`，
> 必须引用**同名程序集**做类型统一（勿改成 CoreModule 等模块 DLL，会 CS0012）。

## 打包（Windows）

```bat
dotnet build -c Release -p:GameDir="D:\Games\Overcooked! 2"
```

## 安装与分发

- 自用：把 `OC2LevelRuntimeLoader.dll` 复制到游戏 `BepInEx/plugins/`；
- 玩家：随你的「[前置]BepInEx」包或模组更新渠道分发一次即可（与更新版
  commonW1 bundle 同批）。

## 注意

- 不要 `Unload` 已加载的 runtime bundle——场景组件类型存活于其中的程序集；
- 排查：看 BepInEx/LogOutput.log 里 `[OC2LevelRuntimeLoader]` 前缀的日志
  （"已加载关卡程序集: Stub_xxx" 即成功）。
