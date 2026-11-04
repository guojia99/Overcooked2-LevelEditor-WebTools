using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using LevelEditorStub;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CustomStub
{
    /// <summary>
    /// 开关 / 压力开关 / 莲花压力地板的调色（2026-10-30）。
    ///
    /// 数据通道：编辑器写回在 Design/Switch Tint Logic/Tint_&lt;hash&gt; 上烘焙本组件
    /// + SwitchTint|T:&lt;路径&gt;|I:&lt;未按下色&gt;|P:&lt;按下色&gt; tag；真机 Missing Script
    /// 由 EntryPoint.HealObject 按 tag 补挂并还原字段。
    ///
    /// 实现要点：
    ///  - 不改材质、不建材质实例：统一用 MaterialPropertyBlock 做乘法染色。
    ///    OC2 gloss 家族 albedo = tex2D(_DiffuseMap) × _MaskColor，因此只写
    ///    _MaskColor；Standard 等 shader 写 _Color。原贴图保持不变，避免纯色
    ///    覆盖丢掉明暗细节后显得过亮；也不污染场景/泄漏材质。
    ///  - 只改颜色：金属度/粗糙度/边缘光/高光全部沿用原材质，保证调色前后
    ///    与原版按钮的亮度和光泽一致（不压高光）。
    ///  - 编辑器模式（ExecuteInEditMode）：按当前状态刷 MPB，Scene 视图直接可见；
    ///    MPB 不参与序列化，保存场景不会写入内嵌材质。
    ///  - Play/真机：低频轮询视觉状态并切换 MPB 颜色：
    ///      普通开关 = m_buttonBit 是否 m_inactiveMaterial；
    ///      压力开关 = m_buttonBit 是否 m_occupiedMaterial；
    ///      莲花地板 = Animator bool "ChefOnPlatfrom"（无则 Client/ServerTriggerZone 占用）。
    ///  - 目标路径用自写 Transform 遍历解析，支持分 P 的 inactive 物体（GameObject.Find 不找 inactive）。
    /// </summary>
    [ExecuteInEditMode]
    public class SwitchTint : MonoBehaviour
    {
        public const string TagPrefix = "SwitchTint|";

        /** 目标开关/压力地板伪根的层级路径（由编辑器写回/EntryPoint 自愈写入）。 */
        public string m_targetPath;
        /** 未按下 / 未受压颜色（#rrggbb；空 = 该状态不覆盖颜色）。 */
        public string m_idleColor;
        /** 按下 / 受压颜色（#rrggbb；空 = 该状态不覆盖颜色）。 */
        public string m_pressedColor;

        private class RendererTint
        {
            public Renderer renderer;
        }

        private GameObject m_target;
        private Component m_cosmetic;
        private FieldInfo m_buttonBitField;
        private FieldInfo m_pressedMaterialField;
        private readonly List<RendererTint> m_renderers = new List<RendererTint>();
        private MaterialPropertyBlock m_mpb;
        private bool m_isLotus;
        private bool m_lotusProbed;
        private bool m_lotusAnimatorProbed;
        private Animator m_lotusAnimator;
        private int m_lotusBoolHash;
        private float m_nextEditTick;
        private bool m_loggedApply;
        private bool m_loggedNoColor;

        private void OnEnable()
        {
            m_target = null;
            m_cosmetic = null;
            m_renderers.Clear();
            m_isLotus = false;
            m_lotusProbed = false;
            m_lotusAnimatorProbed = false;
            m_lotusAnimator = null;
            m_nextEditTick = 0f;
            if (Application.isPlaying)
                StartCoroutine(SafeRunner(RunPlay()));
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            ClearTint();
        }

        private void OnDestroy()
        {
            ClearTint();
        }

        private void Update()
        {
            if (Application.isPlaying)
                return;
            EditTick(false);
        }

        /// <summary>编辑器写回/自愈写完字段后调用：立即重新解析并刷新预览。</summary>
        public void Refresh()
        {
            ClearTint();
            m_target = null;
            m_cosmetic = null;
            m_renderers.Clear();
            m_lotusProbed = false;
            m_lotusAnimatorProbed = false;
            m_lotusAnimator = null;
            if (Application.isPlaying)
                return;
            EditTick(true);
        }

        private void EditTick(bool force)
        {
            if (!force && Time.realtimeSinceStartup < m_nextEditTick)
                return;
            m_nextEditTick = Time.realtimeSinceStartup + 0.5f;
            if (!EnsureReady())
                return;
            // 每 0.5s 无条件重刷：其他组件可能也在写 MaterialPropertyBlock，
            // 周期重刷保证我们的颜色是最后写入者。
            ApplyCurrentState();
        }

        private IEnumerator SafeRunner(IEnumerator inner)
        {
            while (true)
            {
                object current;
                bool hasNext;
                try
                {
                    hasNext = inner.MoveNext();
                    current = hasNext ? inner.Current : null;
                }
                catch (Exception ex)
                {
                    StubLog.LogWarn("[SwitchTint] 协程异常退出: " + name + "\n" + ex);
                    yield break;
                }
                if (!hasNext)
                    yield break;
                yield return current;
            }
        }

        private IEnumerator RunPlay()
        {
            float waited = 0f;
            while (true)
            {
                if (EnsureReady())
                    break;
                waited += 0.25f;
                if (waited > 30f)
                {
                    StubLog.LogWarn("[SwitchTint] 等待目标/渲染器超时（30s）: " + name
                        + " path=" + (m_targetPath ?? ""));
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.25f);
            }

            ApplyCurrentState();
            while (true)
            {
                yield return new WaitForSecondsRealtime(0.1f);
                if (!EnsureReady())
                    continue;
                // 每 0.1s 重刷（对抗其他 MPB 写入者；渲染器/材质变化时自然重算）。
                ApplyCurrentState();
            }
        }

        private bool EnsureReady()
        {
            if (m_target == null)
            {
                ResolveTarget();
                if (m_target != null)
                {
                    m_lotusProbed = false;
                    m_lotusAnimator = null;
                    m_lotusAnimatorProbed = false;
                }
            }
            if (m_target == null)
                return false;

            if (!m_lotusProbed)
            {
                m_isLotus = DetectLotus(m_target);
                m_lotusProbed = true;
            }

            if (!m_isLotus && m_cosmetic == null)
                ResolveCosmetic();

            var needRenderers = m_renderers.Count == 0;
            if (!needRenderers)
            {
                var first = m_renderers[0];
                if (first == null || first.renderer == null)
                    needRenderers = true;
            }
            if (needRenderers)
                ResolveRenderers();

            if (m_renderers.Count == 0)
                return false;

            // 普通开关/压力开关必须等 cosmetic 就绪；莲花地板没有该组件。
            if (!m_isLotus && m_cosmetic == null)
                return false;
            return true;
        }

        private void ResolveTarget()
        {
            if (string.IsNullOrEmpty(m_targetPath))
            {
                m_target = gameObject;
                return;
            }
            m_target = FindByPath(m_targetPath);
        }

        private void ResolveCosmetic()
        {
            m_cosmetic = null;
            m_buttonBitField = null;
            m_pressedMaterialField = null;
            if (m_target == null)
                return;

            var components = m_target.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                var c = components[i];
                if (c == null)
                    continue;
                var typeName = c.GetType().Name;
                if (typeName == "SwitchCosmeticDecisions")
                {
                    m_cosmetic = c;
                    var t = c.GetType();
                    m_buttonBitField = Field(t, "m_buttonBit");
                    m_pressedMaterialField = Field(t, "m_inactiveMaterial");
                    return;
                }
                if (typeName == "PressureSwitchCosmeticDecisions")
                {
                    m_cosmetic = c;
                    var t = c.GetType();
                    m_buttonBitField = Field(t, "m_buttonBit");
                    m_pressedMaterialField = Field(t, "m_occupiedMaterial");
                    return;
                }
            }
        }

        private void ResolveRenderers()
        {
            m_renderers.Clear();
            // child 重建后 Animator/材质都可能是新实例：允许重新探测。
            m_lotusAnimator = null;
            m_lotusAnimatorProbed = false;
            if (m_target == null)
                return;

            var all = m_target.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var r = all[i];
                if (r == null)
                    continue;
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer))
                    continue; // 跳过 ParticleSystemRenderer 等
                if (IsSplashRenderer(r))
                    continue; // 莲花水花特效不参与地板染色
                var rt = new RendererTint();
                rt.renderer = r;
                m_renderers.Add(rt);
            }
        }

        private bool IsSplashRenderer(Renderer r)
        {
            if (r == null)
                return false;
            var stop = m_target != null ? m_target.transform : null;
            var t = r.transform;
            while (t != null && t != stop)
            {
                if (t.name != null
                    && t.name.IndexOf("splash", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                t = t.parent;
            }
            return false;
        }

        private static bool ApplyColorTint(Material mat, Color tint,
            MaterialPropertyBlock mpb)
        {
            if (mat == null)
                return false;
            // OC2 gloss 家族的 albedo = tex2D(_DiffuseMap) × _MaskColor：
            // 只替换 _MaskColor 即可保留原贴图、法线、金属度、粗糙度、边缘光和高光；
            // 不能把 _DiffuseMap 换成纯色贴图，否则会丢掉原贴图明暗并显得过亮。
            if (mat.HasProperty("_MaskColor"))
            {
                mpb.SetColor("_MaskColor", tint);
                return true;
            }
            // Standard / 其他常见主色属性（_MainTex 保持不变）。
            if (mat.HasProperty("_Color"))
            {
                mpb.SetColor("_Color", tint);
                return true;
            }
            if (mat.HasProperty("_BaseColor"))
            {
                mpb.SetColor("_BaseColor", tint);
                return true;
            }
            if (mat.HasProperty("_TintColor"))
            {
                mpb.SetColor("_TintColor", tint);
                return true;
            }
            return false;
        }

        private void ApplyCurrentState()
        {
            if (m_target == null)
                return;
            var pressed = ReadPressed();
            var colorText = pressed ? m_pressedColor : m_idleColor;
            var tint = ParseColor(colorText);
            if (!tint.HasValue)
            {
                ClearTint();
                RepaintSceneViews();
                return;
            }

            if (m_mpb == null)
                m_mpb = new MaterialPropertyBlock();

            var wanted = tint.Value;
            int applied = 0;
            for (int i = 0; i < m_renderers.Count; i++)
            {
                var rt = m_renderers[i];
                if (rt == null || rt.renderer == null)
                    continue;
                var mat = FirstMaterial(rt.renderer);
                if (mat == null)
                    continue;

                m_mpb.Clear();
                // 乘法染色：OC2 gloss 家族写 _MaskColor（albedo = _DiffuseMap × _MaskColor），
                // Standard 等写 _Color。原贴图、法线、金属度、粗糙度、边缘光和高光
                // 一律保持不变，确保调色前后亮度与光泽和原版按钮一致。
                if (ApplyColorTint(mat, wanted, m_mpb))
                {
                    rt.renderer.SetPropertyBlock(m_mpb);
                    applied++;
                }
            }

            if (!m_loggedApply)
            {
                m_loggedApply = true;
                var mats = new System.Text.StringBuilder();
                for (int i = 0; i < m_renderers.Count; i++)
                {
                    var rt = m_renderers[i];
                    var m = rt != null && rt.renderer != null ? FirstMaterial(rt.renderer) : null;
                    mats.Append(rt != null && rt.renderer != null ? rt.renderer.name : "?")
                        .Append("=").Append(m != null ? m.name : "null")
                        .Append("[").Append(m != null && m.shader != null ? m.shader.name : "?")
                        .Append("] ");
                }
                StubLog.Log("[SwitchTint] " + name + " 应用调色: target=" + m_targetPath
                    + " pressed=" + pressed + " color=" + colorText
                    + " renderers=" + m_renderers.Count + " applied=" + applied
                    + " mats=" + mats.ToString());
            }
            if (applied == 0 && !m_loggedNoColor)
            {
                m_loggedNoColor = true;
                StubLog.LogWarn("[SwitchTint] " + name + " 目标没有材质支持颜色属性"
                    + "（_Color/_BaseColor/_MaskColor 等）: target=" + m_targetPath);
            }

            RepaintSceneViews();
        }

        private bool ReadPressed()
        {
            if (m_isLotus)
                return ReadLotusPressed();

            if (m_cosmetic == null || m_buttonBitField == null)
                return false;
            var bit = m_buttonBitField.GetValue(m_cosmetic) as Renderer;
            if (bit == null)
                return false;
            var pressedMat = m_pressedMaterialField != null
                ? m_pressedMaterialField.GetValue(m_cosmetic) as Material : null;
            if (pressedMat != null && bit.sharedMaterial == pressedMat)
                return true;
            return false;
        }

        private bool ReadLotusPressed()
        {
            if (m_lotusAnimator != null)
                return m_lotusAnimator.GetBool(m_lotusBoolHash);

            if (!m_lotusAnimatorProbed)
            {
                m_lotusAnimatorProbed = true;
                // 首次/child 重建后探测 Animator bool "ChefOnPlatfrom"。
                var animators = m_target.GetComponentsInChildren<Animator>(true);
                for (int i = 0; i < animators.Length; i++)
                {
                    var anim = animators[i];
                    if (anim == null)
                        continue;
                    var ps = anim.parameters;
                    if (ps == null)
                        continue;
                    for (int j = 0; j < ps.Length; j++)
                    {
                        if (ps[j].type == AnimatorControllerParameterType.Bool
                            && string.Equals(ps[j].name, "ChefOnPlatfrom", StringComparison.Ordinal))
                        {
                            m_lotusAnimator = anim;
                            m_lotusBoolHash = ps[j].nameHash;
                            return anim.GetBool(m_lotusBoolHash);
                        }
                    }
                }
            }

            try
            {
                // 兜底：ClientTriggerZone 占用（联机同步后的权威客户端状态）。
                if (GameApi.ClientTriggerZoneType != null && GameApi.ClientTriggerZoneOccupiedField != null)
                {
                    var client = m_target.GetComponentsInChildren(GameApi.ClientTriggerZoneType, true);
                    for (int i = 0; i < client.Length; i++)
                    {
                        if (client[i] == null)
                            continue;
                        var v = GameApi.ClientTriggerZoneOccupiedField.GetValue(client[i]);
                        return v is bool && (bool)v;
                    }
                }
                // 再兜底：ServerTriggerZone 的占用列表（单机/主机）。
                if (GameApi.ServerTriggerZoneType != null && GameApi.ServerTriggerZoneCollidersField != null)
                {
                    var server = m_target.GetComponentsInChildren(GameApi.ServerTriggerZoneType, true);
                    for (int i = 0; i < server.Length; i++)
                    {
                        if (server[i] == null)
                            continue;
                        var list = GameApi.ServerTriggerZoneCollidersField.GetValue(server[i]) as IList;
                        if (list != null && list.Count > 0)
                            return true;
                    }
                }
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[SwitchTint] 占用兜底读取失败: " + ex.Message);
            }
            return false;
        }

        private bool DetectLotus(GameObject go)
        {
            if (go == null)
                return false;
            var n = (go.name ?? "").ToLowerInvariant();
            if (n.IndexOf("lotuspressureswitch", StringComparison.Ordinal) >= 0)
                return true;
            var stub = go.GetComponent<PseudoPrefabStub>();
            if (stub != null && stub.pseudoPrefabSO != null
                && !string.IsNullOrEmpty(stub.pseudoPrefabSO.prefabName))
            {
                var pn = stub.pseudoPrefabSO.prefabName.ToLowerInvariant();
                if (pn.IndexOf("lotuspressureswitch", StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }

        private void ClearTint()
        {
            for (int i = 0; i < m_renderers.Count; i++)
            {
                var rt = m_renderers[i];
                if (rt == null || rt.renderer == null)
                    continue;
                rt.renderer.SetPropertyBlock(null);
            }
        }

        private static FieldInfo Field(Type t, string name)
        {
            if (t == null)
                return null;
            return t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private static Material FirstMaterial(Renderer r)
        {
            return r != null ? r.sharedMaterial : null;
        }

        private static Color? ParseColor(string hex)
        {
            Color c;
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out c))
                return c;
            return null;
        }

        private GameObject FindByPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            // 组件所在场景优先（避免同session菜单/其他场景存在同名 Design 根）。
            var own = gameObject.scene;
            if (own.IsValid() && own.isLoaded)
            {
                var found = FindInScene(own, path);
                if (found != null)
                    return found;
            }
            for (int si = 0; si < SceneManager.sceneCount; si++)
            {
                var scene = SceneManager.GetSceneAt(si);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;
                var found = FindInScene(scene, path);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static GameObject FindInScene(Scene scene, string path)
        {
            var parts = path.Split('/');
            if (parts.Length == 0)
                return null;
            var roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                var root = roots[r];
                if (root == null || root.name != parts[0])
                    continue;
                var current = root.transform;
                for (int i = 1; i < parts.Length; i++)
                {
                    var next = current.Find(parts[i]);
                    if (next == null)
                    {
                        for (int c = 0; c < current.childCount; c++)
                        {
                            var ch = current.GetChild(c);
                            if (ch != null && ch.name == parts[i])
                            {
                                next = ch;
                                break;
                            }
                        }
                    }
                    if (next == null)
                    {
                        current = null;
                        break;
                    }
                    current = next;
                }
                if (current != null)
                    return current.gameObject;
            }
            return null;
        }

        private static void RepaintSceneViews()
        {
            if (Application.isPlaying)
                return;
            try
            {
                var type = Type.GetType("UnityEditor.SceneView, UnityEditor");
                if (type == null)
                    return;
                var method = type.GetMethod("RepaintAll", BindingFlags.Public | BindingFlags.Static);
                if (method != null)
                    method.Invoke(null, null);
            }
            catch
            {
                // 编辑器 repaint 失败不影响染色
            }
        }
    }
}
