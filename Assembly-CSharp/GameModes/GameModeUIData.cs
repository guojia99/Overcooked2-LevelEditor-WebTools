using UnityEngine;

namespace GameModes
{
	[CreateAssetMenu(fileName = "GameModeUIData", menuName = "Team17/Game Mode/Frontend Data")]
	public class GameModeUIData : ScriptableObject
	{
		[SerializeField]
		public ModeUIData[] m_gameModes = new ModeUIData[3];

		[SerializeField]
		public ModeSettingUIData[] m_gameModeSettings = new ModeSettingUIData[3];
	}
}
