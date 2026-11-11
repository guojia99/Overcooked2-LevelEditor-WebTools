using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace CustomStub
{
    /// <summary>可上菜传送带站（commonW5 的 web_conveyor_station_serve）。
    ///
    /// 运行时不引用 Assembly-CSharp 类型（asmdef 只依赖 LevelEditorStub），全部通过反射：
    ///  - 在伪根子物体上解析 ServerConveyorStation / ServerAttachStation；
    ///  - 传送带空闲且自身 attach 有物品时，沿【运动方向正前方】找上菜台（ServerPlateStation）：
    ///    上菜口（attach point）须落在正前方 ~1 格内、横向对齐、高度差 ≤1m（同一格）；
    ///  - 等待 0.5 / |GetConveySpeed()| 秒后取走物品并 AddItem 到上菜台；
    ///  - 非盘子的无效上菜由 HarmonyPatches 的 ServerPlateStation.OnItemAdded 前缀接管
    ///    （ReportInvalidServe：断小费/连击 + 发失败事件 + 次帧消耗物品）。
    ///
    /// 该组件只挂在可上菜传送带的伪根上（tag ConveyorServe| 或 ConveyorServe|dir），
    /// 普通传送带没有它，行为完全不变。</summary>
    public class ConveyorServe : MonoBehaviour
    {
        public const string TagPrefix = "ConveyorServe|";

        [SerializeField]
        internal bool m_enabled = true;

        /// <summary>本次 AddItem 是否由可上菜传送带发起；Harmony 前缀只在此窗口内接管非盘子。</summary>
        internal static bool TransportInProgress;

        /// <summary>非盘子拦截补丁是否已装；未装时拒绝把非盘子塞进上菜台（避免宿主 NRE）。</summary>
        internal static bool PatchesReady;

        private bool m_transferring;
        private float m_bootTime;
        private float m_nextResolveAt;
        private float m_nextTargetAt;

        private Component m_serverConveyor;
        private Component m_conveyorAttach;
        private Component m_selfReceiver;
        private Component m_plateStation;
        private Component m_plateAttach;

        private Type m_serverAttachType;
        private Type m_plateStationType;
        private Type m_gridIndexType;
        private PropertyInfo m_gridX;
        private PropertyInfo m_gridZ;

        private MethodInfo m_isConveying;
        private MethodInfo m_getNextGridIndex;
        private MethodInfo m_getConveySpeed;
        private MethodInfo m_takeResponsibility;
        private MethodInfo m_inspectItem;
        private MethodInfo m_hasItem;
        private MethodInfo m_takeItem;
        private MethodInfo m_addItem;
        private MethodInfo m_isReceiving;
        private MethodInfo m_updateAdjacentReceiver;
        private MethodInfo m_getGridFromPos;
        private MethodInfo m_getAttachPoint;
        private FieldInfo m_gridManagerField;
        private FieldInfo m_selfReceiverField;
        private object m_defaultContext;

        /// <summary>上菜台缓存（约 1s TTL）——热路径不做高频 FindObjectsOfType。</summary>
        private UnityEngine.Object[] m_plateCache;
        private float m_nextPlateScanAt;
        /// <summary>诊断节流（常开）：未命中上菜台每 2s 一次；命中目标变化时一次。</summary>
        private float m_nextDiagAt;
        private string m_lastDiagTarget;

        /// <summary>几何判定常量（实测两种 attach point 均在本格中心 (0,0.5,0)）：
        ///  仅接受【相邻格】上菜台——上菜口距传送带落点约 1 格（≈1.2m，即前沿外 ≤0.6m），
        ///  横向 ≤0.8m（半格 0.6 留浮点/摆放容差；2×1 上菜台上菜口在跨度中心，喂任一侧都是
        ///  正 0.6m 左右），高度差 ≤1m（同一格）。</summary>
        private const float MinForward = 0.3f;
        private const float MaxForward = 1.3f;
        private const float MaxLateral = 0.8f;
        private const float MaxHeight = 1.0f;

        private Vector3 m_lastPosition;
        private Quaternion m_lastRotation;
        private bool m_haveLastPose;

        private void Start()
        {
            m_bootTime = Time.time;
        }

        private void Update()
        {
            if (!m_enabled || m_transferring)
                return;
            if (Time.time < m_bootTime + 0.3f)
                return;
            if (!GameApi.IsServerMachine())
                return;
            if (!ResolveConveyor())
                return;
            if (m_serverConveyor == null || m_conveyorAttach == null)
                return;

            RefreshAdjacentOnMove();
            RefreshSelfReceiver();

            try
            {
                if (m_isConveying != null && (bool)m_isConveying.Invoke(m_serverConveyor, null))
                    return;
                if (m_selfReceiver != null && m_isReceiving != null && (bool)m_isReceiving.Invoke(m_selfReceiver, null))
                    return;
                if (m_hasItem == null || !(bool)m_hasItem.Invoke(m_conveyorAttach, null))
                    return;
                if (m_inspectItem == null)
                    return;

                var item = m_inspectItem.Invoke(m_conveyorAttach, null) as GameObject;
                if (item == null)
                    return;

                if (Time.time < m_nextTargetAt)
                    return;
                m_nextTargetAt = Time.time + 0.2f;

                m_plateStation = FindPlateStation(item);
                if (m_plateStation == null)
                    return;

                m_plateAttach = GameApi.GetComponent(m_plateStation.gameObject, m_serverAttachType);
                if (m_plateAttach == null)
                    return;
                if (m_addItem == null)
                    return;
                // 上菜口为空才送（HasItem == true 表示已有物品，等它空出来）。
                if ((bool)m_hasItem.Invoke(m_plateAttach, null))
                    return;

                float speed = 0.5f;
                if (m_getConveySpeed != null)
                {
                    object raw = m_getConveySpeed.Invoke(m_serverConveyor, null);
                    if (raw != null)
                        speed = Convert.ToSingle(raw);
                }
                StartCoroutine(Transfer(item, m_plateAttach, speed));
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[ConveyorServe] Update 异常（下帧重试）: " + ex.Message);
            }
        }

        private bool ResolveConveyor()
        {
            if (m_serverConveyor != null && m_conveyorAttach != null)
                return true;
            if (Time.time < m_nextResolveAt)
                return false;
            m_nextResolveAt = Time.time + 0.5f;

            try
            {
                var conveyorType = GameApi.Find("ServerConveyorStation");
                if (conveyorType == null)
                    return false;
                m_serverAttachType = GameApi.Find("ServerAttachStation");
                m_plateStationType = GameApi.Find("ServerPlateStation");
                m_gridIndexType = GameApi.Find("GridIndex");
                if (m_serverAttachType == null || m_plateStationType == null)
                    return false;

                if (m_gridIndexType != null)
                {
                    m_gridX = m_gridIndexType.GetProperty("X", BindingFlags.Instance | BindingFlags.Public);
                    m_gridZ = m_gridIndexType.GetProperty("Z", BindingFlags.Instance | BindingFlags.Public);
                }

                var components = GetComponentsInChildren<Component>(true);
                for (int i = 0; i < components.Length; i++)
                {
                    var c = components[i];
                    if (c == null)
                        continue;
                    if (c.GetType().FullName == "ServerConveyorStation")
                    {
                        m_serverConveyor = c;
                        break;
                    }
                }
                if (m_serverConveyor == null)
                    return false;

                m_conveyorAttach = GameApi.GetComponent(m_serverConveyor.gameObject, m_serverAttachType);
                if (m_conveyorAttach == null)
                {
                    var parent = m_serverConveyor.GetComponentInParent(m_serverAttachType);
                    m_conveyorAttach = parent;
                }
                if (m_conveyorAttach == null)
                {
                    m_serverConveyor = null;
                    return false;
                }

                var conveyorType2 = m_serverConveyor.GetType();
                m_isConveying = FindMethodUp(conveyorType2, "IsConveying", 0);
                m_getNextGridIndex = FindMethodUp(conveyorType2, "GetNextGridIndex", 0);
                m_getConveySpeed = FindMethodUp(conveyorType2, "GetConveySpeed", 0);
                m_takeResponsibility = FindMethodUp(conveyorType2, "TakeResponsibilityForItem", 0);
                m_updateAdjacentReceiver = FindMethodUp(conveyorType2, "UpdateAdjacentReceiver", 0);
                m_inspectItem = FindMethodUp(m_conveyorAttach.GetType(), "InspectItem", 0);
                m_hasItem = FindMethodUp(m_conveyorAttach.GetType(), "HasItem", 0);
                m_takeItem = FindMethodUp(m_conveyorAttach.GetType(), "TakeItem", 0);
                m_getAttachPoint = FindMethodUp(m_serverAttachType, "GetAttachPoint", 1);
                m_addItem = FindMethodUp(m_conveyorAttach.GetType(), "AddItem", 3);
                if (m_addItem == null)
                    m_addItem = FindMethodUp(m_conveyorAttach.GetType(), "AddItem", 2);
                if (m_addItem != null)
                {
                    var ps = m_addItem.GetParameters();
                    if (ps.Length == 3 && ps[2].ParameterType.IsValueType)
                        m_defaultContext = Activator.CreateInstance(ps[2].ParameterType);
                }

                m_gridManagerField = GetFieldUp(conveyorType2, "m_gridManager");
                m_selfReceiverField = GetFieldUp(conveyorType2, "m_selfReceiver");
                if (m_selfReceiverField != null)
                    m_selfReceiver = m_selfReceiverField.GetValue(m_serverConveyor) as Component;
                if (m_selfReceiver != null)
                    m_isReceiving = FindMethodUp(m_selfReceiver.GetType(), "IsReceiving", 0);

                if (m_gridManagerField != null)
                {
                    var gm = m_gridManagerField.GetValue(m_serverConveyor);
                    if (gm != null)
                    {
                        var gmType = gm.GetType();
                        m_getGridFromPos = FindMethodUp(gmType, "GetGridLocationFromPos", 1);
                    }
                }

                m_lastPosition = m_serverConveyor.transform.position;
                m_lastRotation = m_serverConveyor.transform.rotation;
                m_haveLastPose = true;

                StubLog.Log("[ConveyorServe] 已解析传送带: " + m_serverConveyor.name);
                return true;
            }
            catch (Exception ex)
            {
                m_serverConveyor = null;
                m_conveyorAttach = null;
                StubLog.LogWarn("[ConveyorServe] 解析传送带失败: " + ex.Message);
                return false;
            }
        }

        private void RefreshSelfReceiver()
        {
            if (m_selfReceiver != null || m_selfReceiverField == null || m_serverConveyor == null)
                return;
            var receiver = m_selfReceiverField.GetValue(m_serverConveyor) as Component;
            if (receiver == null)
                return;
            m_selfReceiver = receiver;
            m_isReceiving = FindMethodUp(receiver.GetType(), "IsReceiving", 0);
        }

        private void RefreshAdjacentOnMove()
        {
            if (m_serverConveyor == null || m_updateAdjacentReceiver == null)
                return;
            var t = m_serverConveyor.transform;
            if (m_haveLastPose && t.position == m_lastPosition && t.rotation == m_lastRotation)
                return;
            m_lastPosition = t.position;
            m_lastRotation = t.rotation;
            m_haveLastPose = true;
            try
            {
                m_updateAdjacentReceiver.Invoke(m_serverConveyor, null);
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[ConveyorServe] 刷新相邻目标失败: " + ex.Message);
            }
        }

        /// <summary>沿传送方向正前方找【相邻格】上菜台：取上菜口
        ///  （ServerAttachStation.GetAttachPoint）落在运动方向正前方约 1 格
        ///  [MinForward, MaxForward]（≈1.2m，即前沿外 ≤0.6m）、横向 ≤MaxLateral（0.6m，
        ///  直接对齐）、与传送带落点高度差 ≤MaxHeight（同一格内，1m）的最近者。
        ///  方向背离 / 非相邻 / 跨层的上菜台一律不算。</summary>
        private Component FindPlateStation(GameObject item)
        {
            if (m_plateStationType == null)
                return null;

            Vector2 dir = OutputDirectionXZ();
            if (dir.sqrMagnitude < 0.0001f)
                return null;

            Vector3 anchor = ConveyorAnchor(item);
            var plates = GetPlateStations();
            Component best = null;
            float bestScore = float.MaxValue;
            float bestFwd = 0f, bestLat = 0f;
            Component near = null;
            float nearAbsFwd = float.MaxValue;
            float nearFwd = 0f, nearLat = 0f, nearH = 0f;
            for (int i = 0; i < plates.Length; i++)
            {
                var c = plates[i] as Component;
                if (c == null)
                    continue;

                Vector3 port = PlatePort(c, item);
                float dx = port.x - anchor.x;
                float dz = port.z - anchor.z;
                float forward = dx * dir.x + dz * dir.y;
                float lateral = Mathf.Abs(dx * -dir.y + dz * dir.x);
                float height = Mathf.Abs(port.y - anchor.y);
                if (Mathf.Abs(forward) < nearAbsFwd)
                {
                    nearAbsFwd = Mathf.Abs(forward);
                    near = c;
                    nearFwd = forward;
                    nearLat = lateral;
                    nearH = height;
                }
                if (forward < MinForward || forward > MaxForward)
                    continue;
                if (lateral > MaxLateral)
                    continue;
                if (height > MaxHeight)
                    continue;

                float score = forward + lateral * 2f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = c;
                    bestFwd = forward;
                    bestLat = lateral;
                }
            }

            if (best != null)
            {
                string tn = best.name;
                if (tn != m_lastDiagTarget)
                {
                    m_lastDiagTarget = tn;
                    StubLog.Log("[ConveyorServe] " + name + " 命中上菜台: " + tn +
                        " fwd=" + bestFwd.ToString("0.##") + " lat=" + bestLat.ToString("0.##"));
                }
            }
            else if (Time.time >= m_nextDiagAt)
            {
                m_nextDiagAt = Time.time + 2f;
                m_lastDiagTarget = null;
                string nearTxt = near != null
                    ? (" 最近 " + near.name + " fwd=" + nearFwd.ToString("0.##") +
                       " lat=" + nearLat.ToString("0.##") + " h=" + nearH.ToString("0.##"))
                    : " 无上菜台";
                StubLog.LogWarn("[ConveyorServe] " + name + " 未命中上菜台: dir=(" +
                    dir.x.ToString("0.##") + "," + dir.y.ToString("0.##") + ") anchor=(" +
                    anchor.x.ToString("0.##") + "," + anchor.y.ToString("0.##") + "," +
                    anchor.z.ToString("0.##") + ") 上菜台数=" + plates.Length + nearTxt);
            }
            return best;
        }

        /// <summary>传送带输出方向（世界 XZ，单位化）：优先按真实「下一格」差
        ///  （= 原版 GetNextGridIndex 采用的方向，尊重 Leftwards/Rightwards 与旋转）；
        ///  取不到时退化为站体 -right（Rightwards 同向，与皮带模型一致）。</summary>
        private Vector2 OutputDirectionXZ()
        {
            if (m_getNextGridIndex != null && m_getGridFromPos != null
                && m_gridManagerField != null && m_serverConveyor != null
                && m_gridX != null && m_gridZ != null)
            {
                object gridManager = m_gridManagerField.GetValue(m_serverConveyor);
                if (gridManager != null)
                {
                    object next = null;
                    object current = null;
                    try { next = m_getNextGridIndex.Invoke(m_serverConveyor, null); }
                    catch (Exception) { next = null; }
                    try { current = m_getGridFromPos.Invoke(gridManager, new object[] { m_serverConveyor.transform.position }); }
                    catch (Exception) { current = null; }
                    if (next != null && current != null)
                    {
                        int dx = ReadInt(m_gridX, next) - ReadInt(m_gridX, current);
                        int dz = ReadInt(m_gridZ, next) - ReadInt(m_gridZ, current);
                        if (dx != 0 || dz != 0)
                            return new Vector2(dx, dz).normalized;
                    }
                }
            }

            var right = m_serverConveyor != null ? m_serverConveyor.transform.right : transform.right;
            var v = new Vector2(-right.x, -right.z);
            var cardinal = new Vector2(Mathf.Round(v.x), Mathf.Round(v.y));
            if (cardinal.sqrMagnitude > 0.01f)
                return cardinal.normalized;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector2.zero;
        }

        /// <summary>传送带物品落点（attach point）世界坐标；取不到退回站体位置。</summary>
        private Vector3 ConveyorAnchor(GameObject item)
        {
            if (m_conveyorAttach != null && m_getAttachPoint != null)
            {
                try
                {
                    var t = m_getAttachPoint.Invoke(m_conveyorAttach, new object[] { item }) as Transform;
                    if (t != null)
                        return t.position;
                }
                catch (Exception) { }
            }
            return m_serverConveyor != null ? m_serverConveyor.transform.position : transform.position;
        }

        /// <summary>上菜台上菜口（attach point）世界坐标；取不到退回上菜台自身位置。</summary>
        private Vector3 PlatePort(Component plate, GameObject item)
        {
            if (plate != null && m_getAttachPoint != null)
            {
                var attach = GameApi.GetComponent(plate.gameObject, m_serverAttachType);
                if (attach == null)
                    attach = GameApi.GetComponentInParent(plate, m_serverAttachType);
                if (attach != null)
                {
                    try
                    {
                        var t = m_getAttachPoint.Invoke(attach, new object[] { item }) as Transform;
                        if (t != null)
                            return t.position;
                    }
                    catch (Exception) { }
                }
            }
            return plate != null ? plate.transform.position : Vector3.zero;
        }

        /// <summary>上菜台列表（约 1s TTL 缓存，热路径不做高频 FindObjectsOfType）。</summary>
        private UnityEngine.Object[] GetPlateStations()
        {
            if (m_plateCache != null && Time.time < m_nextPlateScanAt)
                return m_plateCache;
            m_nextPlateScanAt = Time.time + 1f;
            m_plateCache = GameApi.FindAll(m_plateStationType);
            return m_plateCache;
        }

        private int ReadInt(PropertyInfo p, object index)
        {
            if (p == null || index == null)
                return 0;
            object v = p.GetValue(index, null);
            return v == null ? 0 : Convert.ToInt32(v);
        }

        private IEnumerator Transfer(GameObject item, Component plateAttach, float speed)
        {
            m_transferring = true;
            yield return null;

            float absSpeed = Mathf.Abs(speed);
            if (absSpeed < 0.01f)
            {
                // 原版 0/负速度不会推进（prop 不增长），这里同样不投递，避免 50s 假等待。
                WarnOnce("conveyorserve-speed", "[ConveyorServe] 传送带速度 <= 0，跳过本次投递。");
                m_transferring = false;
                yield break;
            }
            yield return new WaitForSeconds(0.5f / absSpeed);

            if (item == null || m_conveyorAttach == null || plateAttach == null)
            {
                m_transferring = false;
                yield break;
            }

            GameObject current = null;
            try
            {
                current = m_inspectItem.Invoke(m_conveyorAttach, null) as GameObject;
            }
            catch (Exception)
            {
                current = null;
            }
            if (current != item)
            {
                m_transferring = false;
                yield break;
            }

            bool plateBusy = false;
            try
            {
                plateBusy = (bool)m_hasItem.Invoke(plateAttach, null);
            }
            catch (Exception)
            {
                plateBusy = true;
            }
            if (plateBusy)
            {
                m_transferring = false;
                yield break;
            }

            if (m_takeResponsibility == null || m_addItem == null)
            {
                m_transferring = false;
                yield break;
            }

            if (!PatchesReady)
            {
                var plateType = GameApi.Find("ServerPlate");
                if (plateType == null || GameApi.GetComponent(current, plateType) == null)
                {
                    WarnOnce("conveyorserve-nopatch",
                        "[ConveyorServe] 非盘子拦截补丁未安装，拒绝送入上菜台（物品留在传送带上）。");
                    m_transferring = false;
                    yield break;
                }
            }

            GameObject taken = null;
            try
            {
                taken = m_takeResponsibility.Invoke(m_serverConveyor, null) as GameObject;
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[ConveyorServe] 取走物品失败: " + ex.Message);
            }
            if (taken == null)
            {
                // 取不下来就不要硬塞，避免物品同时挂在传送带和上菜台上。
                m_transferring = false;
                yield break;
            }

            TransportInProgress = true;
            try
            {
                Vector3 fwd = taken.transform.forward;
                StubLog.Log("[ConveyorServe] " + name + " 送入上菜台: " +
                    (plateAttach != null ? plateAttach.gameObject.name : "?") + " item=" + taken.name);
                m_addItem.Invoke(plateAttach, new object[] { taken, new Vector2(fwd.x, fwd.z), m_defaultContext });
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[ConveyorServe] 送入上菜台失败: " + ex.Message);
            }
            finally
            {
                TransportInProgress = false;
            }

            m_transferring = false;
        }

        /// <summary>非盘子被送进上菜台时调用：断小费/连击 + 发失败事件 + 次帧消耗物品。</summary>
        internal static void ReportInvalidServe(MonoBehaviour station, GameObject item)
        {
            if (station == null || item == null)
                return;

            try
            {
                FieldInfo handlerField = GetFieldUp(station.GetType(), "m_orderHandler");
                object handler = handlerField != null ? handlerField.GetValue(station) : null;
                bool failed = false;
                if (handler != null)
                {
                    MethodInfo fail = FindMethodOneParam(handler.GetType(), "OnFailedDelivery");
                    if (fail != null)
                    {
                        fail.Invoke(handler, new object[] { station });
                        failed = true;
                    }
                }
                if (!failed)
                {
                    WarnOnce("conveyorserve-failmethod",
                        "[ConveyorServe] 订单处理器缺少 OnFailedDelivery，非盘子只做消耗，不再重置小费。");
                }

                MethodInfo send = FindMethodUp(station.GetType(), "SendDeliverEvent", 2);
                if (send != null)
                {
                    try
                    {
                        send.Invoke(station, new object[] { item, false });
                    }
                    catch (Exception ex)
                    {
                        // PlateStationMessage.Serialise 对未注册物品没有空检查，忽略即可。
                        StubLog.Dbg("[ConveyorServe] 发送失败上菜事件抛异常（忽略）: " + ex.Message);
                    }
                }

                station.StartCoroutine(ConsumeNextFrame(station, item));
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[ConveyorServe] 无效上菜处理失败: " + ex.Message);
                try
                {
                    station.StartCoroutine(ConsumeNextFrame(station, item));
                }
                catch (Exception)
                {
                }
            }
        }

        private static IEnumerator ConsumeNextFrame(MonoBehaviour station, GameObject item)
        {
            yield return null;
            if (item == null)
                yield break;

            try
            {
                var attachType = GameApi.Find("ServerAttachStation");
                if (attachType == null)
                    yield break;
                var attach = GameApi.GetComponent(station.gameObject, attachType);
                if (attach == null)
                    yield break;

                var take = attach.GetType().GetMethod("TakeItem", BindingFlags.Instance | BindingFlags.Public);
                if (take == null)
                    yield break;

                var taken = take.Invoke(attach, null) as GameObject;
                // 玩家可能已把物品拿走，或上菜台已空；此时不要再消耗任何东西。
                if (taken == null)
                    yield break;
                item = taken;

                var netType = GameApi.Find("NetworkUtils");
                if (netType != null)
                {
                    var destroy = netType.GetMethod("DestroyObject", BindingFlags.Static | BindingFlags.Public);
                    if (destroy != null)
                    {
                        destroy.Invoke(null, new object[] { item });
                        yield break;
                    }
                }
            }
            catch (Exception ex)
            {
                StubLog.LogWarn("[ConveyorServe] 消耗无效上菜物品失败: " + ex.Message);
            }

            if (item != null)
                item.SetActive(false);
        }

        private static FieldInfo GetFieldUp(Type type, string name)
        {
            while (type != null)
            {
                var f = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null)
                    return f;
                type = type.BaseType;
            }
            return null;
        }

        private static MethodInfo FindMethodUp(Type type, string name, int parameterCount)
        {
            while (type != null)
            {
                var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (int i = 0; i < methods.Length; i++)
                {
                    var m = methods[i];
                    if (m.Name == name && m.GetParameters().Length == parameterCount)
                        return m;
                }
                type = type.BaseType;
            }
            return null;
        }

        private static MethodInfo FindMethodOneParam(Type type, string name)
        {
            while (type != null)
            {
                var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (int i = 0; i < methods.Length; i++)
                {
                    var m = methods[i];
                    if (m.Name == name && m.GetParameters().Length == 1)
                        return m;
                }
                type = type.BaseType;
            }
            return null;
        }

        private static readonly System.Collections.Generic.HashSet<string> s_warned =
            new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);

        internal static void WarnOnce(string key, string message)
        {
            if (!s_warned.Add(key))
                return;
            StubLog.LogWarn(message);
        }
    }
}
