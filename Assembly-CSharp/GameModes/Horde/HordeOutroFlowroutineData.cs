using System;
using UnityEngine;

namespace GameModes.Horde
{
	[Serializable]
	public class HordeOutroFlowroutineData : FlowroutineData
	{
		[SerializeField]
		public GameObject m_failureOutroPrefab;

		[SerializeField]
		public float m_failureOutroDelaySeconds = 3f;

		[SerializeField]
		public GameObject m_successUIPrefab;

		[SerializeField]
		public GameObject m_failedUIPrefab;

		[SerializeField]
		public float m_successFailPrefabDelaySeconds = 3f;

		[SerializeField]
		public float m_minOutroDelaySeconds = 20f;

		[SerializeField]
		public HordeRatingUIController m_hordeRatingUIController;

		[NonSerialized]
		[HideInInspector]
		public bool m_success;

		[NonSerialized]
		[HideInInspector]
		public float m_health;

		[NonSerialized]
		[HideInInspector]
		public GameProgress.UnlockData[] m_unlocks;

		[NonSerialized]
		[HideInInspector]
		public object m_scoreData;
	}
}
