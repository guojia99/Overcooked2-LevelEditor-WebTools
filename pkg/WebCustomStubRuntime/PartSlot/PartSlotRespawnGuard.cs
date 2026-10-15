using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 分 P 道具重生守卫（3.10.0，2026-10-15，docs/10 §4.4/D16）。
    ///
    /// 问题：分 P 关卡旧 P 下沉清理（slot 根 TriggerKillAttachments → KillOrRespawn）
    /// 对锅具/净盘/脏盘叠/灭火器等带 IRespawnBehaviour 的道具只会重生、永不销毁
    /// （ServerPlayerRespawnManager.KillOrRespawn）；而重生选台
    /// ServerUtensilRespawnBehaviour.GetFreeAttachStation 扫全场景
    /// ServerAttachStation，分 P「slot 根常激活停放」（规格 F5）使沉没/停泊中的
    /// 站台全部 activeInHierarchy 且距理想出生点最近 → 道具重生回地下站台
    /// （玩家视角 = 没销毁、也没出现在下一 P）。
    ///
    /// 方案：给 ServerUtensilRespawnBehaviour 及 4 个派生类（锅/净盘/脏盘叠/灭火器，
    /// 全部 override）各自声明的 CanRespawnOnStation 打 Harmony 后缀——站台位于
    /// 「非在场 slot」（祖先为 Design/PartSlots/&lt;pid&gt; 且 slot 根 Animator 不在
    /// Idle_Active 定持态）时改判 false。效果：
    ///  - 旧 P 道具被杀后，基类 while(freeStation==null) 重试循环会等到新 P 升起
    ///    就位（Entry→Idle_Active）再落到其空闲站台 = 「刷新到下一 P」（跨 P 携带）；
    ///  - 脏盘叠派生类的 IdealSpawnLocation 短路同样经 CanRespawnOnStation 判定，
    ///    理想站台沉没时自动回落基类全场扫描；
    ///  - base 层站台不在任何 slot 下，恒可选择（玩家放到 base 桌台上的道具不动）。
    ///
    /// 配套站台优先（同版）：再给 GetStationRespawnPriority（基类 + 锅具派生
    /// 声明）打后缀——净盘对非归还台（无 ServerPlateReturnStation 组件）加
    /// +1000 距离惩罚，使其优先落归还台、无空闲归还台才落普通工作台。锅具/
    /// 搅拌碗（含搅拌杯）原版已有配套硬约束（InvalidStationFilter 要求灶台类型
    /// 匹配）+ 非灶台 +1000 惩罚，脏盘叠原版已限定只扫归还台，均无需干预；
    /// 灭火器等通用道具保持「最近可用站台」（含 base/当前 P 工作台）。
    ///
    /// 生效门槛（按需安装，与其他 stub 同规约）：HealScene 扫到场景存在
    /// Design/PartSlots 根才安装补丁；普通关卡 s_sceneHasSlots=0，postfix 首行
    /// 放行（一次静态字段读取），零行为变化。编辑器 Play 与真机同路径安装。
    /// </summary>
    internal static class PartSlotRespawnGuard
    {
        /// <summary>在场判定兜底阈值（slot 根 Animator 缺失时按 localPosition 判，
        /// 超过 0.5m 视为非在场）。</summary>
        private const float InPlaceThresholdSqr = 0.25f;

        /// <summary>快速放行：0 = 本场景无 PartSlots（postfix 首行直读）。</summary>
        private static int s_sceneHasSlots;

        private static bool s_patched;
        private static bool s_patchFailed;
        private static bool s_postfixWarned;

        /// <summary>场景切换登记：本场景是否含 Design/PartSlots（EntryPoint.HealScene
        /// 调用，幂等）。</summary>
        internal static void OnSceneChanged(bool hasPartSlots)
        {
            s_sceneHasSlots = hasPartSlots ? 1 : 0;
        }

        /// <summary>按需安装 CanRespawnOnStation postfix（基类 + 4 派生类各自声明，
        /// 反射目标在 GameApi.UtensilCanRespawnOnStationMethods），幂等。与其他
        /// 补丁组相互独立——失败只影响分 P 关卡的道具跨 P 携带（退回原版行为）。</summary>
        internal static void EnsurePatches()
        {
            if (s_patched || s_patchFailed)
                return;
            var targets = GameApi.UtensilCanRespawnOnStationMethods;
            if (targets == null || targets.Length == 0)
            {
                s_patchFailed = true;
                StubLog.LogWarn("[CustomStub] 分 P 道具重生守卫: CanRespawnOnStation 反射失败，"
                    + "补丁未装（道具重生=原版行为）");
                return;
            }
            try
            {
                var postfix = typeof(PartSlotRespawnGuard).GetMethod("CanRespawnOnStationPostfix",
                    BindingFlags.NonPublic | BindingFlags.Static);
                var harmony = new Harmony("oc2.customstub.partslotguard");
                int ok = 0;
                for (int i = 0; i < targets.Length; i++)
                {
                    if (targets[i] == null)
                        continue;
                    harmony.Patch(targets[i], null, new HarmonyMethod(postfix));
                    ok++;
                }
                // 配套站台优先（净盘→归还台）：优先级后缀，独立于上面的选台过滤。
                var prioTargets = GameApi.UtensilStationPriorityMethods;
                if (prioTargets != null)
                {
                    var prioPostfix = typeof(PartSlotRespawnGuard).GetMethod(
                        "GetStationRespawnPriorityPostfix", BindingFlags.NonPublic | BindingFlags.Static);
                    for (int i = 0; i < prioTargets.Length; i++)
                    {
                        if (prioTargets[i] == null)
                            continue;
                        harmony.Patch(prioTargets[i], null, new HarmonyMethod(prioPostfix));
                    }
                }
                s_patched = ok > 0;
                StubLog.Log("[CustomStub] 分 P 道具重生守卫已装（按需）: CanRespawnOnStation postfix × " + ok);
            }
            catch (Exception ex)
            {
                s_patchFailed = true;
                StubLog.LogWarn("[CustomStub] 分 P 道具重生守卫安装失败（道具重生=原版行为）: " + ex);
            }
        }

        /// <summary>CanRespawnOnStation 后缀：站台位于非在场 slot 时改判 false。
        /// 非热路径（仅道具重生扫描时调用）；异常 once-flag 上报后放行原结果
        /// （原版行为兜底）。参数名 _attachStation 与宿主 5 个目标方法一致
        /// （Harmony 按名注入）。</summary>
        private static void CanRespawnOnStationPostfix(object _attachStation, ref bool __result)
        {
            if (!__result || s_sceneHasSlots == 0)
                return;
            try
            {
                if (IsUnderNonActivePartSlot(_attachStation as Component))
                    __result = false;
            }
            catch (Exception ex)
            {
                if (!s_postfixWarned)
                {
                    s_postfixWarned = true;
                    StubLog.LogWarn("[CustomStub] 分 P 道具重生守卫后缀异常（放行原结果=原版行为）: " + ex.Message);
                }
            }
        }

        /// <summary>不合适站台的距离惩罚（与原版锅具 c_incorrectStationModifier
        /// 同量级；须大于最大停泊深度 200m 的距离量，保证「同 P 远配套站台」
        /// 仍优先于「隔壁普通工作台」）。</summary>
        private const float UnsuitableStationPenalty = 1000f;

        /// <summary>GetStationRespawnPriority 后缀（配套站台优先）：净盘
        /// 对非归还台加惩罚——原版锅具/搅拌碗已有配套约束与惩罚、脏盘叠已限定
        /// 归还台集合，唯独净盘纯按距离（可能落普通工作台而非归还台）。归还台
        /// 不可用时（满/拼盘步骤不匹配）CanRespawnOnStation 已排除，自然落回
        /// 普通工作台。参数名 _station 与宿主目标方法一致（Harmony 按名注入）。</summary>
        private static void GetStationRespawnPriorityPostfix(object __instance, object _station, ref float __result)
        {
            if (s_sceneHasSlots == 0)
                return;
            try
            {
                var cleanPlate = GameApi.CleanPlateUtensilRespawnType;
                var plateReturn = GameApi.ServerPlateReturnStationType;
                if (cleanPlate == null || plateReturn == null)
                    return;
                if (!cleanPlate.IsInstanceOfType(__instance))
                    return;
                var c = _station as Component;
                if (c == null)
                    return;
                if (c.GetComponent(plateReturn) == null)
                    __result += UnsuitableStationPenalty;
            }
            catch (Exception ex)
            {
                if (!s_postfixWarned)
                {
                    s_postfixWarned = true;
                    StubLog.LogWarn("[CustomStub] 分 P 道具重生守卫优先级后缀异常（放行原结果=原版行为）: " + ex.Message);
                }
            }
        }

        /// <summary>站台是否位于「非在场 slot」子树：沿父链找 Design/PartSlots/&lt;pid&gt;
        /// 的 slot 根；在场 = 激活且 Animator 处于 Idle_Active 定持态（Entry/Exit/
        /// Idle_Park 均非在场——转场途中两侧都不可选，重生重试循环自然等到新 P
        /// 就位）。Animator 缺失/未启用时按 park 偏移阈值兜底。不在任何 slot 下
        /// （base 层站台）恒 false。</summary>
        private static bool IsUnderNonActivePartSlot(Component c)
        {
            if (c == null)
                return false;
            var t = c.transform;
            while (t != null)
            {
                var p = t.parent;
                if (p != null && p.name == "PartSlots")
                {
                    var gp = p.parent;
                    if (gp == null || gp.name != "Design")
                        return false;
                    if (!t.gameObject.activeInHierarchy)
                        return true;
                    var anim = t.GetComponent<Animator>();
                    if (anim != null && anim.enabled && anim.runtimeAnimatorController != null)
                        return !anim.GetCurrentAnimatorStateInfo(0).IsName("Idle_Active");
                    var lp = t.localPosition;
                    return lp.sqrMagnitude > InPlaceThresholdSqr;
                }
                t = p;
            }
            return false;
        }
    }
}
