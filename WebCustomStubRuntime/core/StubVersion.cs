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
        public const string Value = "2.2.1";
    }
}
