using System;
using System.Collections.Generic;
using LevelEditorStub;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 分屏驱动：负责模板克隆、viewport、原相机屏蔽、HUD 全屏相机、框线绘制。
    /// 挂在场景根容器 "SplitScreenCameras" 上，随场景销毁。
    /// </summary>
    public sealed class SplitScreenCameraDriver : MonoBehaviour
    {
        private Component m_original;
        private Camera m_originalCamera;
        private GameObject m_template;
        private GameObject m_backgroundGo;
        private Camera m_backgroundCamera;

        private readonly List<GameObject> m_splitRigs = new List<GameObject>();
        private readonly List<Camera> m_splitCameras = new List<Camera>();
        private readonly List<Component> m_splitMultiplayerCameras = new List<Component>();
        private readonly List<Transform> m_targets = new List<Transform>();

        private Rect[] m_viewports = new Rect[0];
        private Color[] m_colors = new Color[0];
        private SplitScreenCamera.SplitCameraConfig[] m_configs =
            new SplitScreenCamera.SplitCameraConfig[4];

        private bool m_built;
        private bool m_shielded;
        private int m_origCullingMask;
        private CameraClearFlags m_origClearFlags;
        private float m_origDepth;
        private Rect m_origRect;
        private bool m_origAllowHdr;
        private bool m_origAllowMsaa;
        private bool m_origCameraEnabled;
        private bool m_origMpEnabled;
        private int m_builtCount;
        private float m_nextCheck;
        private float m_nextUiScan;
        private float m_elapsed;
        private int m_lastScreenW;
        private int m_lastScreenH;

        private static Texture2D s_white;

        private static readonly Color[] Palette =
        {
            new Color(0.91f, 0.30f, 0.24f, 1f), // 红
            new Color(0.20f, 0.60f, 0.94f, 1f), // 蓝
            new Color(0.18f, 0.78f, 0.40f, 1f), // 绿
            new Color(0.95f, 0.77f, 0.10f, 1f), // 黄
        };

        public void Init(Component original, SplitScreenCamera.SplitCameraConfig[] configs)
        {
            m_original = original;
            m_originalCamera = FindCamera(original != null ? original.gameObject : null);
            m_configs = NormalizeConfigs(configs);
            m_nextCheck = 0f;
            m_elapsed = 0f;
            CheckAndBuild(); // 场景加载即尝试分屏；厨师未就绪则先屏蔽原镜头等下一帧
        }

        public void SetConfigs(SplitScreenCamera.SplitCameraConfig[] configs)
        {
            var next = NormalizeConfigs(configs);
            var changed = false;
            for (int i = 0; i < 4; i++)
            {
                if (Mathf.Abs(next[i].Size - m_configs[i].Size) > 0.0001f
                    || (next[i].Offset - m_configs[i].Offset).sqrMagnitude > 0.0001f
                    || Mathf.Abs(next[i].FieldOfView - m_configs[i].FieldOfView) > 0.0001f)
                {
                    changed = true;
                    break;
                }
            }
            m_configs = next;
            if (!changed)
                return;
            m_built = false;
            m_builtCount = 0;
            m_nextCheck = 0f;
            CheckAndBuild();
        }

        private static SplitScreenCamera.SplitCameraConfig[] NormalizeConfigs(
            SplitScreenCamera.SplitCameraConfig[] configs)
        {
            var result = new SplitScreenCamera.SplitCameraConfig[4];
            for (int i = 0; i < result.Length; i++)
            {
                var c = configs != null && i < configs.Length
                    ? configs[i]
                    : new SplitScreenCamera.SplitCameraConfig();
                c.Size = Mathf.Clamp(c.Size > 0f ? c.Size : 1f, 0.25f, 4f);
                result[i] = c;
            }
            return result;
        }

        private SplitScreenCamera.SplitCameraConfig ConfigFor(int index)
        {
            return index >= 0 && index < m_configs.Length
                ? m_configs[index]
                : new SplitScreenCamera.SplitCameraConfig { Size = 1f, Visible = true };
        }

        public void SetOriginal(Component original)
        {
            if (original == null || original == m_original)
                return;
            m_original = original;
            m_originalCamera = FindCamera(original.gameObject);
            if (m_template != null)
                UnityEngine.Object.Destroy(m_template);
            m_template = null;
            m_shielded = false;
            m_built = false;
            m_builtCount = 0;
            CleanupSplitCameras();
            m_nextCheck = 0f;
        }

        private void Update()
        {
            if (m_original == null)
                return;

            // 目标被销毁时 MultiplayerCamera 会访问空 Transform：每帧做 4 个 Transform 的有效性检查，
            // 失效立即重建（比等 0.5s 定时器更稳；开销仅列表长度）。
            if (m_built && !TargetsValid())
            {
                m_built = false;
                m_builtCount = 0;
                m_nextCheck = 0f;
                CheckAndBuild();
                return;
            }

            var dt = Time.unscaledDeltaTime;
            m_elapsed += dt;
            m_nextCheck -= dt;

            if (m_nextCheck <= 0f)
            {
                // 开局 2 秒内逐帧等厨师；之后 0.5s 低频校验（玩家销毁/重建要尽快收口）。
                m_nextCheck = m_elapsed < 2f ? 0f : 0.5f;
                CheckAndBuild();
            }

            if (m_built && (Screen.width != m_lastScreenW || Screen.height != m_lastScreenH))
                UpdateAspects();

            // HUD Canvas 可能晚于开局实例化（ScreenSpaceCamera 需要重指全屏 UI 相机）。
            // 低频扫描（5s）而非每帧，避免 FindGameObjectsWithTag 热路径开销。
            if (m_built && Time.unscaledTime >= m_nextUiScan)
            {
                m_nextUiScan = Time.unscaledTime + 5f;
                EnsureUiCamera();
            }
        }

        private void CheckAndBuild()
        {
            var avatars = new List<Component>();
            SplitScreenCamera.CollectLocalAvatars(avatars);

            if (m_built && m_builtCount == avatars.Count && TargetsValid())
                return;

            if (avatars.Count == 0)
            {
                // 厨师还没就绪：保持原版相机画面（不提前屏蔽，避免黑屏等待）。
                CleanupSplitCameras();
                RestoreOriginal();
                m_built = false;
                m_builtCount = 0;
                m_nextCheck = 0.5f;
                m_viewports = new Rect[0];
                m_colors = new Color[0];
                return;
            }

            EnsureTemplate();
            EnsureBackgroundCamera();
            CleanupSplitCameras();
            ShieldOriginal();

            var count = avatars.Count;
            if (count > 4)
            {
                StubLog.LogWarn("[CustomStub] 分屏厨师数 " + count + " > 4，仅启用前 4 个镜头");
                count = 4;
            }

            var rects = BuildViewports(count);
            BuildSplitCameras(avatars, rects, count);

            if (m_splitCameras.Count == 0)
            {
                var anyVisible = false;
                for (int i = 0; i < count; i++)
                {
                    if (ConfigFor(i).Visible)
                    {
                        anyVisible = true;
                        break;
                    }
                }
                if (anyVisible)
                {
                    // 构建失败（相机缺失/补丁异常等）：回退原版相机，绝不黑屏。
                    RestoreOriginal();
                    m_built = false;
                    m_builtCount = 0;
                    m_nextCheck = 1f;
                    m_viewports = new Rect[0];
                    m_colors = new Color[0];
                    StubLog.LogWarn("[CustomStub] 分屏相机创建失败，已回退原版相机（检查相机层级/分屏补丁）");
                    return;
                }
            }

            m_viewports = rects;
            m_colors = BuildColors(count);
            m_built = true;
            m_builtCount = avatars.Count;
            m_lastScreenW = Screen.width;
            m_lastScreenH = Screen.height;
            EnsureUiCamera();
            StubLog.Log("[CustomStub] 分屏已就绪: " + m_splitCameras.Count + "/" + count
                + " 镜头（原版镜头已屏蔽）");
            if (m_splitCameras.Count > 0 && m_splitCameras[0] != null)
            {
                var c0 = m_splitCameras[0];
                StubLog.Log("[CustomStub] 分屏相机样本: pos=" + c0.transform.position
                    + " rect=" + c0.rect + " mask=" + c0.cullingMask
                    + " fov=" + c0.fieldOfView + " enabled=" + c0.enabled);
            }
        }

        private bool TargetsValid()
        {
            for (int i = 0; i < m_targets.Count; i++)
            {
                if (m_targets[i] == null)
                    return false;
            }
            return true;
        }

        private void EnsureTemplate()
        {
            if (m_template != null || m_original == null)
                return;

            // 克隆前先摘掉会修改全局 Canvas 参考分辨率的组件，避免模板/克隆继承。
            DestroyComponentsInChildren(m_original.gameObject, GameApi.FixedAspectRatioCameraType);
            DestroyComponentsInChildren(m_original.gameObject, GameApi.CameraCanvasUpdaterType);

            m_template = (GameObject)UnityEngine.Object.Instantiate(m_original.gameObject, transform);
            m_template.name = "SplitScreenCameraTemplate";
            m_template.SetActive(false);
            StripForSplit(m_template);
        }

        /// <summary>模板/克隆清理：去掉音频、动画、Canvas 相机更新器，统一 Untagged；
        /// 保留 Camera / MultiplayerCamera（单目标由 SetupAvatars 前缀限定）/ CameraShaker。</summary>
        private static void StripForSplit(GameObject root)
        {
            if (root == null)
                return;


            // CameraAudioListener 先于 AudioListener 销毁：它的 OnDisable 会写
            // m_audiolistener.enabled，监听器先没了会 MissingReference。
            DestroyComponentsInChildren(root, GameApi.CameraAudioListenerType);
            var listeners = root.GetComponentsInChildren<AudioListener>(true);
            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i] == null)
                    continue;
                listeners[i].enabled = false;
                UnityEngine.Object.DestroyImmediate(listeners[i]);
            }
            var animators = root.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] == null)
                    continue;
                animators[i].enabled = false;
                UnityEngine.Object.DestroyImmediate(animators[i]);
            }
            DestroyComponentsInChildren(root, GameApi.CameraCanvasUpdaterType);
            DestroyComponentsInChildren(root, GameApi.FixedAspectRatioCameraType);

            // 模板/克隆从原相机复制了 SplitScreen| 载体 tag：运行期相机不需要它，
            // 移除以免后续场景扫描/自愈把分屏相机当成新的分屏宿主。
            // 模板/克隆不需要任何 CustomStub tag（SplitScreen|/CameraOffset| 等）；
            // 全部摘掉，避免运行期相机被后续 tag 扫描误当成新的分屏/偏移宿主。
            var stubTags = root.GetComponentsInChildren<SpecificPseudoPrefabTag>(true);
            for (int i = 0; i < stubTags.Length; i++)
            {
                if (stubTags[i] != null)
                    UnityEngine.Object.DestroyImmediate(stubTags[i]);
            }

            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null)
                    transforms[i].gameObject.tag = "Untagged";
            }
            var cam = FindCamera(root);
            if (cam != null && cam.transform != root.transform)
            {
                cam.transform.localPosition = Vector3.zero;
                cam.transform.localRotation = Quaternion.identity;
            }
        }

        private void BuildSplitCameras(List<Component> avatars, Rect[] rects, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (avatars[i] == null || avatars[i].transform == null)
                    continue;

                var cfg = ConfigFor(i);
                if (!cfg.Visible)
                {
                    // 编辑器里取消「启用镜头」= 不生成该玩家画面（保留该分屏黑框）。
                    StubLog.Log("[CustomStub] 分屏镜头 " + (i + 1) + " 已隐藏（不生成画面）");
                    continue;
                }

                var rig = (GameObject)UnityEngine.Object.Instantiate(m_template, transform);
                rig.name = "SplitScreenCamera_" + i;
                rig.tag = "Untagged";
                StripForSplit(rig); // 模板上的 Destroy 是延迟的，克隆可能继承残留组件：再清一次

                var mp = SplitScreenCamera.FindMultiplayerCamera(rig);
                var cam = FindCamera(rig);
                if (mp == null || cam == null)
                {
                    StubLog.LogWarn("[CustomStub] 分屏克隆缺少 MultiplayerCamera/Camera，跳过镜头 " + i);
                    UnityEngine.Object.Destroy(rig);
                    continue;
                }

                var target = avatars[i].transform;
                // 激活前注册：SetupAvatars 前缀把 m_avatars 限定为单目标，Awake 按
                // 原版主相机参数计算 m_basePos/距离；GetIdealLocation 前后缀把
                // basePos 钉在玩家上并叠加可选 offset/size（默认 0/1 = 主相机单目标）。
                SplitScreenCamera.RegisterTarget(mp, target, cfg.Offset, cfg.Size);
                // 距离/高度沿用主相机，但单玩家分屏需要更跟手的平滑，避免跑出画面。
                if (GameApi.MultiplayerCameraGradientLimitField != null)
                    GameApi.MultiplayerCameraGradientLimitField.SetValue(mp, 25f);
                if (GameApi.MultiplayerCameraTimeToMaxField != null)
                    GameApi.MultiplayerCameraTimeToMaxField.SetValue(mp, 0.25f);

                var fov = cfg.FieldOfView > 0f ? Mathf.Clamp(cfg.FieldOfView, 1f, 179f) : cam.fieldOfView;
                cam.fieldOfView = fov;
                cam.rect = rects[i];
                cam.aspect = ComputeAspect(rects[i]);
                cam.depth = 0f;
                cam.enabled = true;

                m_splitRigs.Add(rig);
                m_splitMultiplayerCameras.Add(mp);
                m_splitCameras.Add(cam);
                m_targets.Add(target);

                rig.SetActive(true);
                // 激活后再落一次 FOV/rect/aspect：避免 Camera.OnEnable 重算覆盖。
                cam.fieldOfView = fov;
                cam.rect = rects[i];
                cam.aspect = ComputeAspect(rects[i]);
            }
        }

        private void CleanupSplitCameras()
        {
            for (int i = 0; i < m_splitMultiplayerCameras.Count; i++)
                SplitScreenCamera.UnregisterTarget(m_splitMultiplayerCameras[i]);
            for (int i = 0; i < m_splitRigs.Count; i++)
            {
                if (m_splitRigs[i] != null)
                    UnityEngine.Object.DestroyImmediate(m_splitRigs[i]);
            }
            m_splitRigs.Clear();
            m_splitMultiplayerCameras.Clear();
            m_splitCameras.Clear();
            m_targets.Clear();
        }

        /// <summary>屏蔽原版镜头：保留 Camera/AudioListener/MainCamera，但不出图。
        /// MultiplayerCamera 禁用（冻结跟随），后处理/动画关闭。</summary>
        private void ShieldOriginal()
        {
            if (m_shielded || m_originalCamera == null)
                return;

            m_origCullingMask = m_originalCamera.cullingMask;
            m_origClearFlags = m_originalCamera.clearFlags;
            m_origDepth = m_originalCamera.depth;
            m_origRect = m_originalCamera.rect;
            m_origAllowHdr = m_originalCamera.allowHDR;
            m_origAllowMsaa = m_originalCamera.allowMSAA;
            m_origCameraEnabled = m_originalCamera.enabled;
            var mpb = m_original as Behaviour;
            m_origMpEnabled = mpb != null && mpb.enabled;

            m_originalCamera.enabled = true;
            // 完全屏蔽原版世界画面；HUD 由 Overlay Canvas 或专用 UI 相机负责。
            // 不再用 UI 层兜底，避免某些关卡的全屏 UI 背景把分屏画面盖成黑屏。
            m_originalCamera.cullingMask = 0;
            m_originalCamera.clearFlags = CameraClearFlags.Nothing;
            m_originalCamera.rect = new Rect(0f, 0f, 1f, 1f);
            m_originalCamera.depth = 50f;
            m_originalCamera.allowHDR = false;
            m_originalCamera.allowMSAA = false;

            if (GameApi.PostProcessingBehaviourType != null)
            {
                var pps = GameApi.GetComponentsInChildren(m_original.gameObject,
                    GameApi.PostProcessingBehaviourType, true);
                for (int i = 0; i < pps.Length; i++)
                {
                    var b = pps[i] as Behaviour;
                    if (b != null)
                        b.enabled = false;
                }
            }

            var animators = m_original.gameObject.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null)
                    animators[i].enabled = false;
            }
            if (GameApi.CameraShakerType != null)
            {
                var shakers = GameApi.GetComponentsInChildren(m_original.gameObject,
                    GameApi.CameraShakerType, true);
                for (int i = 0; i < shakers.Length; i++)
                {
                    var b = shakers[i] as Behaviour;
                    if (b != null)
                        b.enabled = false;
                }
            }
            m_originalCamera.transform.localPosition = Vector3.zero;

            if (mpb != null)
                mpb.enabled = false;
            m_shielded = true;
        }

        /// <summary>分屏失败/厨师未就绪时回退原版相机，避免玩家黑屏。</summary>
        private void RestoreOriginal()
        {
            if (!m_shielded || m_originalCamera == null)
                return;

            m_originalCamera.enabled = m_origCameraEnabled;
            m_originalCamera.cullingMask = m_origCullingMask;
            m_originalCamera.clearFlags = m_origClearFlags;
            m_originalCamera.depth = m_origDepth;
            m_originalCamera.rect = m_origRect;
            m_originalCamera.allowHDR = m_origAllowHdr;
            m_originalCamera.allowMSAA = m_origAllowMsaa;

            if (GameApi.PostProcessingBehaviourType != null)
            {
                var pps = GameApi.GetComponentsInChildren(m_original.gameObject,
                    GameApi.PostProcessingBehaviourType, true);
                for (int i = 0; i < pps.Length; i++)
                {
                    var b = pps[i] as Behaviour;
                    if (b != null)
                        b.enabled = true;
                }
            }
            var animators = m_original.gameObject.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null)
                    animators[i].enabled = true;
            }
            if (GameApi.CameraShakerType != null)
            {
                var shakers = GameApi.GetComponentsInChildren(m_original.gameObject,
                    GameApi.CameraShakerType, true);
                for (int i = 0; i < shakers.Length; i++)
                {
                    var b = shakers[i] as Behaviour;
                    if (b != null)
                        b.enabled = true;
                }
            }

            var mpb = m_original as Behaviour;
            if (mpb != null)
                mpb.enabled = m_origMpEnabled;
            m_shielded = false;
        }

        private void EnsureBackgroundCamera()
        {
            if (m_backgroundCamera != null)
                return;
            m_backgroundGo = new GameObject("SplitScreenBackgroundCamera");
            m_backgroundGo.transform.SetParent(transform, false);
            m_backgroundCamera = m_backgroundGo.AddComponent<Camera>();
            m_backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
            m_backgroundCamera.backgroundColor = Color.black;
            m_backgroundCamera.cullingMask = 0;
            m_backgroundCamera.orthographic = true;
            m_backgroundCamera.depth = -100f;
            m_backgroundCamera.rect = new Rect(0f, 0f, 1f, 1f);
            m_backgroundCamera.allowHDR = false;
            m_backgroundCamera.allowMSAA = false;
        }

        /// <summary>HUD 全屏：把 ScreenSpaceCamera Canvas 改为 ScreenSpaceOverlay，
        /// 避免额外 UI 相机（cullingMask 可能误带世界层，出现黑屏/世界重绘）。</summary>
        private void EnsureUiCamera()
        {
            GameObject[] canvases;
            try
            {
                canvases = GameObject.FindGameObjectsWithTag("Canvas");
            }
            catch
            {
                return;
            }
            if (canvases == null || canvases.Length == 0)
                return;

            var converted = 0;
            for (int i = 0; i < canvases.Length; i++)
            {
                var go = canvases[i];
                if (go == null || IsCanvasCameraAssigned(go))
                    continue;
                var canvas = go.GetComponent<Canvas>();
                if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    converted++;
                }
            }
            if (converted > 0)
                StubLog.Log("[CustomStub] HUD Canvas 已改为全屏 Overlay × " + converted);
        }

        private static bool IsCanvasCameraAssigned(GameObject go)
        {
            return GameApi.CanvasCameraAssignmentType != null
                && go.GetComponent(GameApi.CanvasCameraAssignmentType) != null;
        }

        private static int BuildUiMask(GameObject[] canvases)
        {
            var mask = 0;
            for (int i = 0; i < canvases.Length; i++)
            {
                var go = canvases[i];
                if (go == null || IsCanvasCameraAssigned(go))
                    continue;
                var canvas = go.GetComponent<Canvas>();
                if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceCamera)
                    continue;
                var transforms = go.GetComponentsInChildren<Transform>(true);
                for (int k = 0; k < transforms.Length; k++)
                {
                    if (transforms[k] != null)
                        mask |= 1 << transforms[k].gameObject.layer;
                }
            }
            return mask == 0 ? (1 << 5) : mask;
        }

        private void UpdateAspects()
        {
            m_lastScreenW = Screen.width;
            m_lastScreenH = Screen.height;
            for (int i = 0; i < m_splitCameras.Count && i < m_viewports.Length; i++)
            {
                if (m_splitCameras[i] != null)
                    m_splitCameras[i].aspect = ComputeAspect(m_viewports[i]);
            }
        }

        private static float ComputeAspect(Rect r)
        {
            var w = Screen.width * r.width;
            var h = Screen.height * r.height;
            if (w <= 0f || h <= 0f)
                return 1.7777f;
            return w / h;
        }

        private static Rect[] BuildViewports(int count)
        {
            if (count <= 1)
                return new[] { new Rect(0f, 0f, 1f, 1f) };
            if (count == 2)
            {
                return new[]
                {
                    new Rect(0f, 0f, 0.5f, 1f),
                    new Rect(0.5f, 0f, 0.5f, 1f)
                };
            }
            if (count == 3)
            {
                // 上 60% 宽居中 + 下两 50%；上方左右各 20% 由背景相机呈黑边。
                return new[]
                {
                    new Rect(0.2f, 0.5f, 0.6f, 0.5f),
                    new Rect(0f, 0f, 0.5f, 0.5f),
                    new Rect(0.5f, 0f, 0.5f, 0.5f)
                };
            }
            return new[]
            {
                new Rect(0f, 0.5f, 0.5f, 0.5f),
                new Rect(0.5f, 0.5f, 0.5f, 0.5f),
                new Rect(0f, 0f, 0.5f, 0.5f),
                new Rect(0.5f, 0f, 0.5f, 0.5f)
            };
        }

        private static Color[] BuildColors(int count)
        {
            var colors = new Color[count];
            for (int i = 0; i < count; i++)
                colors[i] = Palette[i % Palette.Length];
            return colors;
        }

        private void OnGUI()
        {
            if (!m_built || m_viewports.Length == 0 || m_colors.Length == 0)
                return;
            if (Event.current == null || Event.current.type != EventType.Repaint)
                return;

            EnsureWhiteTexture();
            if (s_white == null)
                return;

            var thickness = Mathf.Max(2, Mathf.RoundToInt(Screen.height / 360f));
            for (int i = 0; i < m_viewports.Length && i < m_colors.Length; i++)
            {
                var r = m_viewports[i];
                var x = r.x * Screen.width;
                var y = (1f - r.y - r.height) * Screen.height;
                var w = r.width * Screen.width;
                var h = r.height * Screen.height;
                GUI.color = m_colors[i];
                GUI.DrawTexture(new Rect(x, y, w, thickness), s_white);
                GUI.DrawTexture(new Rect(x, y + h - thickness, w, thickness), s_white);
                GUI.DrawTexture(new Rect(x, y, thickness, h), s_white);
                GUI.DrawTexture(new Rect(x + w - thickness, y, thickness, h), s_white);
            }
            GUI.color = Color.white;
        }

        private static void EnsureWhiteTexture()
        {
            if (s_white != null)
                return;
            s_white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            s_white.SetPixel(0, 0, Color.white);
            s_white.Apply();
            s_white.hideFlags = HideFlags.HideAndDontSave;
        }

        private static Camera FindCamera(GameObject root)
        {
            if (root == null)
                return null;
            var cam = root.GetComponent<Camera>();
            if (cam != null)
                return cam;
            return root.GetComponentInChildren<Camera>(true);
        }

        private static void DestroyComponentsInChildren(GameObject root, Type type)
        {
            if (root == null || type == null)
                return;
            var comps = GameApi.GetComponentsInChildren(root, type, true);
            for (int i = 0; i < comps.Length; i++)
            {
                if (comps[i] == null)
                    continue;
                var b = comps[i] as Behaviour;
                if (b != null)
                    b.enabled = false;
                UnityEngine.Object.DestroyImmediate(comps[i]);
            }
        }

        private void OnDestroy()
        {
            // 驱动 GameObject 被销毁时，模板/背景/分屏相机都是其子物体，
            // 由场景卸载统一销毁；这里只解除目标映射并清运行时列表。
            for (int i = 0; i < m_splitMultiplayerCameras.Count; i++)
                SplitScreenCamera.UnregisterTarget(m_splitMultiplayerCameras[i]);
            m_splitRigs.Clear();
            m_splitMultiplayerCameras.Clear();
            m_splitCameras.Clear();
            m_targets.Clear();
            SplitScreenCamera.OnDriverDestroyed(this);
            // 静态 1x1 白纹理不在此销毁：同一帧可能已有新驱动复用；
            // HideAndDontSave 会在退出/域重载时自动清理。
        }
    }
}
