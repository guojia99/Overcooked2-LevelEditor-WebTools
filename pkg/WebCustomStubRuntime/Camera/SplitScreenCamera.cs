using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 多镜头分屏（SplitScreen|1 tag 自愈挂载）。
    ///
    /// 默认跟随行为与原版主相机（MultiplayerCamera）一致：每个分屏镜头克隆原相机
    /// 组件，只把 m_avatars 限定为一个厨师（SetupAvatars 前缀），距离/高度/边缘
    /// 缓冲全部沿用场景主相机参数；可选 offset/size/FOV 作为额外微调。
    ///
    /// 原版相机被屏蔽：保留 Camera + AudioListener + MainCamera tag
    /// （Camera.main / 音频语义不变），但 clearFlags=Nothing、cullingMask=0，
    /// depth 放到分屏相机之后；世界画面不再出原版视图。
    /// </summary>
    public static class SplitScreenCamera
    {
        /// <summary>tag 载体前缀（Camera 子物体或相机根节点均可）。</summary>
        public const string TagPrefix = "SplitScreen|";

        /// <summary>单个分屏镜头的运行时配置。</summary>
        public struct SplitCameraConfig
        {
            public bool Visible;
            public Vector3 Offset;
            public float FieldOfView;
            public float Size;
        }

        private struct CameraTarget
        {
            public Transform Target;
            public Vector3 Offset;
            public float Size;
            public Vector3 LastPosition;
        }

        private static bool s_active;
        private static bool s_patchInstalled;
        private static bool s_patchFailed;
        private static SplitScreenCameraDriver s_driver;
        private static readonly Dictionary<int, CameraTarget> s_targets =
            new Dictionary<int, CameraTarget>();

        // FastList<PlayerIDProvider> 反射缓存（_items / Count）。
        private static Type s_fastListType;
        private static FieldInfo s_fastListItemsField;
        private static PropertyInfo s_fastListCountProperty;

        /// <summary>场景自愈入口：tag 可挂在 Camera 子物体或相机根节点
        /// （MultiplayerCamera 所在物体）。</summary>
        internal static void ActivateFromTag(GameObject tagGo, string tagValue)
        {
            if (tagGo == null)
                return;
            var mp = FindMultiplayerCamera(tagGo);
            if (mp == null)
            {
                StubLog.LogWarn("[CustomStub] 分屏自愈: " + tagGo.name
                    + " 上/父级找不到 MultiplayerCamera（tag 应挂 Camera 子物体或相机根节点），跳过");
                return;
            }

            var configs = ParseConfigs(tagValue);
            s_active = true;
            CameraAuthoredOffset.SetSplitScreenActive(true);
            EnsurePatches();

            if (s_driver == null)
            {
                var host = new GameObject("SplitScreenCameras");
                s_driver = host.AddComponent<SplitScreenCameraDriver>();
                s_driver.Init(mp, configs);
                StubLog.Log("[CustomStub] 分屏模式已启用（相机根 " + mp.gameObject.name + "）");
            }
            else
            {
                s_driver.SetOriginal(mp);
                s_driver.SetConfigs(configs);
            }
        }

        internal static bool IsActive
        {
            get { return s_active; }
        }

        /// <summary>跨场景清场。同场景重复 HealScene 时保留现有驱动与目标映射
        /// （否则会从已屏蔽的原相机重新克隆导致黑屏）；旧场景驱动随场景销毁后
        /// 变成 Unity 假 null，这里清掉引用即可。</summary>
        internal static void OnSceneChanged()
        {
            s_active = false;
            if (s_driver != null && s_driver.gameObject == null)
            {
                s_driver = null;
                s_targets.Clear();
            }
            CameraAuthoredOffset.SetSplitScreenActive(false);
        }

        /// <summary>驱动被销毁（含退到主菜单等不走 HealScene 的场景）：复位静态
        /// 分屏开关与目标映射。若旧驱动晚于新驱动销毁（s_driver 已指向新驱动），忽略。</summary>
        internal static void OnDriverDestroyed(SplitScreenCameraDriver driver)
        {
            if (driver == null || s_driver != driver)
                return;
            s_driver = null;
            s_active = false;
            s_targets.Clear();
            CameraAuthoredOffset.SetSplitScreenActive(false);
        }

        internal static void RegisterTarget(Component mp, Transform target, Vector3 offset, float size)
        {
            if (mp == null || target == null)
                return;
            var info = new CameraTarget();
            info.Target = target;
            info.Offset = offset;
            info.Size = Mathf.Clamp(size > 0f ? size : 1f, 0.25f, 4f);
            info.LastPosition = target.position;
            s_targets[mp.GetInstanceID()] = info;
        }

        internal static void UnregisterTarget(Component mp)
        {
            if (mp == null)
                return;
            s_targets.Remove(mp.GetInstanceID());
        }

        /// <summary>SetupAvatars 前缀：把分屏镜头的 m_avatars 限定为单个厨师，
        /// 原版 Awake 随后按单目标计算 m_basePos/理想距离（默认行为=主相机）。</summary>
        private static bool SetupAvatarsPrefix(object __instance)
        {
            if (s_targets.Count == 0)
                return true;
            try
            {
                var comp = __instance as Component;
                if (comp == null)
                    return true;
                CameraTarget info;
                if (!s_targets.TryGetValue(comp.GetInstanceID(), out info) || info.Target == null)
                    return true;
                if (GameApi.MultiplayerCameraAvatarsField == null)
                    return true;
                GameApi.MultiplayerCameraAvatarsField.SetValue(__instance, new Transform[] { info.Target });
                return false;
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[CustomStub] 分屏 SetupAvatars 前缀异常（放行原方法）: " + ex.Message);
                return true;
            }
        }

        /// <summary>GetIdealLocation 前缀：把 m_basePos 固定到对应厨师当前位置，
        /// 让镜头始终以该厨师为中心（默认距离/高度仍由原版 m_minDistance + 主相机
        /// forward 决定）。目标已销毁时直接返回最后位置并跳过原方法，避免
        /// MissingReferenceException。</summary>
        private static bool IdealLocationPrefix(object __instance, ref Vector3 __result)
        {
            if (s_targets.Count == 0)
                return true;
            try
            {
                var comp = __instance as Component;
                if (comp == null)
                    return true;
                var id = comp.GetInstanceID();
                CameraTarget info;
                if (!s_targets.TryGetValue(id, out info))
                    return true;
                if (info.Target == null)
                {
                    __result = info.LastPosition;
                    return false;
                }
                info.LastPosition = info.Target.position;
                s_targets[id] = info;
                if (GameApi.MultiplayerCameraBasePosField != null)
                    GameApi.MultiplayerCameraBasePosField.SetValue(__instance, info.Target.position);
                return true;
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[CustomStub] 分屏 GetIdealLocation 前缀异常: " + ex.Message);
                return true;
            }
        }

        /// <summary>GetIdealLocation 后缀：在“主相机单目标跟随”结果上叠加可选的
        /// 每镜头 offset / size（默认 offset=0、size=1，完全等于原版主相机行为）。</summary>
        private static void IdealLocationPostfix(object __instance, ref Vector3 __result)
        {
            if (s_targets.Count == 0)
                return;
            try
            {
                var comp = __instance as Component;
                if (comp == null)
                    return;
                CameraTarget info;
                if (!s_targets.TryGetValue(comp.GetInstanceID(), out info) || info.Target == null)
                    return;
                if (Mathf.Abs(info.Size - 1f) > 0.0001f)
                {
                    var anchor = info.Target.position;
                    __result = anchor + (__result - anchor) * info.Size;
                }
                __result += info.Offset;
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[CustomStub] 分屏 GetIdealLocation 后缀异常: " + ex.Message);
            }
        }

        private static void EnsurePatches()
        {
            if (s_patchInstalled || s_patchFailed)
                return;
            var setupTarget = GameApi.MultiplayerCameraSetupAvatarsMethod;
            var idealTarget = GameApi.MultiplayerCameraGetIdealLocationMethod;
            if (setupTarget == null || idealTarget == null)
            {
                s_patchFailed = true;
                StubLog.LogWarn("[CustomStub] MultiplayerCamera.SetupAvatars/GetIdealLocation 反射失败，分屏补丁未装");
                return;
            }
            try
            {
                var setupPrefix = typeof(SplitScreenCamera).GetMethod("SetupAvatarsPrefix",
                    BindingFlags.NonPublic | BindingFlags.Static);
                var idealPrefix = typeof(SplitScreenCamera).GetMethod("IdealLocationPrefix",
                    BindingFlags.NonPublic | BindingFlags.Static);
                var idealPostfix = typeof(SplitScreenCamera).GetMethod("IdealLocationPostfix",
                    BindingFlags.NonPublic | BindingFlags.Static);
                var harmony = new Harmony("oc2.customstub.splitscreen");
                harmony.Patch(setupTarget, new HarmonyMethod(setupPrefix), null);
                harmony.Patch(idealTarget, new HarmonyMethod(idealPrefix),
                    new HarmonyMethod(idealPostfix));
                s_patchInstalled = true;
                StubLog.Log("[CustomStub] 分屏补丁已装（按需）: SetupAvatars prefix + GetIdealLocation postfix");
            }
            catch (Exception ex)
            {
                s_patchFailed = true;
                StubLog.LogWarn("[CustomStub] 分屏补丁安装失败: " + ex);
            }
        }

        /// <summary>解析 SplitScreen tag：C0..C3:ox,oy,oz,fov,size,vis；兼容旧 S:size。</summary>
        internal static SplitCameraConfig[] ParseConfigs(string tagValue)
        {
            var configs = new SplitCameraConfig[4];
            for (int i = 0; i < configs.Length; i++)
            {
                configs[i].Visible = true;
                configs[i].Offset = Vector3.zero;
                configs[i].FieldOfView = 0f;
                configs[i].Size = 1f;
            }
            if (string.IsNullOrEmpty(tagValue))
                return configs;

            var parts = tagValue.Split('|');
            // v2 起 C 段 offset = 相对主相机单目标默认跟随的额外偏移；
            // v1 的 C offset 是旧版“主相机相对玩家平均位置”的基准位，直接叠加会
            // 双倍偏移（相机飞走/黑屏）——v1 忽略 offset，按默认 0 迁移。
            var version = parts.Length > 1 ? parts[1] : "1";
            var useOffset = version == "2";
            var sawC = false;
            for (int p = 0; p < parts.Length; p++)
            {
                var part = parts[p];
                if (string.IsNullOrEmpty(part) || part.Length < 4 || part[0] != 'C'
                    || part[1] < '0' || part[1] > '3' || part[2] != ':')
                    continue;
                var idx = part[1] - '0';
                var vals = part.Substring(3).Split(',');
                if (vals.Length < 5)
                    continue;
                var c = configs[idx];
                if (useOffset)
                    c.Offset = new Vector3(FltAt(vals, 0), FltAt(vals, 1), FltAt(vals, 2));
                c.FieldOfView = FltAt(vals, 3);
                c.Size = Mathf.Clamp(FltAt(vals, 4), 0.25f, 4f);
                c.Visible = vals.Length < 6 || vals[5] != "0";
                configs[idx] = c;
                sawC = true;
            }
            if (sawC)
                return configs;

            // 旧格式：SplitScreen|1|S:1,1.2,0.8,1
            for (int p = 0; p < parts.Length; p++)
            {
                var part = parts[p];
                if (string.IsNullOrEmpty(part) || part.Length < 3 || part[0] != 'S' || part[1] != ':')
                    continue;
                var vals = part.Substring(2).Split(',');
                for (int i = 0; i < configs.Length && i < vals.Length; i++)
                {
                    float v;
                    if (float.TryParse(vals[i], System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out v))
                        configs[i].Size = Mathf.Clamp(v, 0.25f, 4f);
                }
                break;
            }
            return configs;
        }

        private static float FltAt(string[] vals, int i)
        {
            if (vals == null || i < 0 || i >= vals.Length)
                return 0f;
            float v;
            return float.TryParse(vals[i], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out v) ? v : 0f;
        }

        internal static Component FindMultiplayerCamera(GameObject go)
        {
            if (go == null || GameApi.MultiplayerCameraType == null)
                return null;
            var comp = go.GetComponent(GameApi.MultiplayerCameraType);
            if (comp != null)
                return comp;
            var inChildren = GameApi.GetComponentInChildren(go, GameApi.MultiplayerCameraType);
            if (inChildren != null)
                return inChildren;
            var t = go.transform.parent;
            while (t != null)
            {
                comp = t.GetComponent(GameApi.MultiplayerCameraType);
                if (comp != null)
                    return comp;
                t = t.parent;
            }
            return null;
        }

        /// <summary>本地厨师列表（按 PlayerID、实例 ID 排序）。只收 IsLocallyControlled
        /// 的对象，联机时不会给远端玩家分镜头。</summary>
        internal static void CollectLocalAvatars(List<Component> result)
        {
            result.Clear();
            var field = GameApi.PlayerIDProviderAllProvidersField;
            if (field == null)
                return;
            object list;
            try
            {
                list = field.GetValue(null);
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[CustomStub] 分屏读取 PlayerIDProvider.s_AllProviders 失败: "
                    + ex.Message);
                return;
            }
            if (list == null)
                return;

            try
            {
                var listType = list.GetType();
                if (s_fastListType != listType)
                {
                    s_fastListType = listType;
                    s_fastListItemsField = listType.GetField("_items",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    s_fastListCountProperty = listType.GetProperty("Count",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }
                if (s_fastListItemsField == null || s_fastListCountProperty == null)
                    return;

                var items = s_fastListItemsField.GetValue(list) as Array;
                if (items == null)
                    return;
                var count = (int)s_fastListCountProperty.GetValue(list, null);
                if (count > items.Length)
                    count = items.Length;
                for (int i = 0; i < count; i++)
                {
                    var provider = items.GetValue(i) as Component;
                    if (provider == null)
                        continue;
                    if (GameApi.IsLocallyControlledMethod != null)
                    {
                        var local = GameApi.IsLocallyControlledMethod.Invoke(provider, null);
                        if (local is bool && !(bool)local)
                            continue;
                    }
                    result.Add(provider);
                }
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[CustomStub] 分屏收集厨师列表失败: " + ex.Message);
                return;
            }

            result.Sort(CompareAvatars);
        }

        private static int CompareAvatars(Component a, Component b)
        {
            var ida = GetPlayerId(a);
            var idb = GetPlayerId(b);
            if (ida != idb)
                return ida.CompareTo(idb);
            return a.GetInstanceID().CompareTo(b.GetInstanceID());
        }

        private static int GetPlayerId(Component provider)
        {
            if (provider == null || GameApi.PlayerIDProviderGetIDMethod == null)
                return int.MaxValue;
            try
            {
                var value = GameApi.PlayerIDProviderGetIDMethod.Invoke(provider, null);
                return value == null ? int.MaxValue : Convert.ToInt32(value);
            }
            catch
            {
                return int.MaxValue;
            }
        }
    }
}
