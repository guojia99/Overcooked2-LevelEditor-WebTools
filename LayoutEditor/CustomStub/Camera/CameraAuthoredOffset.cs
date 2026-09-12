using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 相机出发点偏移（2026-09-11，接替 loader v1.7.0 的全局 CameraAuthoredOffsetPatch——
    /// 该补丁对热方法 GetIdealLocation 做全局 detour，拖整个 session 所有场景的帧率，
    /// 且违反「没用 runtime 的图零影响」铁律，已从 loader v1.7.1 移除）。
    ///
    /// 原版 MultiplayerCamera 在 Awake/FixedUpdate 里把相机位置重算为「玩家出生点
    /// 中心 + FOV/边缘缓冲反推距离」的函数，场景里摆放的相机 X/Z 在投影中被完全
    /// 抵消（编辑器里调的出发点位置 Play 后必被还原）。本模块给 GetIdealLocation
    /// 的返回值叠加固定 XZ 偏移（Y 恒 0，高度仍由跟随距离驱动；
    /// 偏移 = 摆放位 − 首个理想位），以摆放位为跟随中心，保留开场运镜与跟随微调。
    ///
    /// 生效双门槛（按需安装，与其他 stub 同规约）：
    ///  1. 场景相机根节点带 SpecificPseudoPrefabTag「CameraOffset|&lt;x&gt;,&lt;z&gt;」
    ///     （x/z = 摆放位世界坐标，invariant 浮点；由编辑器写回 ApplyCameraInfo 在
    ///     positionEdited 且 X/Z 实际变化时烘焙——无配置的场景根本没有这个 tag）；
    ///  2. tag 存在 → 该集 zip 必带 runtime（导出扫描前缀表）→ 本程序集加载 →
    ///     场景自愈扫到 tag 才安装 Harmony 补丁。
    ///    没用 runtime 的图：本程序集不存在，零影响；
    ///    装了 runtime 但没配置相机的图：补丁不安装，零影响；
    ///    补丁安装后进入无配置相机的场景：postfix 首行 s_activeCount 静态计数放行
    ///    （一次字段读取）。
    ///
    /// tag payload 即权威摆放位（不靠 Awake 前缀捕获）——场景自愈在 sceneLoaded
    /// 跑，晚于相机 Awake；若靠 Awake 捕获会错过首个场景。payload 方案下补丁只需
    /// 早于第一个 FixedUpdate，时序无忧；Awake 已完成原版 snap 时，相机会在开场
    /// 运镜期间由正弦跟随自然滑移到偏移位（不可见）。
    ///
    /// 编辑器 Play 与真机统一由本补丁承担（2026-09-12 起）——此前的编辑器侧镜像
    /// （Assembly-CSharp-Patch/MultiplayerCamera.cs 整文件补丁）违反「新功能逻辑
    /// 归 CustomStub、上游补丁目录只放最小覆盖」条例，已删除并还原原版文件。
    /// </summary>
    internal static class CameraAuthoredOffset
    {
        /// <summary>tag 载体前缀（值格式：&lt;x&gt;,&lt;z&gt;，invariant 浮点 = 相机根节点
        /// 摆放位世界 XZ）。前缀表三处同步：EntryPoint.IsStubTag/HealObject、
        /// LayoutEditorSetExporter.CustomStubTagPrefixes、LayoutEditorStubIO 写回。</summary>
        public const string TagPrefix = "CameraOffset|";

        /// <summary>instanceID → tag 携带的摆放位 XZ（待换算成偏移）。</summary>
        private static readonly Dictionary<int, Vector2> s_authored = new Dictionary<int, Vector2>();
        /// <summary>instanceID → 已算出的固定 XZ 偏移（Y 恒 0）。</summary>
        private static readonly Dictionary<int, Vector3> s_offsets = new Dictionary<int, Vector3>();
        /// <summary>快速放行计数（= s_authored.Count，postfix 首行直读，纳秒级）。</summary>
        private static int s_activeCount;

        private static bool s_patched;
        private static bool s_patchFailed;
        private static bool s_postfixWarned;

        /// <summary>场景自愈入口：相机根节点（带 tag 的物体）+ 摆放位 XZ。
        /// 幂等（同实例重复注册覆盖同值并重算偏移）。</summary>
        internal static void RegisterFromTag(GameObject rigRoot, float x, float z)
        {
            if (rigRoot == null)
                return;
            EnsurePatches();
            var cam = GameApi.GetComponent(rigRoot, GameApi.MultiplayerCameraType);
            if (cam == null)
            {
                StubLog.LogWarn("[CustomStub] CameraOffset 自愈: " + rigRoot.name
                    + " 上无 MultiplayerCamera 组件（tag 挂错物体？应为相机根节点），跳过");
                return;
            }
            var id = cam.GetInstanceID();
            s_authored[id] = new Vector2(x, z);
            s_offsets.Remove(id); // 重复 heal 时清掉旧偏移，下次 postfix 按新摆放位重算
            s_activeCount = s_authored.Count;
            StubLog.Log("[CustomStub] CameraOffset 注册: " + rigRoot.name + "（摆放位 XZ = "
                + x.ToString("0.##") + ", " + z.ToString("0.##") + "）");
        }

        /// <summary>场景切换清场。注意：不挂 ResetSceneTickers（该链在 HealScene 之后跑，
        /// 会把刚注册的相机清掉）——由 EntryPoint.HealScene 在扫描 tag 重新注册之前调用。</summary>
        internal static void OnSceneChanged()
        {
            s_authored.Clear();
            s_offsets.Clear();
            s_activeCount = 0;
        }

        /// <summary>按需安装 GetIdealLocation postfix（首个 CameraOffset| tag 自愈时触发），
        /// 幂等。与 KillPlane/锅具时间/空气取出补丁相互独立——任一失败不影响其他组。
        /// 编辑器 Play 与真机同路径安装（编辑器侧 Assembly-CSharp-Patch 镜像已删除）。</summary>
        internal static void EnsurePatches()
        {
            if (s_patched || s_patchFailed)
                return;
            var target = GameApi.MultiplayerCameraGetIdealLocationMethod;
            if (target == null)
            {
                s_patchFailed = true;
                StubLog.LogWarn("[CustomStub] MultiplayerCamera.GetIdealLocation 反射失败，"
                    + "相机偏移补丁未装（相机行为=原版）");
                return;
            }
            try
            {
                var postfix = typeof(CameraAuthoredOffset).GetMethod("IdealLocationPostfix",
                    BindingFlags.NonPublic | BindingFlags.Static);
                var harmony = new Harmony("oc2.customstub.cameraoffset");
                harmony.Patch(target, null, new HarmonyMethod(postfix));
                s_patched = true;
                StubLog.Log("[CustomStub] 相机偏移补丁已装（按需）: MultiplayerCamera.GetIdealLocation postfix");
            }
            catch (Exception ex)
            {
                s_patchFailed = true;
                StubLog.LogWarn("[CustomStub] 相机偏移补丁安装失败（相机行为=原版）: " + ex);
            }
        }

        /// <summary>MultiplayerCamera.GetIdealLocation 后缀：叠加固定 XZ 偏移。
        /// 跑在宿主热路径（每 FixedUpdate × 每相机）——首行静态计数放行，无配置
        /// 相机的场景零字典操作；异常 once-flag 上报后放行原结果（原版行为兜底）。</summary>
        private static void IdealLocationPostfix(object __instance, ref Vector3 __result)
        {
            if (s_activeCount == 0)
                return;
            try
            {
                var comp = __instance as Component;
                if (comp == null)
                    return;
                var id = comp.GetInstanceID();
                Vector3 offset;
                if (s_offsets.TryGetValue(id, out offset))
                {
                    __result += offset;
                    return;
                }
                Vector2 authored;
                if (!s_authored.TryGetValue(id, out authored))
                    return; // 该相机未配置偏移（其他图的相机）——不动
                // 首次经过：偏移 = 摆放位 − 原始理想位（仅 XZ，高度仍由跟随距离驱动）
                offset = new Vector3(authored.x - __result.x, 0f, authored.y - __result.z);
                s_offsets[id] = offset;
                __result += offset;
                StubLog.Log("[CustomStub] 相机偏移 [" + comp.gameObject.scene.name + "]: XZ 偏移 ("
                    + offset.x.ToString("0.##") + ", " + offset.z.ToString("0.##") + ") 已生效");
            }
            catch (Exception ex)
            {
                if (!s_postfixWarned)
                {
                    s_postfixWarned = true;
                    StubLog.LogWarn("[CustomStub] 相机偏移后缀异常（放行原结果=原版行为）: " + ex.Message);
                }
            }
        }
    }
}
