using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 锅具时间配置载体·编辑期组件（权威通道）。
    ///
    /// 背景：宿主（编辑器 PseudoPrefab / 游戏侧 OC2DIYLevel）只认识
    /// PseudoPrefabCookingUtensilStub 的既有字段——时间参数（cookTime/burnTime/
    /// mixTime/overMixTime，0 = 原版默认）不能进 LevelEditorStub（禁止改动），
    /// 沿用 TimedCookingSwitch / RandomCrate 的双通道模式：
    ///  - 权威通道：本组件（写回时由 LayoutEditorStubIO 反射烘焙到伪 prefab 包装上，
    ///    随场景序列化，程序集 = Stub_&lt;set&gt;）；
    ///  - 载体通道：SpecificPseudoPrefabTag.prefabTag = "UtensilTiming|&lt;cook&gt;,&lt;burn&gt;,&lt;mix&gt;,&lt;over&gt;"
    ///    （invariant 浮点；程序集缺失/组件丢失时由 loader / EntryPoint 场景自愈还原；
    ///    前缀表三处同步：EntryPoint.HealObject、OC2LevelRuntimeLoader、
    ///    LayoutEditorSetExporter.CustomStubTagPrefixes）。
    ///
    /// 运行时由 UtensilTiming ticker 读取（沿锅具实例祖先链找到本组件）：
    ///  - cook/mix > 0 → 直接写 CookingHandler.m_cookingtime / MixingHandler.m_mixingTime；
    ///  - burn/overMix > 0 → 锅具上挂 UtensilTiming（Harmony 前缀接管阈值判定）。
    /// </summary>
    public class UtensilTimingConfig : MonoBehaviour
    {
        /// <summary>煮熟秒数（0 = 原版默认，prefab 自带）。</summary>
        public float m_cookTime;

        /// <summary>煮糊秒数（0 = 默认 2× 煮熟）。</summary>
        public float m_burnTime;

        /// <summary>混合完成秒数（0 = 原版默认）。</summary>
        public float m_mixTime;

        /// <summary>过度混合秒数（0 = 默认 2× 混合）。</summary>
        public float m_overMixTime;

        /// <summary>tag 载体前缀（值格式：cook,burn,mix,over，invariant 浮点）。</summary>
        public const string TagPrefix = "UtensilTiming|";

        /// <summary>是否配置了任一时间参数。</summary>
        public bool HasAnyValue
        {
            get { return m_cookTime > 0f || m_burnTime > 0f || m_mixTime > 0f || m_overMixTime > 0f; }
        }
    }
}
