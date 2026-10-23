namespace CustomStub
{
    /// <summary>
    /// CustomStub / WebCustomStubRuntime 统一权威版本号（SSOT · semver）。
    ///
    /// 唯一锚点：EntryPoint / RandomCrate 的日志金丝雀、staging 写入的运行时版本、
    /// Loader 的 PluginVersion 与关卡集 requires.txt 门控，全部以本常量为准。
    /// 改版本号只改这一处（Loader 侧同步 PluginVersion，构建期一致性校验兜底）。
    ///
    /// 语义（版本门控）：关卡集导出时把本值写进 levels/&lt;set&gt;/requires.txt；
    /// Loader 用自身 PluginVersion 与之 semver 比较——已装依赖 &gt;= 关卡集 requires
    /// 才加载 stub 支持，否则警告跳过（关卡本体仍加载）。
    /// </summary>
    public static class StubVersion
    {
        /// <summary>权威 semver 版本号（形如 2.0.0）。
        /// 3.7.0（2026-10-06 真机「部分关卡进图永久卡加载」双根因修复）：
        ///  - 修复①（双 stub 强转崩溃·真机卡加载主因）：编辑器 wrapper 升级路径
        ///    （酱料机/饮料机、上菜台/回收台变体）补挂派生 stub 后残留基础
        ///    PseudoPrefabStub（组件序在前）。真机 OC2DIYLevel 按派生 stub 分类挂
        ///    派生运行时，PseudoPrefab.Awake 的 GetComponent&lt;PseudoPrefabStub&gt;()
        ///    命中基础 stub → 派生 Setup 开头强转 (派生Stub) 抛 InvalidCastException
        ///    中断 ResetAllPseudoPrefabs 全链 → 关卡永久卡加载（diantang /
        ///    jia_level_2_1 酱料机实锤）；编辑器侧因基础 Setup 空操作而静默。
        ///    修复：EntryPoint.Install 无条件装 ResetAllPseudoPrefabs 前缀自愈
        ///    （HarmonyPatches.DualStubHealPrefix：AddComponent 循环前移除基础
        ///    stub + 分发器生成食材回落 soArray 首项）——存量坏包免重导出；
        ///    编辑器侧 StubIO 三分支补齐「删 base 对」范式 + 导出前自动修复
        ///    （LayoutEditorDualStubRepair，含存量场景修复菜单）。
        ///  - 修复②（空清单休眠→commonW 不加载）：只引用 commonW 素材而无
        ///    CustomStub 玩法的关卡 stub_levels.txt 为空 → 加载器休眠 → commonW
        ///    不加载 → 场景外部引用无法解析 → 卡加载（LaTiao 0.9.1+ 导出实锤）。
        ///    修复：Loader v3.7.0 存在 stub_levels.txt 文件（即使 0 条目）即完整
        ///    激活；导出器补 "web" 特征（场景/LevelInfo 引用 commonW 素材即写行）。
        ///  - 修复③（进图竞态窗口）：异步依赖队列全程 ~15s，快速连按空格/A 可在
        ///    队列完成前进图（commonW 未就位/自愈补丁未装 → 同样卡加载）。
        ///    修复：Loader v3.7.0 进图闸门（LoadLevelAsync 前缀同步收口，解压仍
        ///    走工作线程）+ 运行时 bundle 优先入队（EntryPoint/自愈补丁秒级就位）。
        ///  - 存量坏包（双 stub / 空清单）装本版依赖包即修复，无需重导出；
        ///    重导出则根治（场景不再带基础 stub 对、清单带 web 行）。
        ///    requires.txt 门控随本版本对齐（Loader PluginVersion 同步 3.7.0）。
        ///
        /// 3.6.0（2026-09-29 联机「双按钮+传送带×4 偶发方向未旋转/停位不齐/动画
        /// 未完整执行」修复，运行时逻辑变更·loader 逻辑零变更仅同步版本）：
        ///  - 根因①（丢步/差 90°）：按钮链每步 = 主机 ServerTimedQueue 广播一条
        ///    TimedQueueMessage(index=0)，全端 ClientTimedQueue 到点 SetTrigger(BLPress)。
        ///    网络突刺/时钟追赶把两条消息挤进同一帧时，一帧两次 DoEvent 的两次
        ///    SetTrigger 被 Mecanim 单值 Trigger 合并为一次过渡 → 该端永久少转
        ///    一步（≤5° 停稳吸附救不了）；单机走本地回环分帧，必现不了。
        ///  - 根因②（停位差几度不回正）：ConveyorDirectionSync 吸附基准在 OnEnable
        ///    捕获，真机 station child 晚于自愈挂载实例化时退用伪根旋转（失真）
        ///    且永不重捕 → 吸附失效。
        ///  - 修复 F2（治本·步数对账）：新增 NodeRingSync（免 tag，HealScene 扫
        ///    Design/Animated Objects 组根，按 controller 参数表含 BLPress 判定
        ///    节点环）；Harmony postfix（EnsureNodeRingAuditPatches 按需装：
        ///    ClientTimedQueue.ApplyServerEvent / ClientTriggerQueue.DoEvent）把
        ///    权威步数（QueueEvent 计数）与 SetTrigger 计数交给组件，落后且停在
        ///    Idle → Animator.Play(Idle_目标) 瞬跳终态（Idle 定持 clip 常值=精确
        ///    姿态）；SetTrigger 计数 > 本地步数 = 丢步直接证据（常开日志）。
        ///  - 修复 F1（防重入 + Pending 门控）：ButtonLogicRelay 主机侧发 BLGo 前
        ///    检查目标组——节点动画进行中/对账未平（NodeRingSync.IsBusy）或传送带
        ///    在途投递（ServerConveyorStation.IsConveying||m_receiving，反射）则
        ///    推迟派发（复刻 vanilla ServerTriggerAnimationOnConveyor 的 Pending
        ///    语义：从不边送边转）；按目标计数排队防丢，2s 超时 fail-open 强发。
        ///  - 修复 F3（吸附基准）：station 晚就绪时首次解析到即重捕基准组（打日志）。
        ///  - 存量关卡无需重写回（全部走运行时自愈/反射，不动烘焙资产）；
        ///    requires.txt 门控随本版本对齐（Loader PluginVersion 同步 3.6.0）。
        ///
        /// 3.5.9（2026-09-29 版本对齐 · Loader 性能与排障系列收口）：
        ///  - 本运行时**逻辑零变更**，仅将 SSOT 版本对齐 Loader v3.5.9
        ///    （v3.5.2~3.5.9 全部为 loader 侧改动，详见 Loader.cs 头部变更记录：
        ///    依赖 bundle 异步分帧加载修复启动读档卡 2-5 秒、帧卡顿看门狗、
        ///    ≥100ms 慢调用定位、info bundle 预载尝试与回退、日志减负）。
        ///  - 对齐后导出的 requires.txt/依赖包 zip 名统一为 3.5.9，与 Loader.dll
        ///    PluginVersion 一致（本常量的同步要求恢复满足）。
        ///  - ⚠ 编辑此文件后需 Unity 重编译 + AutoBake 重新 staging
        ///    webcustomstub_runtime（导出新鲜度守卫会强制拦过期包）。
        ///
        /// 3.5.1（2026-09-29）：
        ///  - RatHeist 打鼠修复：包装 prefab 补 PseudoPrefab（实体扫描前生成老鼠）；
        ///    晚生成路径 ServerRegisterObject 补挂 Interactable 同步器；DynamicGridLocation
        ///    占格跟随；蟑螂视觉子树碰撞体禁用以免挡交互扫描。
        /// Loader 的 PluginVersion 必须同步为同值。</summary>
        /// 3.5.0（2026-09-28 零介入铁律 · 清单驱动的逐关卡按需挂载）：
        ///  - 铁律：未使用 web 导出的关卡（官方图/旧导出集/无自定义关卡），loader
        ///    与本运行时对游戏零介入——不 hook 任何函数、不装任何补丁、不做任何
        ///    场景扫描/探测；web 关卡集里未用 CustomStub 的关卡同样零监控零对局钩子。
        ///  - 机制：导出器逐场景扫描写出 levels/&lt;set&gt;/stub_levels.txt（每行
        ///    "关卡|特征,..."，15 类特征：crate/pushable/hotpot/timing/switch/
        ///    startvisual/blrelay/animgrid/conveyor/camera/travelator/teleportal/
        ///    rat/worldmap/terminal/cannon）；loader 汇总为 WebManifest 注入
        ///    EntryPoint（反射桥）；sceneLoaded 按 scene.path 解析 (集,关卡) 查
        ///    清单决定归零或按特征挂载。
        ///  - KillPlane 补丁拆出核心组：仅 pushable 特征关卡安装；ticker 子系统
        ///    （HotPot/VoidFall/UtensilTiming/TerminalGuard/CannonGuard）各按特征
        ///    开关轮询；触发区占用同步 + 联机诊断随核心（清单命中即装）。
        ///  - TickProbe 30 秒探测通道真机退役（清单权威化，消灭 IsLargePot 名字
        ///    子串误判类风险），仅无约束模式（编辑器 Play）保留。
        ///  - loader 侧：删除 v3.1.0/v3.2.1 RecipeHelper 兼容护栏（不主动 hook
        ///    非 web 关卡路径的函数；旧集混装崩溃回归 = 重新导出解决）；清单为空
        ///    = 加载器整体休眠（不加载任何 bundle/程序集）。
        ///  - **兼容期（2026-09-28 用户决策）**：3.5.0 ≤ 版本 &lt; 4.0.0 期间，
        ///    旧版导出集（有 requires.txt、无 stub_levels.txt，如 3.2.1 导出）由
        ///    loader 生成 "集|*|legacy" 兼容条目——EntryPoint 维持 v3.4 行为
        ///    （探测+tag 自愈+特征全开），**已分发旧集无需重导出即可继续工作**；
        ///    v4.0.0 起严格按 stub_levels.txt 逐关卡清单（届时旧集需重新导出一次
        ///    以生成清单）。新导出的集始终走严格清单路径（本版导出器即写出清单）。
        ///
        /// 3.4.0（2026-09-28 互锁/共轭外观与初始关闭色）：
        ///  - 新增 SwitchStartVisual：初始关闭（startEnabled=false）开关开局即显示
        ///    关闭色并预禁用交互（此前宿主 Setup 先设绿材质，关闭色要等同步期
        ///    ClientSwitchCosmeticDecisions 轮询，开局一段绿闪、真机无保障）。
        ///    编辑器写回烘焙组件；真机由 EntryPoint 扫 PseudoPrefabSwitchStub
        ///    .startEnabled 补挂（无 tag 载体）。编辑器侧另有编辑态预览 pass。
        ///  - ButtonLogicRelay：启动摘要日志（模式=互锁 A=/B=｜初始状态｜分发数，
        ///    一眼识别「互锁退化为共轭」）+ 互锁翻转投递结果打点（✓/✗）+
        ///    child 解析失败告警提示撞名/旧烘焙。
        ///  - 按压防抖 ≥0.35s（MinPressIntervalSeconds）：无绑定组互锁换手改为
        ///    0.35s 脉冲态（原 AReady↔BReady 瞬时换手零防抖，连按可每帧来回翻）；
        ///    relay 对运行期不足最短间隔的联动延迟回绿/换手并吞掉窗口内按压触发
        ///    （快速动画/脉冲场景连按只算一次）。
        ///  - 无绑定组共轭修复：stateNames 补 "Run" 态 + 0.35s 合成脉冲 clip——
        ///    此前 relay 检测不到 Run、「按一个另一个不动」。
        ///  - 配套烘焙侧（Editor 程序集）：互锁 BLRelay tag 写入 |X:A,B（此前漏传
        ///    导致互锁运行期退化共轭）；联动源按钮唯一命名 <原名>_BL<n>（N:/X: 与
        ///    GameObject.Find 的身份键不再依赖 Switch (N) 自动后缀）；联动源停用
        ///    SwitchReenable 自动复位（互锁按下侧不再被 0.35s 拉回绿）。
        ///
        /// 3.3.5（2026-09-25 热修：3.3.4 吸附写错物体致传送带乱飞）：吸附把 90°
        ///  整数绝对旋转写到了【组根】世界旋转——组根位于原点时成员绕原点公转
        ///  飞出（Play 一按即飞）。修复：改为在【wrapper】（站点祖先链上、组根
        ///  的直接子，即 clip 曲线目标）上施加 ≤5° 的相对修正（绕自身轴就地
        ///  旋转，与节点 clip 同款驱动方式）。⚠ 3.3.4 勿分发。
        ///
        /// 3.3.4（2026-09-25 传送带旋转停位差几度修复）：节点环 Idle 原为空状态
        ///  （无曲线+WD=off），exit-time 瞬时转移的接管帧不写旋转——transform
        ///  残留结束前最后一帧采样（几度级残差，是否命中结束帧取决于帧时机，
        ///  时准时不准；clip 烘焙数据本身端点精确）。双层修复：①运行时
        ///  ConveyorDirectionSync 停稳位姿吸附——相对基准 Y 残差 ≤5° 容差归整到
        ///  最近 90° 倍数（写动画祖先 wrapper 防累积漂移；tag 自愈通道，存量
        ///  场景无需重写回）；②烘焙侧 Idle 挂「节点起始姿态」定持 clip（接管帧
        ///  起每帧重写精确姿态，根治）+ 入口混合时长钳制 ≤clip 一半（防短 clip
        ///  混合未完被 exit 打断）——②需重新写回生效。吸附带监控日志
        ///  （残差角度全量打点）。
        ///
        /// 3.3.3（2026-09-25 12:47 真机「按一次永红」残留修复）：3.3.2 的按钮
        /// 生命周期接管在真机仍失效——ResolveButtonChild 反射的 PseudoPrefab
        /// 是编辑器专用程序集脚本，真机 Missing Script，静默返回 null（真机日志
        /// 实证：动画链两次旋转正常、[ButtonLogicRelay] 零日志）。修复：解析加
        /// 通道 2 兜底——子树扫 TriggerDisableScript（vanilla，真机可解析，
        /// SwitchReenable 同款模式）；复位触发名按实例 m_enableTrigger 读取而非
        /// 硬编码 "Reset"。监控增强：Disable/Reset 投递打 sent/total 全量打点，
        /// child 解析失败按按钮根一次性告警，新增 Run 状态卡死（BLDone 丢失）
        /// 30s 看门狗告警。
        ///
        /// 3.3.2（2026-09-25 真机按钮永红修复）：ButtonLogicRelay 接管按钮生命周期——
        ///  进入 Run 向全部配对按钮 child 发 Disable（共轭：按一个双锁），离开 Run
        ///  （动画完成）发 Reset（一起变绿）。根因：香草开关无自复位，回绿通道
        ///  （编辑器 LayoutEditorSwitchLinkPatch / 真机 SwitchReenable tag）此前只随
        ///  机器联动（switchLinks）烘焙，纯 ButtonLink 按钮两头都没配上（真机实测
        ///  02:02：两次旋转正常、按钮各按一次后双双永红）。tag 载体加 N: 按钮根名段。
        ///
        /// 3.3.1（2026-09-25 真机按钮锁死修复）：ButtonLogicRelay / AnimGridMemberSync
        ///  此前作为场景烘焙组件分发——真机无 CustomStub 脚本注册表，烘焙件是
        ///  Missing Script 死件 → BLGo 无人分发 → BLDone 永不到达 → 锁定模式下
        ///  BLAdv 永久挂起 = 「按一次就无法再按」。修复：接线配置编码进 BLRelay|
        ///  tag（BLRelay|P:..|B:..|E:状态>触发>目标|D:..，%,;>| 转义），EntryPoint
        ///  自愈补挂；AnimGridMemberSync 免 tag（扫 Design/Animated Objects 成员
        ///  直接补挂，零配置）；导出前缀表补 BLRelay|。
        ///
        /// 3.3.0（2026-09-24 按钮动画联动 v3）：
        ///  - 新增 ButtonLogicRelay：按钮联动 helper 的「状态→组分发」场景组件——
        ///    controller 内嵌 SendTriggerToObject/ClearTriggerDuringState SMB 在
        ///    Unity 2017.4 资产往返中不持久化（运行期按压无分发 + 回导恒空），
        ///    分发整体迁移到场景组件（含锁定/共轭互斥/迟到 done 防锁存）。
        ///  - 新增 AnimGridMemberSync：动画成员子树 StaticGridLocation → vanilla
        ///    DynamicGridLocation 换装，传送带喂料目标随动画移动自动跟随。
        ///  - ConveyorDirectionSync 重写刷新时序：位姿停稳 + 自身无在途投递才
        ///    刷新投递目标（修「旋转后整排传送卡死」——m_receiving 永久卡 true），
        ///    并支持平移后的格位回写（m_gridIndex）。
        ///  - 配套编辑器侧能力（烘焙于 Editor 程序集）：开关动画组节点环（逐节点
        ///    推进/同 startTime 并行/单旋转自动往返）、多源共控按钮、同按模式。
        ///
        /// 3.2.1（2026-09-22）：
        ///  - Loader 动态读取自身目录下所有 commonW1、commonW2、commonW3...
        ///    依赖包，导出器同步自动分发 commonW3 及更高扩展包；
        ///  - 依赖包导出目录增加 version.txt，记录 Loader/debugLog 版本。
        ///
        /// 2.4.0（2026-09-19）：
        ///  - 新增大炮防线 CannonGuard（编辑器 Play 与真机统一路径）：
        ///    ① 空炮发射拦截——ServerCannon.OnTrigger 前缀，m_loadedObject 为空或
        ///    玩家已脱离 AttachPoint 即跳过原方法。原版此路径 m_flying 永久 true +
        ///    客户端 LaunchProjectile(null) NRE，大炮从此拒入（真机实测：空炮按
        ///    一次后谁都进不去炮）。
        ///    ② 按钮占用门控——按「炮内是否有人（挂 AttachPoint 下）」状态迁移时
        ///    发 Disable/Reset（走原版 ServerTriggerDisableScript 网络通道，双端
        ///    变灰/点亮+不可按/可按），空炮不再常亮可按。
        ///    ③ SwitchReenable 绑定前/复位前双重复查跳过大炮发射按钮（旧烘焙场景
        ///    不重写回即受保护）；烘焙侧同步改为大炮联动按钮不烤自动复位。
        ///  - GameApi 新增 Cannon/ServerCannon/SetupCannonStub 反射组 + 命名空间
        ///    候选表补 LevelEditorStub. 前缀。
        /// 2.3.0（2026-09-19）：
        ///  - 新增老鼠偷食材（RatHeist）：开局藏身工作台下，按可调间隔出洞，
        ///    服务端权威偷取台面/地面物品（TakeItem→Carry/Attach→DestroyObject
        ///    全走官方网络消息），拖回销毁循环；玩家交互键=打一下掉落食材+逃回家；
        ///    无目标时随机巡逻（≥5 格）再回洞。
        ///  - 分类开关（默认只偷原材料，排除正在烹饪/搅拌的锅）、半径/速度/皮肤
        ///    （retro / dlc08 官方 h18 同款贴图换肤）全参数化；客户端影子演出
        ///    以携带/销毁同步事件矫正。tag 载体 RatHeist|。
        ///  - 实测修复①：rat.prefab 无 "Attachment" 子物体 → PlayerAttachmentCarrier
        ///    挂点全 null → 物品 Attach 后不跟鼠走。双端补建同名挂点+反射直写
        ///    m_attachPoints。
        ///  - 实测修复②：老鼠未被实体扫描同步化时无 ServerPlayerAttachmentCarrier
        ///    → CarryItem 从未执行（物品 TakeItem 后掉地上）。官方路径优先，
        ///    兜底直接 ServerPhysicalAttachment.Attach(PlayerAttachmentCarrier)。
        ///  - 实测修复③：食材判定组件错误——IngredientPropertiesComponent 仅
        ///    切好食材（ChoppedX）才有，生食材（Tomato 等）只有
        ///    IngredientDisposalBehaviour → 生食材/地上的生食材全部漏判。
        ///    改为 IngredientDisposalBehaviour 为主（垃圾桶识别食材的官方通道，
        ///    生/切好全有）+ IPC 兜底。
        /// 2.2.1（2026-09-15）：
        ///  - **联机致命修复**：可移动火锅在网络实体扫描窗口期 Instantiate 大锅，
        ///    导致主客机实体 ID 整体错位（客机完全不能动、双方厨师原地不动、
        ///    生成物无模型，全程零报错）；单机侧同一根因表现为「其中一口锅开局
        ///    就有汤且丢不进食材」。改为由 MultiplayerController.ScanEntities 前缀
        ///    在发令时刻同步装配全部大锅 + 协程扫描期硬闸。
        ///  - 新增联机实体诊断：StartSynchronisation 前缀输出实体指纹/分段指纹/
        ///    逐锅注册自检（主客机两行一比即可判定 ID 是否对齐）。
        ///  - 反射修复：GameApi.Find 加命名空间候选 + 简单名兜底（此前
        ///    ClientSynchroniserBase/ServerSynchroniserBase/Serialisable 全部落空，
        ///    「触发区占用联机同步」的收发两端补丁从未真正装上）；容量字段读错组件
        ///    （汤面重摆从未生效）；IsLocallyControlled 反射了不存在的接口。
        /// 2.2.0（2026-09-15）：
        ///  - 传送门单向（TeleportalExitOnly）：出口门运行时把 Teleportal.m_exitPortal
        ///    清回 null（并清 Server/ClientTeleportal 已缓存的 m_exitReceivers），
        ///    走人与传送带两条发送路径同时封死、收货不受影响；
        ///    tag 载体 TeleportalExitOnly|&lt;1|0&gt;，不挂 ticker（无该 tag 的图零开销）。
        ///    依赖包过旧时 fail-open = 退化为双向，关卡本体照常加载。
        /// 2.1.0（2026-09-14）：
        ///  - 性能与日志治理：ticker 三态门控、HotPot 大锅门控 + 分帧缓存、
        ///    热路径去分配、日志分级（默认关闭诊断级）、loader 关卡扫描移出主线程；
        ///  - web 火锅烧糊修复：直驱不再在「刚熟」停止（进度到不了 1.3×预警 / 2×烧糊），
        ///    改为推进到 IsBurning 为止 + 宿主双驱动时观测让位；客户端「锅在灶台上」
        ///    标志按触发区直驱（原先恒 false 导致烧糊预警图标被 vanilla 吞掉）。

        /// 3.7.4（2026-10-10 摇杆遥控地板松杆吸附 / 再驾驶修复）：
        ///  - AnimPilotFloorDrive：松杆吸附期间不再每帧清空 m_gridTarget；静止后再
        ///    Deoccupy 区域占格（与原版格心吸附不打架）。
        ///  - 会话结束冻结时设 kinematic=true，再次进入驾驶或 AssignPlayer 时恢复
        ///    kinematic=false（修复退出后无法再推地板）。
        ///  - HealScene 补挂 Marker 时回填 JoystickPseudoRoot；导出特征表登记
        ///    AnimPilotFloorMarker → terminal。
        /// 3.7.5（2026-10-10 摇杆地板会话判定 / 松杆防顶走）：
        ///  - AllowVanillaPilotUpdate：终端占用读 ServerTerminal.m_session（对齐
        ///    JoystickMarkerLink），不再用 SessionInteractable.enabled（避免 Update
        ///    被拦、动作键无法结束会话、摇杆再也无法交互）。
        ///  - 会话内松杆静止后 kinematic=true，仅 HasDriveInput / 吸附 / 未停稳时
        ///    恢复 dynamic（同组地板上玩家碰撞不再推走整组）。
        /// 3.7.6（2026-10-10 摇杆地板松杆后平台防带跑）：
        ///  - WantsPhysicsDrive 不再用平台速度（厨师碰台面会误判未停稳而保持 dynamic）。
        ///  - Update 后缀每帧在松杆 idle 时重新 kinematic+清零速度（抵消原版 Update 被碰撞唤醒）。
        /// 3.7.7（2026-10-10 摇杆地板松杆硬锁位）：
        ///  - 松杆且格心吸附结束后跳过原版 UpdateSynchronising（仅保留摇杆输入/吸附中放行）。
        ///  - idle 时 Rigidbody.isKinematic 直写 + 组根世界坐标锚定（防碰撞/惯性在 FixedUpdate 带跑）。
        /// 3.7.8（2026-10-10 随组摇杆再交互）：
        ///  - 松杆 idle 仍跑 UpdateSynchronising（动作键可结束会话）；无会话但残留 scheme 时
        ///    AssignPlayer(null) 对账；锁位对齐 GetNearestGridPosition；回填 Terminal.pilotableObject。
        /// 3.7.9（2026-10-10 摇杆灯靠近半格亮起）：
        ///  - JoystickMarkerLink：终端可交互且本机玩家距摇杆 ≤0.5 格（0.6m）时 GreenMarker，
        ///    与驾驶占用灯一致；远离或未绑定 pilot 时熄灭。
        /// 3.8.0（2026-10-10 遥控地板摇杆邻近交互）：
        ///  - AnimPilotJoystickProximity：同 pilot 组或水平半格内视为靠近（碰撞盒中心锚点）。
        ///  - Client/ServerInteractable.CanInteract 后缀：邻格扫描失败时仍可按摇杆；
        ///    JoystickMarkerLink 与交互共用同一套判定。
        /// 3.8.1（2026-10-10 灯亮但按交互无反应）：
        ///  - ServerSessionInteractable.CanInteract（摇杆终端实际重写路径）；
        ///  - ClientAnticipateInteractionHighlight.CanHighlight + SetInteractionObjects 注入邻近摇杆。
        /// 3.8.2（2026-10-10 灯仅驾驶中亮 + 交互兜底）：
        ///  - JoystickMarkerLink：仅 m_session 占用亮绿灯（不再半格可触发亮灯）；
        ///  - 半格内强制优先摇杆 InteractionObjects；按交互键 TriggerInteractable 兜底。
        /// 3.8.3（2026-10-10 摇杆地板移动后交互占格）：
        ///  - HealScene：Design/Pilot Objects 成员补挂 AnimGridMemberSync（跳过 PilotFloor）；
        ///  - AnimGridMemberSync：child 重建或残留 StaticGridLocation 时重试换装。
        /// 3.8.4（2026-10-10 摇杆地板半格停位高亮）：
        ///  - AnimPilotMemberInteract：组内工作台/道具 1.35m 水平距离兜底高亮与交互；
        ///  - AnimGridMemberSync：在成员 wrapper 整棵子树换装（非仅 pseudo child）。
        /// 3.8.5（2026-10-10 摇杆平台脏杯高亮 NRE）：
        ///  - 移除误补丁 ClientAnticipateInteractionHighlight.CanHighlight(Material)；
        ///  - 注入仅扫 pilot 直系成员、排除堆叠子盘；取放已占用时不注入 interactable。
        /// 3.8.6（2026-10-10 摇杆组工作台抢判）：
        ///  - AnimPilotMemberInteract：兜底半径 1.35m→半格+0.08m；CanInteract 仅最近一台；
        ///  - 水平距离锚点用 pilot 直系成员根 XZ，避免大碰撞盒拽偏邻台。
        /// 3.8.7（2026-10-10 AnimGridMemberSync 换装顺序）：
        ///  - Static→Dynamic：先挂 Dynamic 再删 Static（RequireComponent 传送带台面）。
        /// 3.8.8（2026-10-11 真机 Harmony 摇杆交互补丁）：
        ///  - SetInteractionObjects 后缀参数名对齐宿主 _newInteractionObjects（HarmonyX
        ///    真机按名绑定，错名整组 IL 编译失败→BepInEx 严重错误/进图异常）。
        ///  - 驾驶核心补丁与邻近交互补丁分拆安装，交互失败不拖垮 AnimPilotFloorDrive。
        /// 3.8.9（2026-10-12 commonW4 Web 储物背包）：
        ///  - WebBackpackStorage：多格 IngredientContainer + 背上取出/放入 + 宫格 Hover；
        ///  - stub_levels 特征 webbackpack；编辑器 WebBackpack stub DTO 写回。
        /// 3.8.10（2026-10-12 Web 背包重写：修取出/放入/宫格三链路 + 全菜谱初填）：
        ///  - 修复初填节点 ctor 类型不匹配（GetOrderComposition 返回 Assembled 族，
        ///    包 IngredientOrderNode 必抛 ArgumentException 炸死协程 → 全链失效）；
        ///  - 修复 GameApi.GetPrefabForNodeMethod 引用后置字段（初始化期恒 null，
        ///    节点→prefab/宫格图标永久失效）；
        ///  - 取出生成物改为双端 RegisterSpawnablePrefab 注册（原版按 ID 查表，
        ///    未注册 = 负下标异常/客户端无法生成；RandomCrate 同款范式）；
        ///  - 交互模型定稿：空手 = 队首食材型节点（FIFO）；空盘族放置 = 队首节点
        ///    注入盘中（组合型唯一取出方式）；食材放置 = 节点入包销毁手中物；
        ///    盘装内容放置 = 节点搬入包、盘清空留手（盘永不进包）；
        ///  - CanHandlePlacement 对 Web 背包条件覆盖（原版 CanCombine 不认空盘）；
        ///    Placement 同步器按 worn 启停（地面背包不吞放置交互）；
        ///  - 随机取出模式移除（恒 FIFO，tag mode 段仅格式兼容）；
        ///  - 初填支持全菜谱/中间产物（PseudoPrefabSORecipe→bundle 订单节点资产）
        ///    与自定义菜谱（m_initialFillCustomUids→关卡匹配表按 uID 解析）；
        ///  - 宫格 1×1/2×1/2×2/3×3/4×4 + 构建失败闩 + 图标回落链（lookup→
        ///    m_iconSprite→组合首子节点）；FindForDispenser 父级兜底堵注册时序洞；
        ///  - 全链路常开诊断日志（自愈/子背包时序/容器装配/初填逐条/同步等待/
        ///    就绪状态矩阵/worn 与门控迁移/取出与放置每个分支的决策原因）——
        ///    交互期日志为按键频率（非热路径），排障以 [WebBackpack] 前缀逐跳定位；
        ///  - 修复 InspectCarriedItem/CarryItem/GetOrderComposition 等重载方法的
        ///    GetMethod 二义性（老 Mono AmbiguousMatchException 曾吞掉全部放置交互）；
        ///    放置 referral 硬接线（worn 时 referree=背包容器）；初填失败限时重试
        ///    + 编辑器优先走宿主 LoadAsset（瞬态 null 自愈）；空格深色底板显式可见。
        /// 3.8.11（2026-10-12 放置/取出 parenting 根治 + 锅具同款宫格）：
        ///  - WebBackpackChild 标记组件：背包被背起后 child 挂到厨师身上，
        ///    GetComponentInParent 寻址全部落空（17:46 场次放置探针实证）——
        ///    Harmony 前缀/FindForDispenser 改标记优先（层级无关）；
        ///  - 宫格弃用自拼 hover 网格：实例化 bundle47 原版 IngredientsContentsUI
        ///    （IngredientContentsUIContainer 自带网格/图标/空位精灵），
        ///    SetMinimumSize(容量)+SetOrder(CompositeAssembledNode 聚合)驱动；
        ///    commonW4 新增 web_backpack_contents_ui SO（写回自动赋值 m_contentsUiSO）；
        ///  - 删除 referral 硬接线（直连组件路由已证实可用，反射目标未命中属死代码）。
        /// 3.8.12（2026-10-12 放入语义三修复）：
        ///  - 「空X接」收紧为盘族（Plate 组件：盘/杯/马克杯/托盘）——面包皮等非盘族
        ///    容器不再接收注入（曾把背包生肉注入汉堡皮拼成汉堡）；
        ///  - workable 源生物（整蔬菜/面包皮/冰块）可入包：源节点取自其加工形态
        ///    节点的 composition[0]（vanilla 切分模型），取出经食材箱源链还原整品；
        ///  - 空的非盘族 workable 容器（面包皮）可存本体（源节点+销毁手中物）；
        ///    盘族有内容=搬入内容盘留手；任何路径均离散格位、绝不合并。
        /// 3.8.13（2026-10-12 四问题修复：组装基底/切碎保持/熟食盘接/宫格布局）：
        ///  - 真盘族=有 Plate 且非组装基底（无 WorkableItem、无 BurgerBun/Burrito/
        ///    Hotdog/Pizza 组装装饰）——面包皮/卷饼皮带 Plate 也不再误接收注入；
        ///  - 组装基底：有内容=整体一格入包（皮的 IOrderDefinition 完整组装复合
        ///    节点，汉堡皮+夹心不再拆散）；空=存基底本体；
        ///  - 切碎保持：原版整料与切碎共享节点 uid（全库无独立 chopped 节点资产），
        ///    放入时按携带物 WorkableItem 形态匹配食材箱链成员注册——切好的进、
        ///    切好的出；存储统一原始节点（去 Simpilfy，切碎/熟制包裹层原样保全）；
        ///  - 盘接分支补齐逐步日志（背包空/队首 null/容器拒收/注入成功）；
        ///  - 宫格布局重设：FixedColumnCount=GridCols(cap)（16 格 4×4 而非 2×8），
        ///    格宽按容量收窄（0.5/0.36/0.30），follow 上移至 1.55~1.7、9/16 格
        ///    整体缩放 0.9/0.85——不再偏下遮挡玩家。
        /// 3.8.14（2026-10-13 空基底堵队 + 落水清空）：
        ///  - 空组装基底（面包皮/切好的面包）存「复合内唯一平节点」——此前存了
        ///    组装复合节点，组合型堵住 FIFO 队首导致空手全部取不出（实测）；
        ///  - KillPlane 落水清空：穿戴者落水或背包本体坠水 → 服务端清空全部
        ///    存货（webbackpack 关卡一并安装 KillPlane 前缀组）。
        /// 3.8.15（2026-10-13 容量集扩展）：
        ///  - 支持容量 1/2/4/6/9/12/16/20/25/36（宫格列多行少：6=3×2、12=4×3、
        ///    20=5×4）；格宽按容量收窄（0.5→0.24），大容量宫格上移+整体缩小；
        ///    前端容量下拉/画布小宫格/写回校验/编辑器规范同步扩展。
        /// 3.8.16（2026-10-13 >31 内容截断修复）：
        ///  - 原版 IngredientContainerMessage 子数仅 5 位（上限 31）——容量 36 的
        ///    背包放入第 32 个物品时线上被截断为 0（实测宫格瞬间清零）；
        ///  - Harmony 替换 Serialise/Deserialise：≤31 与原版逐位等价；>31 用保留
        ///    节点类型 15（工厂 0-5 已占用）+ 6 位计数（上限 63）扩展编码。
        /// 3.8.17（2026-10-13 回退决策：容量上限 30，不做协议扩展）：
        ///  - 撤销 3.8.16 的 IngredientContainerMessage Serialise/Deserialise
        ///    替换（复杂度回退，原版同步协议保持原样）；
        ///  - 容量集改为 1/2/4/6/9/12/16/20/25/30（30=6×5）；历史 36 场景在
        ///    运行时/写回/前端全链路自动归一为 30。
        /// 3.8.18（2026-10-13 移除成品菜放入）：
        ///  - 游戏内不再支持放入成品菜：拼好的组装品（面包皮+夹心）与盘装/
        ///    容器内容一律拒绝（TryTransferContainerIntoBackpack 与整体组装
        ///    分支移除）；保留：空盘接取出、空组装基底存本体、食材/源生物放入、
        ///    编辑器初填（含成品菜，取出走空盘接）。
        /// 3.9.0（2026-10-13 RandomTrigger 随机触发器）：
        ///  - 新增 CustomStub.RandomTrigger：一个信号输入（公有 OnTrigger(string)
        ///    SendMessage 生态 / 源按钮 TriggerDisableScript 下降沿轮询直连）→
        ///    按权重从候选列表随机选一个发出；最终信号 = 信号名 + 分隔符（默认
        ///    ":"，可配）+ 可编程参数值（固定 / 候选 / 随机字符串 / 随机 N 位数值 /
        ///    数值区间[含小数位]），参数未配置只发信号名；
        ///  - 冷却限流（N 秒内只触发一次，0=不限）；服务端权威掷骰
        ///    （IsServerMachine 单点发出，防两端分叉）；目标物体 SendMessage 派发
        ///    （空=自身，自环防抖）；目标 GameObject.Find + 缓存 + 假 null 修剪；
        ///  - 每候选独立目标（物品伪根 / 动画组根 / 其他 RandomTrigger 节点，
        ///    BLRelay 事件组同款名字寻址）；不计入 ticker 激活票数（自治组件，
        ///    TeleportalExitOnly 同款豁免）；EntryPoint v17，自愈 tag
        ///    RandomTrigger|I:..|C:..|S:..|N:..|E:..（%,;>|/@ 转 %XX，
        ///    RandomTriggerBakery 编码对称）；Loader 同步 3.9.0。
        /// 3.9.1（2026-10-13 UtensilTiming 进图卡顿修复）：
        ///  - 扫描早停：全配置已应用/放弃即停；连续无进展 tick 放弃未匹配项；
        ///    强制上限降至 ~8s；缓存 UtensilTiming| 包装与 handler 列表，避免每 tick
        ///    全场景 FindObjectsOfType / 重复 FindAll；
        ///  - cook/mix-only 应用后挂 UtensilTiming 标记组件，幂等跳过后续 tick；
        ///  - 按名兜底剥离实例名 (Clone) 后缀；失败日志附带 prefab 名与 handler 计数；
        ///    HealScene 末尾同步一次 Tick；Loader 同步 3.9.1。
        /// 3.10.0（2026-10-14 按钮控速 + 按钮生成器批次）：
        ///  - ButtonSpeedControl（SpeedCtl|E:&lt;1|0&gt;|T:&lt;目标路径&gt;|N:&lt;按钮路径,..&gt;
        ///    |L:&lt;s1,s2,..&gt;）：按钮按压循环切换步带 Travelator.m_speed
        ///    （+SetSpeedOnMaterial 材质同步）/传送带 ConveyorStation.m_conveySpeed
        ///    档位；TriggerDisableScript enabled 下降沿轮询（CoaxialButtonGroup
        ///    同款），各端本地应用；
        ///  - ButtonSpawnerBatch（SpawnerBatch|E:&lt;1|0&gt;|T:&lt;目标路径&gt;|N:&lt;按钮路径,..&gt;
        ///    |C:&lt;count&gt;|I:&lt;interval&gt;|D:&lt;cooldown&gt;）：食材生成器按钮批次送出——
        ///    第 1 个立刻、之后每间隔 1 个直到 N（落点站忙补位）；批次完成后冷却
        ///    N 秒（按压无效）；仅服务端驱动，生成复用原生 ServerTriggerAttachedSpawn
        ///    联网链；运行时绑定后停用原生 TriggerTimer 自动循环（防双送）；
        ///  - 分 P 修复三件套（2026-10-15，docs/10 v3.4/D15~D17）：
        ///  ① 新增 PartSlotRespawnGuard（Assets/WebCustomStubRuntime/PartSlot/）：
        ///    ServerUtensilRespawnBehaviour 及 4 派生类（锅/净盘/脏盘叠/灭火器）
        ///    CanRespawnOnStation Harmony 后缀——站台位于非在场 slot（祖先
        ///    Design/PartSlots/<pid> 且 slot 根 Animator 非 Idle_Active）时改判
        ///    false。修复分 P 旧 P 下沉后道具滞留沉没/停泊站台（玩家视角
        ///    「不销毁、也不出现在下一 P」）；重生重试循环等新 P 就位后落到其
        ///    空闲站台 = 跨 P 携带（docs/10 D16）。HealScene 扫到 Design/PartSlots
        ///    按需安装；普通关卡首行放行零影响；
        ///  ② 配套站台优先：PartSlotRespawnGuard 增加 GetStationRespawnPriority
        ///    后缀（基类 + 锅具派生声明）——净盘对非归还台（无
        ///    ServerPlateReturnStation）加 +1000 距离惩罚，优先落归还台、无空闲
        ///    归还台才落普通工作台。锅具/搅拌碗（含搅拌杯）原版已有配套硬约束
        ///    （灶台类型匹配）+ 非灶台 +1000 惩罚，脏盘叠原版已限定只扫归还台，
        ///    无需干预；灭火器等通用道具保持最近可用站台（base/当前 P 工作台）。
        ///    仅分 P 场景生效（s_sceneHasSlots 门控），普关零影响；
        ///  ③ 编辑器侧配套（LayoutEditor）：Kill_<from> 时机 = 下一 P 上浮完成
        ///    （Stable_<to> + cleanupDelay）；修复 Kill 计时器 cleanupDelay=0
        ///    死定时器（ServerTriggerTimer 仅 m_timer>0 倒计时——Kill 链自始
        ///    未触发的真根因），Max(0.1f,·) 兜底，BurnerScheduleBakery 首波
        ///    startDelay=0 同款隐患同修；废除 Design 根全场大扫除
        ///    （KillLoose_*、D14 作废——base 层任何东西不清扫，陈旧组件由
        ///    DestroyTimeline 前缀清理自愈）；base 物件/地板改挂棘轮解锁
        ///    （错拖进 slot 的 base 内容写回自动搬回，修复「base 地板随旧 P
        ///    下沉」）。
        ///  - Loader 同步 3.10.0。
        /// 3.10.1（2026-10-15 commonW4 致命鬼魂）：
        ///  - WebKillerGhost + web_killer_ghost_01：DLC07_NPCs_Ghost 视觉 +
        ///    RespawnCollider Hit 触碰击杀；动画组负责位移；stub_levels webkillerghost。
        /// 3.10.2（2026-10-15 致命鬼魂多样式）：
        ///  - Web 右键切换 npc_ghost / npc_keep / ghost_chef（commonW4 视觉代理 SO）。
        public const string Value = "3.11.0";
    }
}
