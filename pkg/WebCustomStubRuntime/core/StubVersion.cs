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
        /// Loader 的 PluginVersion 必须同步为同值。</summary>
        public const string Value = "2.4.0";
    }
}
