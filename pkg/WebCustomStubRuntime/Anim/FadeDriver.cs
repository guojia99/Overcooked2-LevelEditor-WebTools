using System.Collections.Generic;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 动画组成员「淡入淡出（隐身/显型）」驱动器。
    ///
    /// 背景：AnimationClip 无法直接动画运行时才实例化的 bundle 模型 renderer
    /// 材质（真模型是 PseudoPrefabManager 按 PseudoPrefabSO 实例化的 child）。
    /// 烘焙的 fade 事件 clip 改为动画本组件的公开字段 alpha
    /// （EditorCurveBinding FloatCurve(path, "FadeDriver", "alpha")），本组件
    /// 把 alpha 写入成员子树全部 Renderer 的材质颜色 alpha：
    ///  - 惰性实例化：alpha 恒 1 时零开销（不触碰材质）；首次淡出才 Scan 并
    ///    renderer.materials 实例化（每 renderer 一次，之后不反复切换）；
    ///  - 周期重扫（0.5s，仅淡出状态）：捕获运行时后实例化的 bundle child；
    ///  - 渲染队列：半透明阶段切 Transparent(3000)，恢复 1 时还原原队列；
    ///    支持 alpha blend 的 shader 全程渐变；opaque shader 无中间渐变，但
    ///    alpha ≤ 0.01 时直接关 renderer（彻底消失）、恢复时打开——两端恒正确；
    ///  - 只动 renderer 可见性：网格占位（ServerLogic 网格制）与碰撞体不受影响，
    ///    桌台类「隐身后仍占格、不可交互（模型不可见但碰撞仍在）」。
    ///
    /// 挂载：AnimGroupBakery 对含 fade 事件的组反射挂载到每个成员 wrapper
    /// （同 AnimGridMemberSync 模式；类型缺失仅告警跳过——编辑器/旧真机包
    /// 安全降级为无淡出）。
    /// </summary>
    public class FadeDriver : MonoBehaviour
    {
        /// <summary>当前透明度（0=全透明隐身，1=不透明）；烘焙的 clip 动画此字段。</summary>
        public float alpha = 1f;

        private class MatState
        {
            public Renderer renderer;
            public Material[] mats;
            public float[] baseAlpha;
            public int[] baseQueue;
        }

        private readonly List<MatState> m_states = new List<MatState>();
        private float m_lastAlpha = -1f;
        private float m_nextScan;

        private void Update()
        {
            bool fading = alpha < 0.999f;
            // 淡出状态周期重扫：bundle child 运行时实例化后也要被收编。
            if (fading && Time.unscaledTime >= m_nextScan)
            {
                m_nextScan = Time.unscaledTime + 0.5f;
                Scan();
            }
            if (Mathf.Abs(m_lastAlpha - alpha) < 0.001f)
                return;
            m_lastAlpha = alpha;
            if (fading && m_states.Count == 0)
                Scan();
            Apply();
        }

        private void Scan()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int ri = 0; ri < renderers.Length; ri++)
            {
                var r = renderers[ri];
                if (r == null || r is ParticleSystemRenderer)
                    continue; // 粒子自带透明动画，不接管
                bool exists = false;
                for (int si = 0; si < m_states.Count; si++)
                {
                    if (m_states[si].renderer == r) { exists = true; break; }
                }
                if (exists) continue;
                var mats = r.materials; // 实例化（一次性）
                var st = new MatState
                {
                    renderer = r,
                    mats = mats,
                    baseAlpha = new float[mats.Length],
                    baseQueue = new int[mats.Length]
                };
                for (int mi = 0; mi < mats.Length; mi++)
                {
                    st.baseAlpha[mi] = mats[mi] != null && mats[mi].HasProperty("_Color")
                        ? mats[mi].color.a
                        : 1f;
                    st.baseQueue[mi] = mats[mi] != null ? mats[mi].renderQueue : 2000;
                }
                m_states.Add(st);
            }
            Apply();
        }

        private void Apply()
        {
            float a = Mathf.Clamp01(alpha);
            for (int i = 0; i < m_states.Count; i++)
            {
                var st = m_states[i];
                if (st.renderer == null) continue;
                // alpha≈0 彻底隐藏；恢复即打开（opaque shader 的可靠两端语义）。
                st.renderer.enabled = a > 0.01f;
                for (int mi = 0; mi < st.mats.Length; mi++)
                {
                    var m = st.mats[mi];
                    if (m == null) continue;
                    if (m.HasProperty("_Color"))
                    {
                        var c = m.color;
                        c.a = st.baseAlpha[mi] * a;
                        m.color = c;
                    }
                    m.renderQueue = a < 0.999f ? 3000 : st.baseQueue[mi];
                }
            }
        }
    }
}
