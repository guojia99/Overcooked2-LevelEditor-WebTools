using System;
using System.Collections;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 传送门「仅作为出口」（单向传送的出口侧）。
    ///
    /// 背景 —— 游戏侧传送门天生就是单向的，没有任何「双向」开关：
    ///  - Teleportal.m_exitPortal 是方向的唯一来源；双向 = 两扇门互相指；
    ///  - 发送只有两条入口：ServerTeleportal.OnTriggerStay（走人/物进门）与
    ///    ServerTeleportalConveyenceReceiver.ConveyToMe（传送带喂料，门槛
    ///    CanTeleport）；两者最终都要求 m_exitReceivers 非空；
    ///  - m_exitPortal == null 时 ServerTeleportal.StartSynchronising 不填
    ///    m_exitReceivers → CanTeleport 恒 false → 该门只收不发（收货由入口侧
    ///    ServerTeleportal.TeleportTo 驱动本门 receivers，完全不受影响）。
    ///
    /// 为什么需要运行时组件 —— 编辑器里无法直接表达「出口为空」：
    /// 宿主 LevelEditor.PseudoPrefabTeleportal.LateSetup 与真机 mod 的同源逻辑
    /// 都对 null exitPortal 无判空直接 GetComponent → NRE，写回链因此把无出口的
    /// 传送门降级成装饰件（连带把入口侧的绑定也打断）。故约定：
    ///  - 出口门的 stub.exitPortal 保留【回指入口】的占位（宿主/mod 照常 Setup，
    ///    颜色与双面外观都保住）；
    ///  - 方向语义由本组件 + tag 载体决定：运行时把 m_exitPortal 清回 null。
    ///
    /// 时序无关性：不假设宿主 LateSetup 与网络握手的先后——周期性复压
    /// （TravelatorReverser 同款约定）。若 Server/ClientTeleportal 已经在
    /// StartSynchronising 里缓存了 m_exitReceivers（ServerTeleportal.cs:60-63 /
    /// ClientTeleportal.cs:51-54），一并反射清空，否则该门仍会发送。
    ///
    /// 联机：只改本端的 vanilla 字段，各端同配置组件天然一致，无自定义网络消息
    /// （TimedCookingSwitch / TravelatorReverser 同款约定）。
    ///
    /// 数据双通道：
    ///  - 权威通道：本组件序列化字段（StubIO 写回时反射 AddComponent + 填字段）；
    ///  - 载体通道：SpecificPseudoPrefabTag.prefabTag = "TeleportalExitOnly|1"
    ///    （1/0 = 启用/保留但不生效），组件缺失时由 EntryPoint 场景自愈还原。
    ///
    /// 依赖包过旧（版本门控跳过 stub 支持）时的退化 = 出口门照常双向传送
    /// （fail-open，关卡本体不受影响）。
    /// </summary>
    public class TeleportalExitOnly : MonoBehaviour
    {
        /** false = 配置保留但不生效（该门恢复为可进可出）。 */
        public bool m_enabled = true;

        /// <summary>tag 载体前缀（EntryPoint 自愈与 StubIO 烘焙共用约定）。</summary>
        public const string TagPrefix = "TeleportalExitOnly|";

        /// <summary>绑定前的等待上限（秒）：宿主/模组的子物体是异步生成的。</summary>
        private const float BindTimeoutSeconds = 20f;

        /// <summary>握手前的复压间隔（帧）：宿主 LateSetup 可能晚于本组件启动。</summary>
        private const int FastReassertFrames = 10;

        /// <summary>收口后的复压间隔（帧）：稳态只做一次字段读 + null 比较。</summary>
        private const int SlowReassertFrames = 60;

        private Component m_teleportal;
        private GameObject m_originalExit;
        private bool m_bound;
        private bool m_hasOriginalExit;
        private bool m_clearedLogged;
        private bool m_applyWarned;
        private bool m_receiversCleared;

        private void OnEnable()
        {
            StartCoroutine(SafeRunner(Drive()));
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            // 编辑器停止 Play / 组件被移除：还原烘焙的占位出口，不留单向残留
            RestoreOriginalExit();
            m_bound = false;
            m_teleportal = null;
            m_receiversCleared = false;
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
                catch (Exception ex)
                {
                    StubLog.LogWarn("[TeleportalExitOnly] 协程异常退出: " + name + "\n" + ex);
                    yield break;
                }
                if (!hasNext)
                    yield break;
                yield return current;
            }
        }

        private IEnumerator Drive()
        {
            if (GameApi.TeleportalType == null || GameApi.TeleportalExitPortalField == null)
            {
                StubLog.LogWarn("[TeleportalExitOnly] 反射缺失（Teleportal/m_exitPortal），"
                    + "单向失效（退化为双向）: " + name);
                yield break;
            }

            // 等子树里的 Teleportal（宿主/模组生成 child 后才出现）
            var waitChild = 0f;
            while (!m_bound)
            {
                TryBindTeleportal();
                if (m_bound)
                    break;
                waitChild += Time.unscaledDeltaTime;
                if (waitChild > BindTimeoutSeconds)
                {
                    StubLog.LogWarn("[TeleportalExitOnly] 等待 Teleportal 超时（"
                        + BindTimeoutSeconds.ToString("0") + "s），保持原方向: " + name);
                    yield break;
                }
                yield return null;
            }

            if (!m_enabled)
            {
                StubLog.Dbg("[TeleportalExitOnly] 已禁用（配置保留，保持双向）: " + name);
                yield break;
            }

            var frames = FastReassertFrames; // 首帧立即压制
            while (true)
            {
                if (m_teleportal == null)
                {
                    StubLog.LogWarn("[TeleportalExitOnly] Teleportal 已销毁，协程退出"
                        + "（重开关卡由自愈重建）: " + name);
                    yield break;
                }
                // 握手完成且缓存已清 = 收口，降到低频复压（稳态每 60 帧一次字段读）
                var interval = m_receiversCleared ? SlowReassertFrames : FastReassertFrames;
                if (frames++ >= interval)
                {
                    frames = 0;
                    TryApplyExitOnly();
                }
                yield return null;
            }
        }

        private void TryBindTeleportal()
        {
            var found = GetComponentsInChildren(GameApi.TeleportalType, true);
            for (int i = 0; i < found.Length; i++)
            {
                var c = found[i];
                if (c == null)
                    continue;
                m_teleportal = c;
                m_bound = true;
                return;
            }
        }

        /// <summary>压制：m_exitPortal 清回 null + 清 Server/ClientTeleportal 已缓存的
        /// m_exitReceivers。周期调用——同类异常只报一次。</summary>
        private void TryApplyExitOnly()
        {
            try
            {
                var exit = GameApi.TeleportalExitPortalField.GetValue(m_teleportal) as GameObject;
                if (exit != null)
                {
                    if (!m_hasOriginalExit)
                    {
                        m_originalExit = exit;
                        m_hasOriginalExit = true;
                    }
                    GameApi.TeleportalExitPortalField.SetValue(m_teleportal, null);
                    if (!m_clearedLogged)
                    {
                        m_clearedLogged = true;
                        StubLog.Dbg("[TeleportalExitOnly] 已设为仅出口（清除出口指向 "
                            + exit.name + "）: " + name);
                    }
                }
                // 握手后 Server/ClientTeleportal 才挂上并缓存 receivers：清空缓存，
                // 否则「先握手、后清 m_exitPortal」的时序下该门仍会发送。
                var serverCleared = ClearCachedReceivers(
                    GameApi.ServerTeleportalType, GameApi.ServerTeleportalExitReceiversField);
                var clientCleared = ClearCachedReceivers(
                    GameApi.ClientTeleportalType, GameApi.ClientTeleportalExitReceiversField);
                if (!m_receiversCleared && (serverCleared || clientCleared)
                    && GameApi.IsSynchronisationActive())
                {
                    m_receiversCleared = true;
                    StubLog.Dbg("[TeleportalExitOnly] 出口 receivers 缓存已清，进入稳态: " + name);
                }
            }
            catch (Exception ex)
            {
                if (!m_applyWarned)
                {
                    m_applyWarned = true;
                    StubLog.LogWarn("[TeleportalExitOnly] 压制失败: " + name + " " + ex.Message);
                }
            }
        }

        /// <summary>清空同物体上同步器组件缓存的出口 receivers 数组。
        /// 返回 true = 组件已在（无论本次是否需要清空）。</summary>
        private bool ClearCachedReceivers(Type syncType, System.Reflection.FieldInfo field)
        {
            if (syncType == null || field == null || m_teleportal == null)
                return false;
            var sync = m_teleportal.gameObject.GetComponent(syncType);
            if (sync == null)
                return false;
            var current = field.GetValue(sync) as Array;
            if (current != null && current.Length > 0)
            {
                var elementType = field.FieldType.GetElementType();
                if (elementType != null)
                    field.SetValue(sync, Array.CreateInstance(elementType, 0));
            }
            return true;
        }

        private void RestoreOriginalExit()
        {
            if (!m_hasOriginalExit || m_teleportal == null
                || GameApi.TeleportalExitPortalField == null)
                return;
            try
            {
                GameApi.TeleportalExitPortalField.SetValue(m_teleportal, m_originalExit);
            }
            catch (Exception)
            {
                // 退出 Play / 对象销毁途中：忽略
            }
            m_hasOriginalExit = false;
            m_originalExit = null;
            m_clearedLogged = false;
        }
    }
}
