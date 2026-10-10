using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 摇杆灯（ControlTerminal_Marker）→ 摇杆（Terminal）绑定配置 + 运行时灯色驱动。
    /// 仅在终端会话已建立（玩家已触发摇杆驾驶）时 GreenMarker，不作「可触发」提示灯。
    /// </summary>
    public class JoystickMarkerLink : MonoBehaviour
    {
        [SerializeField] public GameObject joystickWrapper;

        private float _nextPoll;
        private bool _lastLit;
        private bool _hasState;

        private void Update()
        {
            if (Time.realtimeSinceStartup < _nextPoll) return;
            _nextPoll = Time.realtimeSinceStartup + 0.25f;
            bool lit;
            if (!TryReadShouldLight(out lit)) return;
            if (_hasState && lit == _lastLit) return;
            _lastLit = lit;
            _hasState = true;
            Fire(lit ? "GreenMarker" : "DeactivateMarker");
        }

        private bool TryReadShouldLight(out bool lit)
        {
            lit = false;
            var jw = joystickWrapper;
            if (jw == null) return false;
            try
            {
                var terminalComp = GameApi.GetComponentInChildren(jw, GameApi.TerminalType) as Component;
                if (terminalComp == null) return false;

                lit = TryReadSessionOccupied(terminalComp.gameObject);
                return true;
            }
            catch (System.Exception e)
            {
                StubLog.Dbg("[JoystickMarkerLink] 状态读取失败 " + name + ": " + e.Message);
            }
            return false;
        }

        private static bool TryReadSessionOccupied(GameObject terminalGo)
        {
            if (GameApi.ServerTerminalType != null && GameApi.SessionField != null)
            {
                var st = GameApi.GetComponent(terminalGo, GameApi.ServerTerminalType);
                if (st != null)
                    return GameApi.SessionField.GetValue(st) != null;
            }
            if (GameApi.ClientTerminalType != null && GameApi.ClientSessionField != null)
            {
                var ct = GameApi.GetComponent(terminalGo, GameApi.ClientTerminalType);
                if (ct != null)
                    return GameApi.ClientSessionField.GetValue(ct) != null;
            }
            return false;
        }

        private void Fire(string trigger)
        {
            try
            {
                BroadcastMessage("OnTrigger", trigger, SendMessageOptions.DontRequireReceiver);
            }
            catch (System.Exception e)
            {
                StubLog.Dbg("[JoystickMarkerLink] 触发失败 " + name + " " + trigger + ": " + e.Message);
            }
        }
    }
}
