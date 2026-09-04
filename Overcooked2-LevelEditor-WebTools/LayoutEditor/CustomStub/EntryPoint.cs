using System;
using System.Globalization;
using HarmonyLib;
using LevelEditorStub;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CustomStub
{
    /// <summary>
    /// CustomStub 运行时入口（新增 stub 组件的统一安装器）。
    ///
    /// 两条安装路径：
    ///  - 游戏侧：OC2LevelRuntimeLoader 在 Assembly.Load(Stub_&lt;set&gt;) 后反射调用
    ///    CustomStub.EntryPoint.Install()；
    ///  - 编辑器 Play：[RuntimeInitializeOnLoadMethod(AfterSceneLoad)] 自动安装
    ///    （Stub_&lt;set&gt; 程序集在编辑器内由 Unity 编译，进入 Play 时触发）。
    ///
    /// 幂等性：多个关卡集各有一份同名程序集（CustomStub.* 类重复定义），用
    /// 哨兵 GameObject（"CustomStub.Runtime"，DontDestroyOnLoad）保证全套
    /// ticker / Harmony 补丁 / 场景自愈只装一次。
    ///
    /// 职责：
    ///  1. Harmony 补丁（KillPlane 跳过 + 玩家脱离，HarmonyPatches）；
    ///  2. HotPot / PushableVoidFall 两个常驻 ticker；
    ///  3. sceneLoaded 场景自愈：按 SpecificPseudoPrefabTag 载体还原组件——
    ///     TimedSwitch| / PushablePot| / SwitchReenable| / WorldMapDressing|
    ///     （RandomCrate| 由 loader 自愈，此处不重复）。
    /// </summary>
    public static class EntryPoint
    {
        /// <summary>版本金丝雀：真机日志确认 bundle 内 DLL 新鲜度看这一行。</summary>
        public const string Version = "v1";

        private const string SentinelName = "CustomStub.Runtime";
        private const string HarmonyId = "oc2.customstub";
        private static bool s_installedThisAssembly;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            // 母本程序集（CustomStub，Editor 平台）只是模板/语法校验载体，
            // 运行实体是各关卡集的 Stub_<set>（全平台编译）——编辑器 Play 只装后者。
            if (typeof(EntryPoint).Assembly.GetName().Name == "CustomStub")
                return;
            Install();
        }

        /// <summary>安装入口（loader 反射调用；幂等）。返回是否由本次调用完成安装。</summary>
        public static bool Install()
        {
            if (s_installedThisAssembly)
                return false;
            s_installedThisAssembly = true;

            try
            {
                // 跨程序集幂等：另一关卡集的程序集已装过全套（同名类重复定义，
                // 重复装 = 双 ticker + 双 Harmony 前缀）
                var existing = GameObject.Find(SentinelName);
                if (existing != null)
                {
                    StubLog.Log("[CustomStub " + Version + "] 已由其他程序集安装，跳过（sentinel 存在）");
                    return false;
                }

                var host = new GameObject(SentinelName);
                UnityEngine.Object.DontDestroyOnLoad(host);
                host.AddComponent<CustomStubMarker>();

                InstallHarmony();
                host.AddComponent<HotPotTicker>();
                host.AddComponent<VoidFallTicker>();
                SceneManager.sceneLoaded += OnSceneLoadedHeal;
                HealScene(SceneManager.GetActiveScene());

                StubLog.Log("[CustomStub " + Version + "] EntryPoint 安装完成"
                    + "（Harmony=" + (s_harmonyInstalled ? "OK" : "失败，已依赖无前缀安全网")
                    + "，HotPot ticker + VoidFall ticker + 场景自愈）");
                return true;
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[CustomStub " + Version + "] 安装异常: " + ex);
                return false;
            }
        }

        /// <summary>Harmony 是否装上（2026-09-04 事故：编辑器 Play 里 HarmonyLib.Harmony
        /// 静态构造抛异常 → KillPlane 前缀缺失 → 玩家挂锅落水被宿主原生重生卡死。
        /// 无前缀时的安全网见 PushableVoidFall.BeginPotFall/松手路径的主动脱离。）</summary>
        private static bool s_harmonyInstalled;

        private static void InstallHarmony()
        {
            s_harmonyInstalled = false;
            var target = GameApi.RespawnObjectAddedMethod;
            if (target == null)
            {
                StubLog.LogWarn("[CustomStub] ServerRespawnCollider.ObjectAdded 反射失败，KillPlane 补丁未装（无前缀安全网生效）");
                return;
            }
            try
            {
                var harmony = new Harmony(HarmonyId);
                var prefixMethod = HarmonyPatches.RespawnColliderObjectAddedPrefixMethod;
                if (prefixMethod == null)
                {
                    StubLog.LogWarn("[CustomStub] 前缀方法缺失，KillPlane 补丁未装（无前缀安全网生效）");
                    return;
                }
                var prefix = new HarmonyMethod(prefixMethod);
                harmony.Patch(target, prefix);
                s_harmonyInstalled = true;
                StubLog.Log("[CustomStub] KillPlane 补丁已装: " + target.DeclaringType.Name + "." + target.Name);
            }
            catch (Exception ex)
            {
                // ex.Message 只有外层一句话（TypeInitializationException 不含真因），
                // 必须打全量（含 InnerException）才能定位（HarmonyLib.Harmony 静态构造
                // 在 Unity 2017.4 编辑器 Mono 上初始化失败，2026-09-04 待查真因）。
                StubLog.LogWarn("[CustomStub] Harmony 安装失败（可移动火锅 KillPlane 行为走无前缀安全网）: " + ex);
            }
        }

        private static void OnSceneLoadedHeal(Scene scene, LoadSceneMode mode)
        {
            HealScene(scene);
        }

        /// <summary>场景自愈：按 tag 载体补挂缺失组件并还原参数（组件为权威，
        /// 已存在的不动）。</summary>
        internal static void HealScene(Scene scene)
        {
            if (!scene.isLoaded)
                return;
            try
            {
                var tags = UnityEngine.Object.FindObjectsOfType<SpecificPseudoPrefabTag>();
                for (int i = 0; i < tags.Length; i++)
                {
                    var tag = tags[i];
                    if (tag == null || string.IsNullOrEmpty(tag.prefabTag))
                        continue;
                    try
                    {
                        HealObject(tag.gameObject, tag.prefabTag);
                    }
                    catch (Exception ex)
                    {
                        StubLog.LogWarn("[CustomStub] 自愈单对象失败 " + tag.gameObject.name + ": " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[CustomStub] 场景自愈失败: " + ex.Message);
            }
        }

        private static void HealObject(GameObject go, string prefabTag)
        {
            if (prefabTag.StartsWith(TimedCookingSwitch.TagPrefix, StringComparison.Ordinal))
            {
                if (go.GetComponent<TimedCookingSwitch>() != null)
                    return;
                var sw = go.AddComponent<TimedCookingSwitch>();
                ParseTimedSwitch(prefabTag.Substring(TimedCookingSwitch.TagPrefix.Length), sw);
                StubLog.Log("[CustomStub] 自愈 TimedSwitch: " + go.name);
            }
            else if (prefabTag.StartsWith(PushablePot.TagPrefix, StringComparison.Ordinal))
            {
                if (go.GetComponent<PushablePot>() != null)
                    return;
                var pot = go.AddComponent<PushablePot>();
                ParsePushablePot(prefabTag.Substring(PushablePot.TagPrefix.Length), pot);
                StubLog.Log("[CustomStub] 自愈 PushablePot: " + go.name);
            }
            else if (prefabTag.StartsWith(SwitchReenable.TagPrefix, StringComparison.Ordinal))
            {
                if (go.GetComponent<SwitchReenable>() != null)
                    return;
                var re = go.AddComponent<SwitchReenable>();
                ParseSwitchReenable(prefabTag.Substring(SwitchReenable.TagPrefix.Length), re);
                StubLog.Log("[CustomStub] 自愈 SwitchReenable: " + go.name);
            }
            else if (prefabTag.StartsWith(WorldMapDressing.TagPrefix, StringComparison.Ordinal))
            {
                if (go.GetComponent<WorldMapDressing>() != null)
                    return;
                go.AddComponent<WorldMapDressing>();
                StubLog.Log("[CustomStub] 自愈 WorldMapDressing: " + go.name);
            }
        }

        /// <summary>TimedSwitch|&lt;1|0&gt;,&lt;on&gt;,&lt;off&gt;,&lt;1|0&gt;</summary>
        private static void ParseTimedSwitch(string payload, TimedCookingSwitch sw)
        {
            if (string.IsNullOrEmpty(payload))
                return;
            var parts = payload.Split(',');
            if (parts.Length < 4)
                return;
            sw.m_enabled = parts[0].Trim() == "1";
            sw.m_onSeconds = ParseFloat(parts[1], 30f);
            sw.m_offSeconds = ParseFloat(parts[2], 30f);
            sw.m_startOn = parts[3].Trim() == "1";
        }

        /// <summary>PushablePot|&lt;bundle&gt;:&lt;path&gt;;&lt;bundle&gt;:&lt;path&gt;;...
        /// 第一项=大锅 prefab，其余=食材 OrderDefinitionNode。</summary>
        private static void ParsePushablePot(string payload, PushablePot pot)
        {
            if (string.IsNullOrEmpty(payload))
                return;
            var entries = payload.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            var bundles = new System.Collections.Generic.List<string>();
            var paths = new System.Collections.Generic.List<string>();
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i].Trim();
                var sep = entry.IndexOf(':');
                if (sep <= 0 || sep >= entry.Length - 1)
                    continue;
                var bundle = entry.Substring(0, sep);
                var path = entry.Substring(sep + 1);
                if (i == 0)
                {
                    pot.m_potBundle = bundle;
                    pot.m_potPath = path;
                }
                else
                {
                    bundles.Add(bundle);
                    paths.Add(path);
                }
            }
            pot.m_extraIngredientBundles = bundles.ToArray();
            pot.m_extraIngredientPaths = paths.ToArray();
        }

        /// <summary>SwitchReenable|&lt;delay&gt;（可空）</summary>
        private static void ParseSwitchReenable(string payload, SwitchReenable re)
        {
            if (string.IsNullOrEmpty(payload))
                return;
            re.m_resetDelay = ParseFloat(payload, 0.35f);
        }

        private static float ParseFloat(string s, float fallback)
        {
            float v;
            if (float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                return v;
            return fallback;
        }

        // ============ 哨兵与 ticker 宿主 ============

        /// <summary>安装标记（供排查：场景里的常驻对象是什么）。</summary>
        public class CustomStubMarker : MonoBehaviour
        {
        }

        /// <summary>火锅链路 ticker：每帧直驱烹饪，每 10 帧做三项维护。</summary>
        private class HotPotTicker : MonoBehaviour
        {
            private void Update()
            {
                try
                {
                    HotPot.CookPotsOverBurner(Time.deltaTime);
                    if (Time.frameCount % 10 != 0)
                        return;
                    HotPot.Tick();
                }
                catch (Exception ex)
                {
                    StubLog.LogWarn("[CustomStub.HotPot] tick skipped: " + ex.Message);
                }
            }
        }

        /// <summary>可移动火锅坠落 ticker（每 2 帧）。</summary>
        private class VoidFallTicker : MonoBehaviour
        {
            private void Update()
            {
                if (Time.frameCount % 2 != 0)
                    return;
                try
                {
                    PushableVoidFall.Tick();
                }
                catch (Exception ex)
                {
                    StubLog.LogWarn("[CustomStub.VoidFall] tick skipped: " + ex.Message);
                }
            }
        }
    }
}
