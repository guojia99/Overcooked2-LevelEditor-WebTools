using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 热水器（Kettle，commonW5 web_kettle_hotwater）：按开关 → 烧水（默认 5 秒）→
    /// 接一杯热水 → 冷却复位。复刻原版 DLC100 生日水壶玩法。
    ///
    /// 挂载对象：web_kettle_hotwater.prefab 实例根（StubIO 写回时反射烘焙）。
    /// prefab 自带原生组件：IngredientPropertiesComponent（当前食材 = 本库
    /// Node_HotWater，uID 100000004）+ PlacementItemSpawner（游戏自动挂
    /// Server/ClientPlacementItemSpawner 同步器，接水走原版联机链）+
    /// TriggerTimer（vanilla 同步计时 + CookingUI 进度条，startTrigger=Switch，
    /// m_CookingUIPrefab 留空由本组件运行时注入 bundle47 CookingUI）+
    /// Animator（DLC0100_Kettle 控制器，参数 StartHeating/Active/Cooldown，
    /// 三态 Cold/Heating/Hot 动画 + 蒸汽/火焰/滴水粒子 + _EmissiveStrength 发光）。
    ///
    /// 本组件职责（原版 ToggleablePlacementItemSpawner + KettleCosmeticDecisions
    /// 两个 DLC100 类在宿主构建中不存在，此处等价复刻）：
    ///  - 门禁：Active 态才允许 PlacementItemSpawner 接水（Harmony 前缀拦
    ///    ServerPlacementItemSpawner.CanHandlePlacement，见 HarmonyPatches）；
    ///  - 接一杯即冷（Harmony 后缀 HandlePlacement → Cooldown → 复位联动开关）；
    ///  - 状态机与动画/音效：Inoperative 触发通道投不进 stub 组件（仓库铁律），
    ///    改为轮询联动开关 TriggerDisableScript.m_script.enabled 的按压沿
    ///    （ButtonSpeedControl/SwitchReenable 同款，双端确定性推导）；
    ///  - 开关按压链（真机原版 Switch prefab）：Interactable 交互 impulse "Switch"
    ///    → TriggerAdapter(Switch→Disable) → TriggerDisableScript 禁用 Interactable
    ///    （按钮变红、不可再按）；**只有显式 "Reset"（m_enableTrigger）才回绿**。
    ///    编辑器 Play 里 0.35s 自动回弹只是 LayoutEditorSwitchReenableRelay
    ///    （仅编辑器存在），真机没有——不能据此认为原版开关会自维护回弹。
    ///  - **接水冷却时补发一次 Reset**（服务端权威；任一联动壶接水即回绿，多壶
    ///    共用一开关时不做“全部冷却”等待）。只发一次、绝不定频补发：ClientSwitch
    ///    CosmeticDecisions.Update 每次 Interactable.enabled 变化都会换材质 +
    ///    放 SwitchOn/SwitchOff，定频补发会让按钮在开/关之间闪（2026-10-27 实测）。
    ///
    /// 联机：按压/禁用状态由 vanilla 网络同步呈现到各端，本组件每端各跑一份、
    /// 各自推导状态（ButtonSpeedControl 同款论证）；接水判定仅服务端权威
    /// （CanHandlePlacement 前缀只在服务端同步器上命中）；复位触发也只由服务端
    /// 投递（客户端由 ClientTriggerDisableScript.ApplyServerEvent 同步回绿）。
    ///
    /// 数据双通道：本组件（StubIO 反射烘焙，权威）+ tag 载体 "Kettle|"
    /// （无 payload；组件缺失时 EntryPoint 自愈 AddComponent，走默认配置）。
    /// </summary>
    public class Kettle : MonoBehaviour
    {
        /** 联动开关（伪根物体；StubIO 由文档 switchLinks 反查烘焙）。 */
        public GameObject m_switch;
        /** 烧水秒数（与 TriggerTimer.m_time 镜像；门禁与动画用）。 */
        public float m_heatSeconds = 5f;
        /** 进度条 UI 供给（common04 _supplies CookingUI，StubIO 烘焙注入）。 */
        public LevelEditorStub.PseudoPrefabSO m_cookingUiSo;

        /// <summary>tag 载体前缀（EntryPoint 自愈与 StubIO 烘焙共用约定）。</summary>
        public const string TagPrefix = "Kettle|";
        /// <summary>版本金丝雀（真机日志确认 DLL 新鲜度）。</summary>
        public const string Version = "1";

        private enum State
        {
            Inactive,
            Loading,
            Active
        }

        private State m_state = State.Inactive;
        private float m_countdown;

        /// <summary>有效烧水秒数（0/负值兜底 5 秒）。</summary>
        private float HeatSeconds
        {
            get { return m_heatSeconds > 0f ? m_heatSeconds : 5f; }
        }
        private Animator m_animator;
        private AudioSource m_boil;
        private AudioSource m_pour;
        private Component m_triggerTimer;
        private Behaviour m_switchScript;
        // 原版开关按压后会被 TriggerDisableScript 禁用 Interactable（按钮变红），
        // 必须收到显式 "Reset" 才回绿。本组件只读 enabled 做按压沿检测，并在接水
        // 冷却时补发一次 Reset（OnServerFilled → ResetSwitchAfterFill）。绝不定频
        // 补发：ClientSwitchCosmeticDecisions.Update 每次 enabled 变化都会换材质 +
        // 放音效，定频补发会让按钮在开/关之间闪。
        private Component m_switchDisable;
        private string m_switchEnableTrigger;
        private bool m_switchLastEnabled = true;
        private bool m_switchBound;
        private bool m_uiInjected;
        private bool m_noSwitchWarned;
        private bool m_resetLogged;
        // 进度条 UI（CookingUIController，反射持有：CustomStub 不能编译期引用宿主类型）
        private Component m_cookUi;
        private MethodInfo m_uiSetState;
        private MethodInfo m_uiSetProgress;

        private static readonly List<Kettle> s_all = new List<Kettle>();
        private static bool s_loggedSelfCheck;

        /// <summary>场景内是否有热水器（Harmony 快速放行用，零分配）。</summary>
        internal static bool HasAny
        {
            get { return s_all.Count > 0; }
        }

        /// <summary>接水门禁：仅 Active 态允许（服务端权威判定）。</summary>
        internal bool AllowFill
        {
            get { return m_state == State.Active; }
        }

        /// <summary>按同步器组件反查热水器（Harmony 补丁入口，线性小表）。</summary>
        internal static Kettle FindBySpawner(Component spawner)
        {
            if (spawner == null || s_all.Count == 0)
                return null;
            var go = spawner.gameObject;
            for (int i = 0; i < s_all.Count; i++)
            {
                var k = s_all[i];
                if (k != null && k.gameObject == go)
                    return k;
            }
            return null;
        }

        private void OnEnable()
        {
            s_all.Add(this);
            StartCoroutine(SafeRunner(Drive()));
        }

        private void OnDisable()
        {
            s_all.Remove(this);
            StopAllCoroutines();
        }

        /// <summary>外层枚举器：C#4 禁止在有 catch 的 try 里 yield，套异常捕获。</summary>
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
                catch (System.Exception ex)
                {
                    StubLog.LogWarn("[Kettle] 协程异常退出: " + name + "\n" + ex);
                    yield break;
                }
                if (!hasNext)
                    yield break;
                yield return current;
            }
        }

        private IEnumerator Drive()
        {
            if (!s_loggedSelfCheck)
            {
                s_loggedSelfCheck = true;
                StubLog.Dbg("[Kettle] 反射自检: TriggerDisableScript=" +
                    (GameApi.TriggerDisableType != null) + " SendTrigger=" +
                    (GameApi.SendTriggerMethod != null) + " TriggerTimerCookingUI=" +
                    (GameApi.TriggerTimerCookingUiField != null));
            }

            // ---- 绑定期：animator / 音源 / 原生 TriggerTimer 本体常驻即可用
            m_animator = GetComponent<Animator>();
            BindAudio();
            m_triggerTimer = GetComponent("TriggerTimer");
            InjectCookingUi();

            float waited = 0f;
            while (!m_switchBound)
            {
                TryBindSwitch();
                if (m_switchBound)
                    break;
                waited += 0.25f;
                if (waited > 8f && !m_noSwitchWarned)
                {
                    m_noSwitchWarned = true;
                    // 无开关 = 永远烧不了水（UI 计时器也无人触发）：配置问题，
                    // 置警告后继续低频等（写回补链后立即生效）。
                    StubLog.LogWarn("[Kettle] " + name + " 未绑定联动开关（switchLinks 目标为本热水器？），暂时无法烧水");
                }
                yield return new WaitForSeconds(0.25f);
            }
            StubLog.Dbg("[Kettle] " + name + " 就绪（heat=" + m_heatSeconds.ToString("0.#") +
                "s switch=" + (m_switch != null ? m_switch.name : "无") + "）");

            // ---- 主循环：按压沿 / 计时 / 复位沿
            while (true)
            {
                if (m_switchScript == null)
                {
                    // 开关被销毁（关卡切换竞态）：静默退出，自愈重建
                    yield break;
                }
                bool enabledNow = m_switchScript.enabled;
                if (m_switchLastEnabled && !enabledNow)
                    OnPress();
                m_switchLastEnabled = enabledNow;

                if (m_state == State.Loading)
                {
                    m_countdown -= Time.deltaTime;
                    SetUiProgress(1f - Mathf.Clamp01(m_countdown / HeatSeconds));
                    if (m_countdown <= 0f)
                        OnActive();
                }
                yield return null;
            }
        }

        /// <summary>绑定音源子物体（SFX_Boil / SFX_Pour，prefab 自带）。</summary>
        private void BindAudio()
        {
            var sources = GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < sources.Length; i++)
            {
                var n = sources[i].gameObject.name;
                if (n == "SFX_Boil") m_boil = sources[i];
                else if (n == "SFX_Pour") m_pour = sources[i];
            }
        }

        /// <summary>绑定联动开关：子树扫 TriggerDisableScript（SwitchReenable 同款）。</summary>
        private void TryBindSwitch()
        {
            if (m_switch == null || GameApi.TriggerDisableType == null || GameApi.DisableScriptField == null)
                return;
            var found = m_switch.GetComponentsInChildren(GameApi.TriggerDisableType, true);
            for (int i = 0; i < found.Length; i++)
            {
                var script = GameApi.DisableScriptField.GetValue(found[i]) as Behaviour;
                if (script == null)
                    continue;
                m_switchScript = script;
                m_switchDisable = found[i];
                m_switchEnableTrigger = GameApi.DisableEnableTriggerField != null
                    ? GameApi.DisableEnableTriggerField.GetValue(found[i]) as string
                    : null;
                if (string.IsNullOrEmpty(m_switchEnableTrigger))
                    m_switchEnableTrigger = "Reset";
                m_switchLastEnabled = script.enabled;
                m_switchBound = true;
                return;
            }
        }

        /// <summary>进度条 UI：宿主 OC2 的 TriggerTimer 没有 m_CookingUIPrefab 字段
        /// （那是 AYCE 新增），注入无效 → 改为自建 CookingUI（bundle47）：烧水期
        /// Progressing + SetProgress 步进；煮好 Completed（自带打勾动画 + ImCooked
        /// 音效 = 完成提醒）；冷却 Idle。</summary>
        private void InjectCookingUi()
        {
            if (m_uiInjected)
                return;
            m_uiInjected = true;
            try
            {
                if (m_cookingUiSo == null || GameApi.InstantiateUIControllerMethod == null)
                {
                    StubLog.LogWarn("[Kettle] " + name + " CookingUI 不可用（SO/反射缺失），进度条不显示");
                    return;
                }
                var prefab = GameApi.LoadPseudoPrefabObject(m_cookingUiSo);
                if (prefab == null)
                {
                    StubLog.LogWarn("[Kettle] " + name + " CookingUI 加载失败（bundle47 未加载？）");
                    return;
                }
                var uiGo = GameApi.InstantiateUIControllerMethod.Invoke(
                    null, new object[] { prefab, "HoverIconCanvas" }) as GameObject;
                if (uiGo == null)
                    return;
                m_cookUi = uiGo.GetComponent("CookingUIController");
                if (m_cookUi == null)
                {
                    Destroy(uiGo);
                    StubLog.LogWarn("[Kettle] " + name + " CookingUIController 缺失");
                    return;
                }
                var t = m_cookUi.GetType();
                m_uiSetState = t.GetMethod("SetState",
                    BindingFlags.Public | BindingFlags.Instance);
                m_uiSetProgress = t.GetMethod("SetProgress",
                    BindingFlags.Public | BindingFlags.Instance);
                var follow = t.GetMethod("SetFollowTransform",
                    BindingFlags.Public | BindingFlags.Instance, null,
                    new System.Type[] { typeof(Transform), typeof(Vector3) }, null);
                if (follow == null && t.BaseType != null)
                    follow = t.BaseType.GetMethod("SetFollowTransform",
                        BindingFlags.Public | BindingFlags.Instance, null,
                        new System.Type[] { typeof(Transform), typeof(Vector3) }, null);
                if (follow != null)
                    follow.Invoke(m_cookUi, new object[] { transform, new Vector3(0f, 1.6f, 0f) });
                SetUiState(0);
                StubLog.Dbg("[Kettle] " + name + " 进度条 UI 已创建");
            }
            catch (System.Exception ex)
            {
                StubLog.LogWarn("[Kettle] " + name + " 进度条 UI 创建异常: " + ex.Message);
            }
        }

        /// <summary>CookingUIController.SetState(0=Idle/1=Progressing/2=Completed)。</summary>
        private void SetUiState(int state)
        {
            if (m_cookUi == null || m_uiSetState == null)
                return;
            try
            {
                var enumType = m_uiSetState.GetParameters()[0].ParameterType;
                m_uiSetState.Invoke(m_cookUi,
                    new object[] { System.Enum.ToObject(enumType, state) });
            }
            catch (System.Exception) { }
        }

        private void SetUiProgress(float progress)
        {
            if (m_cookUi == null || m_uiSetProgress == null)
                return;
            try
            {
                m_uiSetProgress.Invoke(m_cookUi, new object[] { progress });
            }
            catch (System.Exception) { }
        }

        private void OnDestroy()
        {
            if (m_cookUi != null)
                Destroy(m_cookUi.gameObject);
        }

        // ---- 状态迁移（每端各自推导；服务端额外被 Harmony 后缀回调） ----

        private void OnPress()
        {
            if (m_state != State.Inactive)
                return; // 烧水/已开时按压无效（原版：开关保持按下不可重复按）
            m_state = State.Loading;
            m_countdown = HeatSeconds;
            if (m_animator != null)
            {
                // 加热动画定长 5s：按实际烧水秒数慢放步进（12s → 0.4167×）
                m_animator.speed = 5f / HeatSeconds;
                m_animator.SetTrigger("StartHeating");
            }
            SetUiState(1);
            if (m_boil != null)
                m_boil.Play();
            StubLog.Dbg("[Kettle] " + name + " 开始烧水 " + m_heatSeconds.ToString("0.#") + "s");
        }

        private void OnActive()
        {
            m_state = State.Active;
            if (m_animator != null)
            {
                m_animator.speed = 1f;
                m_animator.SetTrigger("Active");
            }
            SetUiState(2);
            StubLog.Dbg("[Kettle] " + name + " 水已烧开，可接热水");
        }

        private void Cooldown(bool playPour)
        {
            m_state = State.Inactive;
            if (m_animator != null)
            {
                m_animator.speed = 1f;
                m_animator.SetTrigger("Cooldown");
            }
            SetUiState(0);
            if (playPour && m_pour != null)
                m_pour.Play();
            StubLog.Dbg("[Kettle] " + name + " 冷却复位" + (playPour ? "（接出一杯）" : ""));
        }

        /// <summary>服务端：接水完成（Harmony 后缀 ServerPlacementItemSpawner.
        /// HandlePlacement 命中本热水器）→ 冷却 + 补发一次开关复位（原版开关被
        /// TriggerDisableScript 禁用后只有显式 Reset 才回绿；真机没有编辑器 relay）。</summary>
        internal void OnServerFilled()
        {
            if (m_state != State.Active)
                return;
            Cooldown(true);
            ResetSwitchAfterFill();
        }

        /// <summary>服务端：任一联动壶接水冷却后补发一次开关复位（用户决策
        /// 2026-11-10：多壶共用一开关时任一壶接走即回绿，不做“全部冷却”等待）。
        /// 仍热的其它壶再次按压时由 OnPress 的 state != Inactive 早退忽略，不会
        /// 白丢水；开关已启用时跳过，避免多壶连续冷却重复投递。</summary>
        private void ResetSwitchAfterFill()
        {
            if (m_switchScript == null)
                return;
            if (!GameApi.IsServerMachine())
                return;
            if (m_switchScript.enabled)
                return;
            var target = m_switchDisable != null
                ? m_switchDisable.gameObject
                : m_switchScript.gameObject;
            if (target == null)
                return;
            var trigger = m_switchEnableTrigger;
            if (string.IsNullOrEmpty(trigger))
                trigger = "Reset";
            GameApi.SendTrigger(target, trigger);
            if (!m_resetLogged)
            {
                m_resetLogged = true;
                StubLog.Log("[Kettle] " + name + " 接水冷却：已复位联动开关 "
                    + (m_switch != null ? m_switch.name : "?") + "（" + trigger + "）");
            }
            else
            {
                StubLog.Dbg("[Kettle] " + name + " 接水冷却：复位联动开关 "
                    + (m_switch != null ? m_switch.name : "?") + "（" + trigger + "）");
            }
        }

        /// <summary>客户端：收到接水消息（ClientPlacementItemSpawner.
        /// ApplyServerEvent 后缀）→ 倒水表现。</summary>
        internal void OnClientFillMessage()
        {
            if (m_state != State.Active)
                return;
            Cooldown(true);
        }

        /// <summary>动画事件入口：KettleHeating 剪辑 t=0 发 AudioTrigger
        /// "DLC_100_Kettle_Boil"（Animator 同物体 SendMessage 生态，可达 stub）。</summary>
        private void AudioTrigger(string data)
        {
            if (data == "DLC_100_Kettle_Boil" && m_boil != null && !m_boil.isPlaying)
                m_boil.Play();
        }
    }
}
