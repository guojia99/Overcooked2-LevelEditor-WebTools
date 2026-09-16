using System;
using UnityEngine;

[Serializable]
public class SurvivalModeOutroFlowroutineData : FlowroutineData
{
	[SerializeField]
	public float m_minRatingDuration = 1f;

	[SerializeField]
	public float m_minOutroDuration = 20f;

	[SerializeField]
	public GameObject m_timesUpUIPrefab;

	[SerializeField]
	public float m_minTimesUpDuration = 3f;

	[SerializeField]
	public SurvivalModeRatingUIController m_survivalModeRatingUIController;

	[NonSerialized]
	[HideInInspector]
	public object m_scoreData;
}
