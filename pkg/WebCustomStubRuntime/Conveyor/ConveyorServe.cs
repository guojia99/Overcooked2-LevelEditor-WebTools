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
    ///  - 传送带空闲且自身 attach 有物品时，沿运动方向找末端上菜台（ServerPlateStation）；
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
        private PropertyInfo m_gridY;
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
        private MethodInfo m_getGridOccupant;
        private MethodInfo m_getPosFromGrid;
        private MethodInfo m_getGridFromPos;
        private FieldInfo m_gridManagerField;
        private FieldInfo m_selfReceiverField;
        private object m_defaultContext;

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

                m_plateStation = FindPlateStation();
                if (m_plateStation == null)
                    return;

                m_plateAttach = GameApi.GetComponent(m_plateStation.gameObject, m_serverAttachType);
                if (m_plateAttach == null)
                    return;
                if (m_addItem == null || !(bool)m_hasItem.Invoke(m_plateAttach, null))
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
                    m_gridY = m_gridIndexType.GetProperty("Y", BindingFlags.Instance | BindingFlags.Public);
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
                        m_getGridOccupant = FindMethodUp(gmType, "GetGridOccupant", 1);
                        m_getPosFromGrid = FindMethodUp(gmType, "GetPosFromGridLocation", 1);
                        m_getGridFromPos = FindMethodUp(gmType, "GetGridLocationFromPos", 1);
                    }
                }

                m_lastPosition = m_serverConveyor.transform.position;
                m_lastRotation = m_serverConveyor.transform.rotation;
                m_haveLastPose = true;

                StubLog.Dbg("[ConveyorServe] 已解析传送带: " + m_serverConveyor.name);
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

        /// <summary>找运动方向末端的上菜台：相邻格 → 沿方向第二格 → 上菜台包围盒跨格兜底。</summary>
        private Component FindPlateStation()
        {
            if (m_getNextGridIndex == null || m_gridManagerField == null || m_getGridOccupant == null)
                return null;

            object gridManager = m_gridManagerField.GetValue(m_serverConveyor);
            if (gridManager == null)
                return null;

            if (m_getGridOccupant == null)
            {
                m_getGridOccupant = FindMethodUp(gridManager.GetType(), "GetGridOccupant", 1);
                m_getPosFromGrid = FindMethodUp(gridManager.GetType(), "GetPosFromGridLocation", 1);
                m_getGridFromPos = FindMethodUp(gridManager.GetType(), "GetGridLocationFromPos", 1);
            }
            if (m_getGridOccupant == null)
                return null;

            object next = m_getNextGridIndex.Invoke(m_serverConveyor, null);
            if (next == null)
                return null;

            var occupant = m_getGridOccupant.Invoke(gridManager, new object[] { next }) as GameObject;
            var plate = FindPlateUp(occupant);
            if (plate != null)
                return plate;

            // 2x1 上菜台可能只占 origin 格：沿运动方向再探一格。
            int dx = 0;
            int dz = 0;
            if (m_getGridFromPos != null && m_gridX != null && m_gridZ != null)
            {
                object current = null;
                try
                {
                    current = m_getGridFromPos.Invoke(gridManager, new object[] { m_serverConveyor.transform.position });
                }
                catch (Exception)
                {
                    current = null;
                }
                if (current != null)
                {
                    dx = ReadInt(m_gridX, next) - ReadInt(m_gridX, current);
                    dz = ReadInt(m_gridZ, next) - ReadInt(m_gridZ, current);
                }
            }
            if (dx == 0 && dz == 0)
            {
                dx = Mathf.RoundToInt(-m_serverConveyor.transform.right.x);
                dz = Mathf.RoundToInt(-m_serverConveyor.transform.right.z);
            }
            if ((dx != 0 || dz != 0) && m_gridX != null && m_gridY != null && m_gridZ != null)
            {
                object second = OffsetIndex(next, dx, dz);
                if (second != null)
                {
                    var occupant2 = m_getGridOccupant.Invoke(gridManager, new object[] { second }) as GameObject;
                    plate = FindPlateUp(occupant2);
                    if (plate != null)
                        return plate;
                }
            }

            // 兜底：按格心与上菜台碰撞体包围盒求交（仅在有物品时调用，带缓存）。
            if (m_getPosFromGrid != null && m_getPosFromGrid.GetParameters().Length == 1)
            {
                object centerRaw = null;
                try
                {
                    centerRaw = m_getPosFromGrid.Invoke(gridManager, new object[] { next });
                }
                catch (Exception)
                {
                    centerRaw = null;
                }
                if (centerRaw is Vector3)
                {
                    var center = (Vector3)centerRaw;
                    var all = GameApi.FindAll(m_plateStationType);
                    for (int i = 0; i < all.Length; i++)
                    {
                        var c = all[i] as Component;
                        if (c == null)
                            continue;
                        if (BoundsCoverXZ(c.gameObject, center))
                            return c;
                    }
                }
            }

            return null;
        }

        private Component FindPlateUp(GameObject go)
        {
            if (go == null)
                return null;
            var direct = GameApi.GetComponent(go, m_plateStationType);
            if (direct != null)
                return direct;
            // 网格占用体可能是上菜台/伪根的父级或子级，两个方向都兜一下。
            var children = GameApi.GetComponentsInChildren(go, m_plateStationType, true);
            if (children != null && children.Length > 0)
                return children[0];
            var t = go.transform.parent;
            while (t != null)
            {
                var p = GameApi.GetComponent(t.gameObject, m_plateStationType);
                if (p != null)
                    return p;
                t = t.parent;
            }
            return null;
        }

        private static bool BoundsCoverXZ(GameObject go, Vector3 p)
        {
            var colliders = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                var c = colliders[i];
                if (c == null)
                    continue;
                var b = c.bounds;
                if (p.x >= b.min.x - 0.05f && p.x <= b.max.x + 0.05f
                    && p.z >= b.min.z - 0.05f && p.z <= b.max.z + 0.05f)
                {
                    if (p.y >= b.min.y - 0.25f && p.y <= b.max.y + 1.5f)
                        return true;
                }
            }
            return false;
        }

        private int ReadInt(PropertyInfo p, object index)
        {
            if (p == null || index == null)
                return 0;
            object v = p.GetValue(index, null);
            return v == null ? 0 : Convert.ToInt32(v);
        }

        private object OffsetIndex(object index, int dx, int dz)
        {
            if (m_gridIndexType == null || index == null)
                return null;
            try
            {
                int x = ReadInt(m_gridX, index) + dx;
                int y = ReadInt(m_gridY, index);
                int z = ReadInt(m_gridZ, index) + dz;
                return Activator.CreateInstance(m_gridIndexType, new object[] { x, y, z });
            }
            catch (Exception)
            {
                return null;
            }
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
