# 上游宿主手动同步

`git merge` 对应合并提交 `facab1e27` 已同步 origin 的 `34ed05cf9`（该合并的第二父提交，且合并已在当前 HEAD 历史中）。但 `Assets/Scripts/Assembly-CSharp/` 下的 ignored 反编译副本不随 Git 更新，仍需用户手动同步。

`layout-editor/docs/upstream-host-sync.patch` 是基于当前四个文件制作的局部 unified diff，仅供审阅和用户手动应用：

- `Assets/Editor/CustomRecipeSOEditor.cs`：补 `using System.Linq;`，供现有数组 `Contains` 调用使用。
- `Assets/Scripts/Assembly-CSharp/GameUtils.cs`：同步根 `Assembly-CSharp-Patch/GameUtils.cs` 的 `using LevelEditor;` 和 `GetOrderPlatingPrefab` 两处容量 patch 块。
- `Assets/Scripts/Assembly-CSharp/ClientTray.cs`、`Assets/Scripts/Assembly-CSharp/ServerTray.cs`：各同步对应根 patch 文件的 Linq import 和槽匹配 patch 块。

保留现有 patch 标记、块内代码及宿主其它差异，不整文件覆盖。Agent 仅创建本文与补丁，不修改四个目标文件、上游目录或权限，不执行补丁、不触发编译／真实修复。

在仓库根目录只检查适用性（不写工作树或索引）：

```sh
git apply --check layout-editor/docs/upstream-host-sync.patch
```

本次 `git apply --check --verbose` 已检查四个目标并以退出码 0 通过，未应用补丁。两个 Tray 文件为 CRLF，其补丁行保留对应行尾；请勿统一转换补丁行尾。适用结果仅针对本次读取的文件状态，文件变化后须重新检查。

用户审阅并自行完成四处同步后，在仓库根目录验证：

```sh
python3 layout-editor/scripts/check-native-host-overlay.py
python3 layout-editor/scripts/check-current-host-backend.py --inspectors
```

第一条只读检查现有上游 import／patch 块，预期三项 PASS；不覆盖宿主其它差异，也不检查 Inspector。第二条以当前源码做 C#4/net35 编译，含 Inspector、宿主及独立后端，需要本机 Unity Managed 引用、dotnet 与缓存的 net35 引用程序集；不是 Play／真机验证。也可由用户在 Unity 手动 Refresh 并检查 Console。

生成时基线：Inspector 缺 Linq import；overlay 检查为 FAIL（GameUtils 缺 3 项，ClientTray／ServerTray 各缺 2 项），符合尚未手动同步的状态。未运行编译，不能据此宣称修复或编译通过。
