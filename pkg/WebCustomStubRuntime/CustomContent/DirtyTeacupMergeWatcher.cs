using System;
using System.Collections;
using System.Reflection;
using LevelEditorStub;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 单个脏茶杯（web_teacup_dirty，tag DirtyTeacup|）合并监视。
    ///
    /// 用户规则：单个时拿单个，一堆时拿一堆；拿着一个放到另一堆（或另一个）上
    /// 就合并为一堆，然后再可整堆拿。
    ///  - 单杯 → 脏茶杯堆：Harmony 补丁（HarmonyPatches.ServerDirtyPlateStack
    ///    CanHandlePlacement/HandlePlacement 前缀，EntryPoint.EnsureDirtyTeacupPatches）。
    ///  - 单杯 + 单杯：本组件服务端轮询，两杯贴近且都已落定（非手持/不在堆内）
    ///    → 生成脏茶杯堆（NetworkUtils.ServerSpawnPrefab + AddToStack×2），
    ///    两杯按 NetworkUtils.DestroyObject 联机安全销毁。
    ///
    /// 挂载：web_teacup_dirty.prefab 内嵌（编辑器直接生效）；真机经 DirtyTeacup|
    /// tag 由 EntryPoint 自愈补挂。m_stackPrefab 序列化直引 web_teacup_stack_dirty。
    /// </summary>
    public class DirtyTeacupMergeWatcher : MonoBehaviour
    {
        /** 合并生成的脏茶杯堆 prefab（web_teacup_stack_dirty）。 */
        public GameObject m_stackPrefab;

        /// <summary>tag 载体前缀（EntryPoint 自愈 + Harmony 按需安装共用约定）。</summary>
        public const string TagPrefix = "DirtyTeacup|";

        private const float MergeDistance = 0.3f;
        private const float PollInterval = 0.5f;

        private static readonly MethodInfo s_spawnMethod = GameApi.NetworkUtilsSpawnPrefabMethod;
        private static readonly MethodInfo s_destroyMethod = GameApi.NetworkUtilsDestroyObjectMethod;
        private static Type s_carrierType;
        private static MethodInfo s_inspectMethod;

        private IEnumerator Start()
        {
            // 仅服务端权威合并（生成/销毁经 vanilla 实体消息同步各端）
            while (true)
            {
                yield return new WaitForSeconds(PollInterval);
                if (!GameApi.IsServerMachine())
                    continue;
                try
                {
                    MergeStep();
                }
                catch (Exception ex)
                {
                    StubLog.LogWarn("[DirtyTeacupMergeWatcher] 合并异常: " + ex.Message);
                }
            }
        }

        private void MergeStep()
        {
            if (m_stackPrefab == null || s_spawnMethod == null || s_destroyMethod == null)
                return;
            var cups = CollectCups();
            for (int i = 0; i < cups.Count; i++)
            {
                for (int j = i + 1; j < cups.Count; j++)
                {
                    var a = cups[i];
                    var b = cups[j];
                    if (a == null || b == null)
                        continue;
                    var pa = a.transform.position;
                    var pb = b.transform.position;
                    if (Mathf.Abs(pa.x - pb.x) > MergeDistance ||
                        Mathf.Abs(pa.z - pb.z) > MergeDistance ||
                        Mathf.Abs(pa.y - pb.y) > MergeDistance)
                        continue;
                    Merge(a, b);
                    return; // 每轮最多合并一对，下轮继续
                }
            }
        }

        private void Merge(GameObject a, GameObject b)
        {
            var pos = (a.transform.position + b.transform.position) * 0.5f;
            var stack = s_spawnMethod.Invoke(null, new object[]
                { gameObject, m_stackPrefab, pos, Quaternion.identity }) as GameObject;
            if (stack != null)
            {
                var serverStack = stack.GetComponent("ServerDirtyPlateStack");
                if (serverStack != null)
                {
                    var add = serverStack.GetType().GetMethod("AddToStack",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (add != null)
                    {
                        add.Invoke(serverStack, null);
                        add.Invoke(serverStack, null);
                    }
                }
            }
            // 销毁前先“正常移出”所在站点：服务端直接 Destroy 不会走 AttachStation /
            // 杯堆的移除回调，台面的高亮列表和挂载引用都会留下失效对象（玩家靠近
            // 工作台即抛 MissingReferenceException → 卡死、高亮/交互失效）。
            DetachFromStation(a);
            DetachFromStation(b);
            HighlightDetach.BeforeDestroy(a);
            HighlightDetach.BeforeDestroy(b);
            s_destroyMethod.Invoke(null, new object[] { a });
            s_destroyMethod.Invoke(null, new object[] { b });
            StubLog.Dbg("[DirtyTeacupMergeWatcher] 合并两个脏茶杯 → 脏茶杯堆 @ " + pos);
        }

        /// <summary>若 a/b 挂在某个 AttachStation 上，先用原版 TakeItem() 正常摘下
        /// （走 OnItemRemoved → 高亮/挂载引用清理），找不到站点则跳过。</summary>
        private static void DetachFromStation(GameObject go)
        {
            if (go == null)
                return;
            var t = go.transform.parent;
            while (t != null)
            {
                var station = t.GetComponent("ServerAttachStation");
                if (station != null)
                {
                    try
                    {
                        var take = station.GetType().GetMethod("TakeItem",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (take != null)
                            take.Invoke(station, null);
                    }
                    catch (Exception)
                    {
                    }
                    return;
                }
                t = t.parent;
            }
        }

        /// <summary>收集落定（非手持、不在堆内）的单个脏茶杯。</summary>
        private static System.Collections.Generic.List<GameObject> CollectCups()
        {
            var result = new System.Collections.Generic.List<GameObject>();
            var tags = UnityEngine.Object.FindObjectsOfType<SpecificPseudoPrefabTag>();
            foreach (var tag in tags)
            {
                if (tag == null || string.IsNullOrEmpty(tag.prefabTag))
                    continue;
                if (!tag.prefabTag.StartsWith(TagPrefix, StringComparison.Ordinal))
                    continue;
                var go = tag.gameObject;
                if (!go.activeInHierarchy)
                    continue;
                if (IsCarriedOrStacked(go.transform))
                    continue;
                result.Add(go);
            }
            return result;
        }

        /// <summary>手持中（祖先含玩家载体）或在某个堆/站点内（祖先含 Stack）。</summary>
        private static bool IsCarriedOrStacked(Transform t)
        {
            while (t != null)
            {
                if (t.GetComponent("ServerPlayerAttachmentCarrier") != null ||
                    t.GetComponent("ClientPlayerAttachmentCarrier") != null)
                    return true;
                if (t.GetComponent("Stack") != null)
                    return true;
                t = t.parent;
            }
            return false;
        }

        /// <summary>解析 ICarrier.InspectCarriedItem。注意 InspectCarriedItem 声明在
        /// 父接口 ICarrierPlacement 上——Type.GetMethod 对【接口】不会遍历继承的接口，
        /// 直接 GetMethod 会永久解析失败（2026-10-28 现场实证：单杯放不进水槽）。
        /// </summary>
        private static bool EnsureCarrierReflection()
        {
            if (s_carrierType == null)
            {
                s_carrierType = GameApi.Find("ICarrier");
                s_inspectMethod = FindMethodIncludingInheritedInterfaces(s_carrierType, "InspectCarriedItem");
            }
            return s_inspectMethod != null;
        }

        private static MethodInfo FindMethodIncludingInheritedInterfaces(Type type, string methodName)
        {
            if (type == null)
                return null;
            var m = type.GetMethod(methodName);
            if (m != null)
                return m;
            var ifaces = type.GetInterfaces();
            for (int i = 0; i < ifaces.Length; i++)
            {
                m = ifaces[i].GetMethod(methodName);
                if (m != null)
                    return m;
            }
            return null;
        }

        /// <summary>Harmony 用：判定「携带物是本族单杯（非堆）且目标堆可接收」。
        /// 命中返回携带物 GameObject，否则 null（放行原版逻辑）。</summary>
        internal static GameObject MatchSingleCup(Component serverStackComp, object carrier)
        {
            try
            {
                if (serverStackComp == null || carrier == null)
                    return null;
                if (!EnsureCarrierReflection())
                    return null;
                var carried = s_inspectMethod.Invoke(carrier, null) as GameObject;
                if (carried == null)
                    return null;
                // 携带物是堆（带 ServerStack）→ 原版合并路径，放行
                if (carried.GetComponent("ServerStack") != null)
                    return null;
                // 携带物必须是本族单杯
                var tag = carried.GetComponent<SpecificPseudoPrefabTag>();
                if (tag == null || string.IsNullOrEmpty(tag.prefabTag) ||
                    !tag.prefabTag.StartsWith(TagPrefix, StringComparison.Ordinal))
                    return null;
                // 目标堆的 platePrefab 必须是本族单杯（防混族合并）
                var dirtyStack = FindFieldInHierarchy(serverStackComp, "m_plateStack");
                var stackBase = dirtyStack as Component;
                if (stackBase == null)
                    return null;
                var platePrefab = FindFieldInHierarchy(stackBase, "m_platePrefab") as GameObject;
                if (platePrefab == null || !platePrefab.name.StartsWith("web_teacup_dirty", StringComparison.Ordinal))
                    return null;
                return carried;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>读携带物（供 AttachStation 拦截前缀复用）。</summary>
        internal static GameObject InspectCarried(object carrier)
        {
            try
            {
                if (carrier == null || !EnsureCarrierReflection())
                    return null;
                return s_inspectMethod.Invoke(carrier, null) as GameObject;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>携带物堆大小；单只可携带物返回 1。</summary>
        internal static int GetCarriedStackSize(GameObject carried)
        {
            if (carried == null)
                return 0;
            var serverStack = carried.GetComponent("ServerStack") as Component;
            if (serverStack == null)
                return 1;
            try
            {
                var getSize = serverStack.GetType().GetMethod("GetSize",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Instance);
                if (getSize != null)
                    return Convert.ToInt32(getSize.Invoke(serverStack, null));
            }
            catch (Exception)
            {
            }
            return 1;
        }

        /// <summary>是否本族脏茶杯（单杯 tag 或脏茶杯堆）。</summary>
        internal static bool IsDirtyTeacupItem(GameObject go)
        {
            if (go == null)
                return false;
            var tag = go.GetComponent<SpecificPseudoPrefabTag>();
            if (tag != null && !string.IsNullOrEmpty(tag.prefabTag)
                && tag.prefabTag.StartsWith(TagPrefix, StringComparison.Ordinal))
                return true;
            var dirtyStack = go.GetComponent("DirtyPlateStack") as Component;
            if (dirtyStack == null)
                return false;
            var platePrefab = FindFieldInHierarchy(dirtyStack, "m_platePrefab") as GameObject;
            if (platePrefab != null && platePrefab.name.StartsWith("web_teacup_dirty", StringComparison.Ordinal))
                return true;
            var washedPrefab = FindFieldInHierarchy(dirtyStack, "m_washedPrefab") as GameObject;
            return washedPrefab != null &&
                washedPrefab.name.StartsWith("web_teacup_stack_clean", StringComparison.Ordinal);
        }

        /// <summary>该 AttachStation 是否属于茶杯水槽（洗涤台面或烘干位）。
        /// 仅用于禁止把脏茶杯放到水槽台面上；茶杯回收台的干净堆不在此列。</summary>
        internal static bool IsTeacupSinkAttachStation(Component station)
        {
            if (station == null)
                return false;
            // 洗涤台面：同一 GameObject 上有 WashingStation，烘干位模板是茶杯堆。
            var washing = station.GetComponent("WashingStation") as Component;
            if (washing != null)
            {
                var drying = FindFieldInHierarchy(washing, "m_dryingStation");
                if (drying != null)
                {
                    var stackPrefab = FindFieldInHierarchy(drying, "m_stackPrefab") as GameObject;
                    if (stackPrefab != null &&
                        stackPrefab.name.StartsWith("web_teacup_stack_clean", StringComparison.Ordinal))
                        return true;
                }
            }
            // 水槽烘干位：同一 GameObject 上有 PlateReturnStation，且父链是
            // web_sink_teacup（不能只凭干净堆判断：茶杯回收台 returnClean=true
            // 也有干净堆，必须放行）。
            var dryingReturn = station.GetComponent("PlateReturnStation") as Component;
            if (dryingReturn != null)
            {
                var stackPrefab = FindFieldInHierarchy(dryingReturn, "m_stackPrefab") as GameObject;
                if (stackPrefab == null ||
                    !stackPrefab.name.StartsWith("web_teacup_stack_clean", StringComparison.Ordinal))
                    return false;
                if (station.GetComponentInParent<TeacupSinkRebuilder>() != null)
                    return true;
                Transform t = station.transform;
                while (t != null)
                {
                    if (!string.IsNullOrEmpty(t.name) &&
                        t.name.StartsWith("web_sink_teacup", StringComparison.Ordinal))
                        return true;
                    t = t.parent;
                }
            }
            return false;
        }

        /// <summary>洗杯台用：目标洗杯台是「茶杯水槽」且携带物是本族单只脏茶杯时，
        /// 返回携带物 GameObject；否则 null（放行原版逻辑）。</summary>
        internal static GameObject MatchSingleCupForSink(Component stationComp, object carrier)
        {
            try
            {
                if (stationComp == null || carrier == null)
                    return null;
                if (!IsTeacupSink(stationComp))
                    return null;
                if (!EnsureCarrierReflection())
                    return null;
                var carried = s_inspectMethod.Invoke(carrier, null) as GameObject;
                if (carried == null)
                    return null;
                if (carried.GetComponent("ServerStack") != null)
                    return null;
                var tag = carried.GetComponent<SpecificPseudoPrefabTag>();
                if (tag == null || string.IsNullOrEmpty(tag.prefabTag) ||
                    !tag.prefabTag.StartsWith(TagPrefix, StringComparison.Ordinal))
                    return null;
                return carried;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>整堆匹配：茶杯水槽 + 携带物是本族脏茶杯堆（DirtyPlateStack.m_washedPrefab
        /// 指向 web_teacup_stack_clean）时返回携带物 GameObject，否则 null（放行原版）。
        /// 必须拦截整堆：原版 ServerWashingStation.HandlePlacement 对整堆会先试烘干 attach 站，
        /// 烘干位空时整堆被挂到烘干台上卡住（OnItemAdded 消耗逻辑不触发）。
        /// 返回值客户端/服务端都成立（DirtyPlateStack 是 prefab 上的客户端组件）。</summary>
        internal static GameObject MatchTeacupStackForSink(Component stationComp, object carrier)
        {
            try
            {
                if (stationComp == null || carrier == null)
                    return null;
                if (!IsTeacupSink(stationComp))
                    return null;
                if (!EnsureCarrierReflection())
                    return null;
                var carried = s_inspectMethod.Invoke(carrier, null) as GameObject;
                if (carried == null)
                    return null;
                var dirtyStack = carried.GetComponent("DirtyPlateStack") as Component;
                if (dirtyStack == null)
                    return null;
                var washedPrefab = FindFieldInHierarchy(dirtyStack, "m_washedPrefab") as GameObject;
                bool teacupWashed = washedPrefab != null &&
                    washedPrefab.name.StartsWith("web_teacup_stack_clean", StringComparison.Ordinal);
                if (!teacupWashed)
                {
                    // 兜底：脏堆的洗净产物字段缺失/被改名时，按单杯 prefab 名认族，
                    // 避免整堆脏茶杯完全放不进茶杯水槽。
                    var platePrefab = FindFieldInHierarchy(dirtyStack, "m_platePrefab") as GameObject;
                    if (platePrefab == null ||
                        !platePrefab.name.StartsWith("web_teacup_dirty", StringComparison.Ordinal))
                        return null;
                }
                return carried;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>诊断：洗杯台单杯匹配失败时把【具体原因】打到可见日志（5s 节流）。
        /// 静态走查全通过但现场仍放不进去时，靠这条日志定位是哪一环不满足。
        /// 用 Time.time 节流（每次 Play 自动归零），无需挂 ResetSceneTickers。</summary>
        internal static void LogSinkMatchFailure(Component stationComp, object carrier)
        {
            string reason = DiagnoseSinkMatch(stationComp, carrier);
            if (reason == null)
                return;
            if (Time.time - s_lastSinkDiagTime < 5f)
                return;
            s_lastSinkDiagTime = Time.time;
            StubLog.Log("[DirtyTeacupMergeWatcher] 洗杯台单杯匹配未生效: " + reason);
        }

        private static float s_lastSinkDiagTime = -999f;

        private static string DiagnoseSinkMatch(Component stationComp, object carrier)
        {
            try
            {
                if (stationComp == null)
                    return "stationComp=null";
                if (carrier == null)
                    return "carrier=null";
                var washing = FindFieldInHierarchy(stationComp, "m_washingStation");
                if (washing == null)
                    return "站点无 m_washingStation 字段(type=" + stationComp.GetType().Name + ")";
                var drying = FindFieldInHierarchy(washing, "m_dryingStation");
                if (drying == null)
                    return "WashingStation 无 m_dryingStation 字段";
                var stackPrefab = FindFieldInHierarchy(drying, "m_stackPrefab") as GameObject;
                if (stackPrefab == null)
                    return "烘干位未接线 m_stackPrefab";
                if (!stackPrefab.name.StartsWith("web_teacup_stack_clean", StringComparison.Ordinal))
                    return "非茶杯水槽(烘干模板=" + stackPrefab.name + ")";
                if (!EnsureCarrierReflection())
                    return "ICarrier.InspectCarriedItem 反射解析失败";
                var carried = s_inspectMethod.Invoke(carrier, null) as GameObject;
                if (carried == null)
                    return "手上无携带物";
                if (carried.GetComponent("ServerStack") != null || carried.GetComponent("DirtyPlateStack") != null)
                {
                    var dirtyStackComp = carried.GetComponent("DirtyPlateStack");
                    if (dirtyStackComp != null)
                    {
                        var washed = FindFieldInHierarchy(dirtyStackComp, "m_washedPrefab") as GameObject;
                        return "携带物是整堆(" + carried.name + ", m_washedPrefab=" +
                            (washed != null ? washed.name : "null") + ")，整堆匹配未生效";
                    }
                    return "携带物是整堆(" + carried.name + ")，无 DirtyPlateStack 组件";
                }
                var tag = carried.GetComponent<SpecificPseudoPrefabTag>();
                if (tag == null)
                    return "携带物无 SpecificPseudoPrefabTag(" + carried.name + ")";
                if (string.IsNullOrEmpty(tag.prefabTag) ||
                    !tag.prefabTag.StartsWith(TagPrefix, StringComparison.Ordinal))
                    return "携带物 prefabTag 不匹配(" + carried.name + ", tag=" +
                        (tag.prefabTag != null ? tag.prefabTag : "null") + ")";
                return null;
            }
            catch (Exception ex)
            {
                return "诊断异常: " + ex.Message;
            }
        }

        /// <summary>本洗杯台是否为「茶杯水槽」：烘干位（PlateReturnStation）的杯堆模板
        /// 是 web_teacup_stack_clean。其它 sink（碗/盘子）不受补丁影响。</summary>
        internal static bool IsTeacupSink(Component stationComp)
        {
            var washing = FindFieldInHierarchy(stationComp, "m_washingStation") ?? stationComp;
            var drying = FindFieldInHierarchy(washing, "m_dryingStation");
            var stackPrefab = FindFieldInHierarchy(drying, "m_stackPrefab") as GameObject;
            return stackPrefab != null &&
                stackPrefab.name.StartsWith("web_teacup_stack_clean", StringComparison.Ordinal);
        }

        private static object FindFieldInHierarchy(object obj, string field)
        {
            var t = obj.GetType();
            while (t != null)
            {
                var f = t.GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null)
                    return f.GetValue(obj);
                t = t.BaseType;
            }
            return null;
        }
    }
}
