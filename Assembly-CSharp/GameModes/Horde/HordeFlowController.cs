using UnityEngine;

namespace GameModes.Horde
{
	public class HordeFlowController : FlowControllerBase
	{
		[AssignResource("horde_wave_number_ui", Editorbility.NonEditable)]
		[SerializeField]
		public GameObject m_waveNumberUIPrefab;

		[SerializeField]
		public float m_waveNumberUIDelay = 1f;

		[SerializeField]
		public string m_waveNumberUILocalisationTag = "Horde.Wave";

		[AssignResource("horde_mode_ui", Editorbility.Editable)]
		[SerializeField]
		public GameObject m_uiPrefab;
	}
}
