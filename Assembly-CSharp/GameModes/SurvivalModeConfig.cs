using System;
using UnityEngine;

namespace GameModes
{
	[Serializable]
	public class SurvivalModeConfig : Config
	{
		[SerializeField]
		public GameObject m_uiPrefab;

		[SerializeField]
		public GameObject m_competitiveUIPrefab;

		[SerializeField]
		public float m_timeMultiplier = 1f;

		[SerializeField]
		public RecipeIntData m_recipeTimes;

		[SerializeField]
		public SurvivalModeOutroFlowroutineData m_outroFlowroutineData;
	}
}
