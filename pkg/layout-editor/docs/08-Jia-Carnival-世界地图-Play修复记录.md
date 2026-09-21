# Jia Carnival 世界地图 Play 修复记录

更新时间：2026-09-21

## 适用范围

本文记录本次会话对 `jia_carnival` 世界地图场景的诊断、修改和验证结果。

主要场景：

```text
Assets/LevelSets/jia_carnival/scenes/s_jia_map.unity
```

官方对照场景：

```text
Assets/LevelSets/jia_carnival/map/official/assets/downloadablecontent/dlc08/dlc_assets/Scenes/WorldMap_DLC08.unity
```

## 约束

- Unity 版本为 `2017.4.8f1`。
- 运行时代码必须兼容 C# 4.0。
- 未修改宿主反编译代码：`Assets/Scripts/`、`Assembly-CSharp-Patch/`、`BepInExPlugins/`。
- 运行时行为放在 Jia CustomStub：`Assets/LevelSets/jia_carnival/map/runtime/`。
- 不改变 Jia 地图节点布局、节点位置和官方 DLC08 UI 尺寸。
- `Assets/LevelSets` 被 Git 忽略，相关资产改动不能只依赖普通 `git status` 判断。

## 问题演进

### 1. 地图主体显示为白色

初始检查发现多个导出的 Shader 仍是 `DummyShaderTextExporter`，Fragment Shader 直接返回白色。少量空材质和空纹理不是整张地图变白的主因。

已恢复纹理采样的 Shader：

- `oc2_maptile.shader`
- `oc2_maphay.shader`
- `oc2_standard.shader`
- `oc2_standard_alpha.shader`
- `oc2_standard_cutout.shader`
- `oc2_interactable.shader`
- `oc2_flag_02.shader`
- `oc2_mapballoon.shader`
- `sh_gloss_d.shader`
- `sh_gloss_d_flag.shader`
- `sh_gloss_d_n_rim.shader`
- `sh_gloss_d_glass_opaque.shader`
- `vertexpush.shader`

恢复内容包括：

- 漫反射/颜色纹理采样。
- `_ColourAlpha`、`_DiffuseMap`、`_Diffuse` 等项目自定义纹理属性。
- `_RMEAO` 的金属度、平滑度和 AO 通道。
- 法线纹理。
- Alpha Cutout 或基础 Alpha 支持。
- 旗帜双面渲染。
- 旗台和玻璃的 Rim/Detail 基础效果。

仍未批量修改的占位 Shader：

- UI Shader。
- Confetti、粒子和后处理 Shader。
- 角色 Shader。
- DLC07 食物/鬼魂 Shader。
- 其他未确认实际用于世界地图的高级 `sh_gloss_*` 变体。

原因是这些 Shader 可能依赖透明混合、UI 专用渲染、粒子排序或后处理状态，不能用同一种 Surface Shader 全局替换。

### 2. Popup、音频和 Steam Editor 问题

`WorldMapDiagnostics.cs` 中保留以下运行时修复：

- 延迟处理过早触发的 `ServerWorldMapInfoPopup.OnEnable()`。
- 等 `StartSynchronising()` 完成字段赋值后启动 Popup 协程。
- 使用活动的 `PopupCoroutineRunner` 执行协程，避免 inactive Popup 直接 `StartCoroutine()`。
- 在 Unity Editor 中跳过 Steam 成就原生初始化，避免 macOS 缺少 `CSteamworks`。

`AudioDiagnostics.cs` 中保留：

- 清理 `m_oneShotAudio` 中的 null 或已销毁 `AudioSource`。
- 同步清理 `m_activeOneShotTags`，避免 `SingleInstance` 状态永久锁死。
- 防止 `AudioManager.Update()` 持续处理无效音源。

当前 `UIPop` 仍会解析到 `audioFile=null`，但无效条目能够被清理。尚未强行映射到某个 DLC08 音效，因为安全的运行时音频 tag 尚未确认。

### 3. 旗子和房屋白色

日志：

```text
logs/playmode_20260921_022253.log
```

确认结果：

- 普通旗面使用 `sh_gloss_d_flag`，原本是纯白占位，已修复。
- 锁定旗面使用 `sh_gloss_d`，已修复。
- 旗台/车辆等带法线和 Rim 的材质使用 `sh_gloss_d_n_rim`，已修复。
- 房屋玻璃使用 `sh_gloss_d_glass_opaque`，已修复。
- `Skyscraper_A` 的父级 Renderer `m_Materials: - {fileID: 0}` 是官方场景中也存在的占位 Renderer，不应直接给父级强行补材质。

### 4. Play 后装饰消失

日志：

```text
logs/playmode_20260921_100611.log
```

诊断统计从：

```text
renderers=2317
```

变为：

```text
renderers=2292
```

减少约 25 个对象。日志没有对应 Shader 编译错误，且场景包含大量 `WorldMapSceneryOptimizer`。

其宿主逻辑会在 `Awake()` 中先执行：

```csharp
m_mesh.SetActive(false);
```

正常地图展开流程结束后才会再次激活 Mesh。当前 Editor Play 的 `RunMapUnfoldRoutine`/同步流程没有完整恢复这些对象，因此 Play 后装饰保持隐藏。

当前在 `WorldMapDiagnostics.cs` 中加入 Jia 专用临时恢复：

- 地图场景激活后检查 `WorldMapSceneryOptimizer`。
- 读取其公开 `Mesh` 属性。
- 在前 6 秒内重新激活被隐藏的 Mesh。
- 不修改宿主 `WorldMapSceneryOptimizer.cs`。

这属于运行时兜底。后续如果确认地图展开同步可以正确修复，应该移除或缩小该兜底范围。

### 5. 相机高度、跟随和灰紫色画面

日志中记录：

```text
camera=Main Camera pos=(29.5, 17.9, -13.5) enabled=True worldMapCamera=True
avatar=MapAvatar pos=(30.4, 1.6, -1.3)
```

最初曾加入相机位置和朝向兜底，尝试降低相机高度并让它直接看向 `MapAvatar`。后来发现这可能导致相机位置正确但画面拍入树或其他近距离模型，因此已撤销激进的相机位置/旋转覆盖。

当前相机运行时组件只做诊断，不再强行改变官方相机：

- 不修改 Main Camera 的位置。
- 不修改 Main Camera 的旋转。
- 不覆盖 `WorldMapCamera/FollowCamera`。
- 记录目标、位置、头像位置、depth 和 culling mask。

## Camera 合成结论

用户确认 Main Camera Inspector 中显示正确，但最终画面主要显示 UI Camera 内容。对照官方 DLC08 场景后，两个相机配置一致：

| 相机 | Clear Flags | Depth | Culling Mask | Target Texture |
|---|---:|---:|---:|---|
| `Main Camera` | Skybox | `-1` | `4294967263` | None |
| `UICamera` | Depth | `1` | `32` | None |

该配置是正常的 UI 叠加模式：

- Main Camera 先渲染世界。
- UICamera 之后只渲染 UI 层。
- UICamera 使用 `Depth`，不应清除 Main Camera 的颜色缓冲。

因此不应直接：

- 禁用 UICamera。
- 修改 UICamera 的 depth。
- 修改 UICamera 的 clear flags。
- 将 UICamera 改成渲染世界层。

如果最终仍看到 UI 加灰紫色画面，下一步应检查：

1. Play 时 Main Camera 是否仍启用。
2. 是否出现第三台 Camera 或运行时复制 Camera。
3. Main Camera 的运行时 `depth`、`cullingMask`、`targetTexture`、`clearFlags` 是否被其他逻辑改写。
4. 是否有后处理或 RenderTexture 将 Main Camera 的颜色缓冲替换。
5. Main Camera 的实际画面是否被近距离模型完全遮挡。

## 当前修改文件

本次会话直接修改的主要文件：

### Runtime

```text
Assets/LevelSets/jia_carnival/map/runtime/Diagnostics/WorldMapDiagnostics.cs
```

包含：

- Popup 时序修复。
- Steam Editor 初始化保护。
- 相机/Renderer 诊断。
- Jia 地图装饰 Mesh 恢复兜底。
- 相机目标和渲染参数日志。

```text
Assets/LevelSets/jia_carnival/map/runtime/Diagnostics/AudioDiagnostics.cs
```

包含：

- 无效音源清理。
- `UIPop` 无效目录项清理。
- AudioManager 诊断探针。

### Shader

```text
Assets/LevelSets/jia_carnival/map/official/assets/downloadablecontent/dlc05/dlc_assets/shaders/
Assets/LevelSets/jia_carnival/map/official/assets/downloadablecontent/dlc08/dlc_assets/shaders/
Assets/LevelSets/jia_carnival/map/official/assets/shaders/
```

具体 Shader 名称见本文“地图主体显示为白色”一节。

### 场景

此前已修复：

```text
Assets/LevelSets/jia_carnival/scenes/s_jia_map.unity
```

修复内容为 `ScreenFader` 缺失 Image 组件及 `FaderUIController.m_image` 绑定。

## 验证记录

已执行并通过：

```bash
git diff --check
```

已检查：

- C# 4.0 禁止语法：未使用 `?.`、字符串插值、`nameof`、表达式体、`async/await`。
- 未修改宿主反编译代码。
- 官方 DLC08 相机和 UI Camera 配置对照一致。
- Steam Editor 的 `CSteamworks` 报错已通过初始化保护消除。

无法在当前环境完成：

- Unity Editor 实际 Shader 编译。
- Unity Play 画面实时验证。
- `dotnet build JiaMapRuntime.csproj --no-restore` 的完整构建，因为环境缺少 `.NETFramework,Version=v3.5,Profile=Unity Subset v3.5` 引用程序集。

## 后续建议

1. 用最新代码重新 Play，确认日志中 Main Camera 的运行时状态。
2. 在 Play 中检查 `Camera.allCameras`，确认只有 Main Camera 和 UICamera，或记录第三台 Camera 的名称、depth 和 target texture。
3. 临时关闭 Occlusion Culling 验证灰紫色是否来自遮挡剔除错误。
4. 如果 Main Camera 画面单独正常而最终画面异常，检查后处理和 RenderTexture 链路。
5. 如果仍有白色旗子/房屋，记录对象名、材质名、Shader 名，而不是继续全局替换 Shader。
6. 如果车辆位置仍固定为 `(30.4, 1.6, -1.3)`，继续检查 `ServerMapAvatarControls`、输入初始化和 Editor Play 的 session 状态；此时相机不是根因。
