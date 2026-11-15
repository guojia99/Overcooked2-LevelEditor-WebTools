# Container transfer behavior checks

Run from the repository root:

```sh
dotnet run --project Assets/WebCustomStubRuntime/Container/Checks~/Container.Behavior.csproj
```

The net8 executable enforces `LangVersion=4` and compiles
`../ContainerTransferSync.cs` directly, with no production-source copy or rewrite.
`BehaviorHost.txt` follows the adjacent WebBackpack check-host convention.
All build output stays under this ignored-by-Unity `Checks~` directory.
No external packages are required. Each case runs in a fresh process so production
static caches and warning deduplication retain their real initialization behavior.
An individual case can be run by appending `-- <case-name>` to the command.

The original 31 cases cover both prefix execution orders and both `_dontRemove` values;
mixable/cookable runtime-only transfers; late upstream installation and removal
in the same process; exact upstream metadata after an unrelated prefix;
owner/method/declaring-type near misses; non-pot targets;
occupied sources; reflected constructor/Simplify failures before mutation;
Combine failures before and after a write; Destroy failure after a write;
missing reflection metadata; stable/state-transition/verbose logs; reflected
inner exception details; and warning deduplication. Assertions check prefix
return values, original fallback, writes, Combine calls, construction,
simplification, carrier destruction, and order-node identity.

Two additional groups bring the suite to 43 cases:

- Ten log-outlet cases inject Info, prefix-list Dbg, transfer-sample Dbg, or all
  outlet failures with and without upstream ownership, plus Warn failures after
  Combine has written for both `_dontRemove` values. Injection counters prove
  the relevant outlet actually threw. Upstream-owned calls must return true
  without constructing, combining, simplifying, or destroying anything in the
  runtime prefix; runtime-owned calls must still write exactly once and return
  false. A throwing Warn after a failed Combine must not escape or allow the original.
- Two destroyed-source cases validate the fake-null/name-throw contract and
  execute upstream-first removal with Verbose enabled. Every fixture binds its
  Source to its Carrier; successful removal marks the source destroyed. The
  remaining managed reference compares null through Unity-style `==`/`!=`.
  Name and instance-ID access counters verify runtime diagnostics never
  dereference the destroyed source while both prefixes still execute and
  produce exactly one write, construction, and carrier destruction.

## Boundaries

Harmony metadata and dispatch are explicit fakes, not real Harmony patching.
The dispatcher models HarmonyX's execution of **all prefixes**, even after one
returns false, and runs the original only if all prefixes permit it. It tests
both supplied orders, not actual Harmony priority sorting or native detours.
The upstream prefix is a behavioral stand-in with the exact owner/type/method
identity. This harness does not load OC2DIYLevel, Unity, or network components.
The fake original is an observable one-write fallback, not the vanilla algorithm.
Destroyed-object behavior is a focused mock: destruction is immediate and marks
only the Source component, not a full GameObject/component hierarchy. It does
not model Unity's deferred destruction, native lifetime, or all Object operators.
Logging failures are deterministic exceptions after recording a message attempt;
they do not reproduce the real loader bridge or logging backend.

Net8 plus C#4 validates language syntax and deterministic behavior, not net35
API availability or real Harmony binary compatibility. The main agent's net35
production build must verify those real APIs. Unity lifecycle, networking,
frame timing, and actual carrier effects require game integration testing.
Diagnostics assertions match semantic details, not exact translated messages.

## Verification

On 2026-11-12 the fixed production implementation passed all original 31 cases
in an actual execution, retaining the old assertions unchanged. This includes
single-write ownership, dynamic metadata, mutation-aware exception handling,
state-change diagnostics, and reflected inner exception details.

After isolating diagnostic failures from transfer decisions, all 43 cases passed
on 2026-11-12. Assertions were retained, including every log-failure injection and
the upstream-first destroyed-source case with Verbose enabled. The production
net35/C#4 build with the real Harmony reference also passed (four existing
CS0252 warnings in Terminal scripts). Unity compiled and staged the runtime DLL;
this does not replace a real-game multiplayer/scene-transition acceptance run.

The Windows `Assets/AssetBundles/webcustomstub_runtime` bundle was rebuilt with
Unity's targeted AssetBundle build API and reopened with `AssetBundle.LoadFromFile`.
Its embedded runtime DLL SHA-256 matched both the staged `.dll.bytes` and
`Library/ScriptAssemblies/WebCustomStubRuntime.dll`:

```text
0d0239b481d957d07030d4ae70a5e4eec663f14d258b288467567e6dbd093bf1
```

## 真机诊断与验收

- 常开日志搜索 `[ContainerTransfer] 单写守卫 r25`：安装后以及上游接管状态变化时
  输出一次。0.10.0 上游补丁存在时应显示 `写入归属=OC2DIYLevel` 和
  `WebRuntime 让行，不合并/不销毁`；缺失时显示 `WebRuntime 兜底`。
- 在 Loader 的 BepInEx 配置中设置 `[Logging] Verbose = true`，重启游戏；也可设置
  环境变量 `OC2_STUB_VERBOSE=1`。状态日志附带目标方法 prefix 数量，Verbose
  列出每个 prefix 的 owner、完整方法名和 priority，用于确认真实补丁来源。
- Verbose 每次接管状态变化后最多输出 8 条 `转移样本`，包含帧号、源/目标名称及
  Unity instance ID、`dontRemove`，以及 `yield-upstream` / `runtime-check`。
  `runtime-check` 只表示进入本方条件检查，不代表一定写入；样本额度跨场景保留，
  测试另一张图需要重新启动游戏，或在同次会话中观察归属状态变化。
- `转移异常` 按阶段去重，记录 `ownership` / `prepare` / `combine` /
  `destroy-carried`、`mutationStarted`、`original=allowed/blocked`、对象上下文
  和完整异常（含反射内层异常）。开始调用 Combine 后即禁止重放原方法，不能判断
  Combine 内部是否已部分写入，也不会自动撤销或重试本次操作。
- 更新游戏依赖目录 `OC2DIYLevelRuntimeWLoader/webcustomstub_runtime` 后重启游戏，
  仅替换 Loader.dll 不会更新此修复。保持发布版本号 4.0.0；内部修订为 4.0.0.25。
- 用 `Pizza_Base_Raw`、`HotdogBun_Chopped` 分别连续投掷和手放进锅、搅拌碗，
  每次合法入料应只增加一份，源食材只消耗一次；检查主机与客机内容一致。
  再覆盖退出重进、Web 图切官方图/其他图，验证原先常驻补丁不会造成后续双写。
  本地行为测试模拟了 HarmonyX 调度，以上真机联机/切图验收仍待执行。
