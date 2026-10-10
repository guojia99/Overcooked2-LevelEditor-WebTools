using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 摇杆遥控地板组根标记：绑定控制终端伪根，供 AnimPilotFloorDrive 判定会话/占格/随动。
    /// 由布局 PilotGroupBakery 或 Play 自愈补挂；勿放在 LevelEditor 程序集。
    /// </summary>
    public class AnimPilotFloorMarker : MonoBehaviour
    {
        [SerializeField]
        private GameObject joystickPseudoRoot;

        public GameObject JoystickPseudoRoot
        {
            get { return joystickPseudoRoot; }
            set { joystickPseudoRoot = value; }
        }
    }
}
