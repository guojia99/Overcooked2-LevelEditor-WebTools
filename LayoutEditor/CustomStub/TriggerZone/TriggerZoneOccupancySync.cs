using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 触发区占用联机同步 + fallPad 占用清理（2026-09-12，接替 Assembly-CSharp-Patch
    /// ServerTriggerZone.cs / ClientTriggerZone.cs 的源码覆盖补丁——新功能逻辑写进
    /// 上游补丁目录违反「新功能归 CustomStub、补丁目录只放最小覆盖」条例，
    /// 两文件已删除并还原原版；fallPad 倒序清理修复一并搬入本模块）。
    ///
    /// 背景：原版游戏注册了 TriggerZoneMessage 类型与 TriggerZone→(Server/Client)
    /// TriggerZone 同步映射（MultiplayerController，原版代码），但服务端从不发、
    /// 客户端从不收——死通道。联机时压力开关踏板外观
    /// （ClientPressureSwitchCosmeticDecisions 按 ClientTriggerZone.IsOccupied()
    /// 切材质/按钮下沉）在客户端永不更新。本模块运行时接线：
    ///  1. ServerTriggerZone.OnTriggerEnter/OnTriggerExit 后缀：占用变化即广播
    ///     TriggerZoneMessage（= 原补丁的 SyncOccupied() 调用，事件驱动非热路径）；
    ///  2. ClientSynchroniserBase.ApplyServerEvent 后缀：实例为 ClientTriggerZone 且
    ///     负载为 TriggerZoneMessage 时写 m_occupied（= 原补丁的 override；
    ///     vanilla ClientTriggerZone 不 override 该虚方法，补丁打在基类上，
    ///     首行 GetType() 门控，其他同步器零影响）；
    ///  3. ServerTriggerZone.UpdateSynchronising 前缀：fallPad 占用列表先做正确清理
    ///     （倒序 + Unity 假 null），原版正序 Remove 循环随后跑在干净列表上——规避
    ///     其跳元素/已销毁 collider 抛 MissingReferenceException 的原版 bug
    ///     （可移动火锅落水 SetCollidersEnabled(false) 正是触发场景）。
    ///
    /// 安装：EntryPoint.Install 随装（独立 Harmony id oc2.customstub.triggerzone，
    /// 与 KillPlane/锅具时间/相机补丁相互独立，任一失败不影响其他组）。
    /// 官方图零影响 = 无 runtime 的关卡 zip 不含本程序集（loader 不注入）；
    /// 同 session 后续进官方图补丁仍在（事件驱动 + 前缀首行字段检查，开销可忽略，
    /// 语义=接通原版死通道/修原版 bug，与相机热方法全局 detour 的性能事故不同类）。
    /// </summary>
    internal static class TriggerZoneOccupancySync
    {
        // ---- 热路径异常 once-flag（与 HarmonyPatches 同规约：每类异常只报一次，
        // 报完放行=原版行为兜底，绝不让补丁异常炸进宿主逻辑） ----
        private static readonly System.Collections.Generic.HashSet<string> s_warnedOnce =
            new System.Collections.Generic.HashSet<string>();

        private static void WarnOnce(string key, string msg)
        {
            if (!s_warnedOnce.Add(key))
                return;
            StubLog.LogWarn(msg);
        }

        /// <summary>ServerTriggerZone.OnTriggerEnter/OnTriggerExit 后缀：占用变化即广播
        /// TriggerZoneMessage（镜像原版死方法 SyncOccupied：m_data.Initialise(IsOccupied())
        /// + SendServerEvent）。postfix 跑在原方法之后，占用列表已是最新。</summary>
        private static void SyncOccupiedPostfix(object __instance)
        {
            try
            {
                var comp = __instance as Component;
                if (comp == null)
                    return;
                if (GameApi.ServerTriggerZoneDataField == null
                    || GameApi.ServerTriggerZoneCollidersField == null
                    || GameApi.TriggerZoneMsgInitialiseMethod == null
                    || GameApi.SendServerEventMethod == null)
                    return;
                var data = GameApi.ServerTriggerZoneDataField.GetValue(comp);
                var list = GameApi.ServerTriggerZoneCollidersField.GetValue(comp) as IList;
                if (data == null)
                    return;
                GameApi.TriggerZoneMsgInitialiseMethod.Invoke(data,
                    new object[] { list != null && list.Count != 0 });
                GameApi.SendServerEventMethod.Invoke(comp, new object[] { data });
            }
            catch (Exception ex)
            {
                WarnOnce("tzSync", "[CustomStub.Harmony] 触发区占用同步后缀异常（跳过=原版行为）: " + ex.Message);
            }
        }

        /// <summary>ServerTriggerZone.UpdateSynchronising 前缀：fallPad 占用列表先正确
        /// 清理（倒序 + Unity 假 null 检查），原版正序 Remove 循环随后跑在干净列表上
        /// 自然空转——既不跳元素也不会对销毁 collider 抛 MissingReferenceException。
        /// 首行字段门控：非 fallPad / 空列表即返回（每帧×每触发区，仅两次字段读取）。</summary>
        private static void FallPadPrunePrefix(object __instance)
        {
            try
            {
                if (GameApi.ServerTriggerZoneTriggerField == null
                    || GameApi.ServerTriggerZoneCollidersField == null
                    || GameApi.FallPadField == null)
                    return;
                var comp = __instance as Component;
                if (comp == null)
                    return;
                var list = GameApi.ServerTriggerZoneCollidersField.GetValue(comp) as IList;
                if (list == null || list.Count == 0)
                    return;
                var zone = GameApi.ServerTriggerZoneTriggerField.GetValue(comp);
                if (zone == null || !(GameApi.FallPadField.GetValue(zone) is bool)
                    || !(bool)GameApi.FallPadField.GetValue(zone))
                    return;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var col = list[i] as Collider;
                    if (col == null || !col.enabled) // Unity 假 null：== null 命中已销毁
                        list.RemoveAt(i);
                }
            }
            catch (Exception ex)
            {
                WarnOnce("fallPadPrune", "[CustomStub.Harmony] fallPad 占用清理前缀异常（放行原版循环）: " + ex.Message);
            }
        }

        /// <summary>ClientSynchroniserBase.ApplyServerEvent 后缀：实例为 ClientTriggerZone
        /// 且负载为 TriggerZoneMessage 时写 m_occupied（= 原补丁在 ClientTriggerZone
        /// 上的 override；vanilla 不 override，故补在基类虚方法上）。首行 GetType()
        /// 门控——其他同步器（厨师/物理物体等）直接放行。</summary>
        private static void ApplyServerEventPostfix(object __instance, object serialisable)
        {
            try
            {
                if (__instance == null || GameApi.ClientTriggerZoneType == null
                    || __instance.GetType() != GameApi.ClientTriggerZoneType)
                    return;
                if (serialisable == null || GameApi.TriggerZoneMessageType == null
                    || serialisable.GetType() != GameApi.TriggerZoneMessageType
                    || GameApi.TriggerZoneMsgOccupiedField == null
                    || GameApi.ClientTriggerZoneOccupiedField == null)
                    return;
                var occupied = GameApi.TriggerZoneMsgOccupiedField.GetValue(serialisable);
                GameApi.ClientTriggerZoneOccupiedField.SetValue(__instance, occupied);
            }
            catch (Exception ex)
            {
                WarnOnce("tzApply", "[CustomStub.Harmony] 触发区消息接收后缀异常（跳过=踏板外观不更新）: " + ex.Message);
            }
        }

        // ---- 供 EntryPoint 手工绑定的 MethodInfo 表 ----

        internal static MethodInfo SyncOccupiedPostfixMethod
        {
            get { return typeof(TriggerZoneOccupancySync).GetMethod("SyncOccupiedPostfix", BF); }
        }

        internal static MethodInfo FallPadPrunePrefixMethod
        {
            get { return typeof(TriggerZoneOccupancySync).GetMethod("FallPadPrunePrefix", BF); }
        }

        internal static MethodInfo ApplyServerEventPostfixMethod
        {
            get { return typeof(TriggerZoneOccupancySync).GetMethod("ApplyServerEventPostfix", BF); }
        }

        private const BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Static;
    }
}
