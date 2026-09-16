using System;
using UnityEngine;

namespace GameModes
{
	[Serializable]
	public class CampaignModeConfig : Config
	{
		[SerializeField]
		public GameObject m_uiPrefab;

		[SerializeField]
		public ScoreScreenFlowroutineData m_scoreScreenData;
	}
}
