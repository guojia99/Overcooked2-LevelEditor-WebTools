using System;
using UnityEngine;

[Serializable]
public class ScoreScreenFlowroutineData : FlowroutineData
{
	[SerializeField]
	public GameObject TimesUpUIPrefab;

	[SerializeField]
	public float TimesUpUILifetime = 3f;

	[SerializeField]
	public StarRatingUIController m_starRatingUIController;

	[SerializeField]
	public AwardAvatarUIController m_awardAvatarUIController;

	[SerializeField]
	public AwardSceneUIController m_awardSceneUIController;

	[SerializeField]
	public float m_fTimeout = 20f;

	[NonSerialized]
	[HideInInspector]
	public object m_scoreData;

	[NonSerialized]
	[HideInInspector]
	public int m_points;

	[NonSerialized]
	[HideInInspector]
	public int m_starsAwarded;

	[NonSerialized]
	[HideInInspector]
	public GameProgress.UnlockData[] m_unlocks;
}
