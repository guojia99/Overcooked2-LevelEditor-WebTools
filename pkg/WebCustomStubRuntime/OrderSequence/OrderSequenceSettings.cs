using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 订单顺序配置组件（关卡级，烘焙在场景 PseudoPrefabManager 物体上）。
    ///
    /// 语义（4.0.0.18（原 4.7.0））：
    ///  - 整关出单被划分为若干「组」序列；组内两种模式：
    ///    ordered（m_modes=0）：按 m_recipeUids 列表顺序逐单出，允许重复；
    ///    random（m_modes=1）：原版防重复加权随机（weight = max((served+2)/N − cnt, 0)）
    ///      只在组内抽出的「批」上运行，出满 N 单（N = m_groupBatches，0 = 整池即
    ///      组内全部去重菜谱数）组完成。批的抽取：组内菜谱去重池 ≥ N 无放回、
    ///      &lt; N 有放回补足到 N 条（10 抽 15 = 15 条含重复）。
    ///  - 组完成后进下一组；全序列完成后按 m_endBehavior：
    ///    loop = 回首组重置；pingpong = 钟摆倒放（A B C D C B A B C D…，端点组
    ///    跨周期重复、相邻不重复；ordered 组重入指针倒放，random 组重抽一批）；
    ///    stop = 不再出单（GetNextRecipe 返回空数组）。
    ///
    /// 载体双通道：组件字段（权威，烘焙方 LayoutEditorOrderSequenceBake 写入）+
    /// OrderSeq| tag（全量标量编码，EntryPoint.HealObject 据此自愈重建组件——
    /// 本组件数据全是 int/string，无 SO 引用，tag 可无损往返）。
    /// 配置源：data/&lt;level&gt;/order_seq~/order_seq.json（前端编辑）→ 写回/导出时烘焙。
    ///
    /// 运行时 OrderSequenceDirector 按 uID 在关卡 RecipeList（RoundData.m_recipes）
    /// 中解析 Entry 接管出单（Harmony prefix RoundData.GetNextRecipe）；查不到的
    /// uID（菜谱未选/debugRecipeCount 截断）跳过并告警一次。
    /// </summary>
    public class OrderSequenceSettings : MonoBehaviour
    {
        /// <summary>场景自愈/特征扫描载体前缀。
        /// 格式：OrderSeq|e=loop|m=0,1|s=3,2|b=0,6|u=101,202,103,301,302</summary>
        public const string TagPrefix = "OrderSeq|";

        private const string Version = "v1(" + StubVersion.Value + ")";

        /// <summary>结束行为：loop（循环）/ pingpong（钟摆倒序）/ stop（停止出单）。</summary>
        [SerializeField] public string m_endBehavior = "loop";

        /// <summary>每组模式：0 = ordered（按序），1 = random（原版随机批）。长度 = 组数。</summary>
        [SerializeField] public int[] m_groupModes;

        /// <summary>每组菜谱条数（m_recipeUids 的切分）。长度 = 组数。</summary>
        [SerializeField] public int[] m_groupSizes;

        /// <summary>random 组抽菜数 N（0 = 整池）。长度 = 组数（ordered 组忽略）。</summary>
        [SerializeField] public int[] m_groupBatches;

        /// <summary>扁平 uID 列表（按 m_groupSizes 切分；ordered 组保序可重复，
        /// random 组为候选池、重复条目等价去重）。</summary>
        [SerializeField] public int[] m_recipeUids;
    }
}
