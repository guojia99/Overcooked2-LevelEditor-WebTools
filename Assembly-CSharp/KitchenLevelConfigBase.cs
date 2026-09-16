using GameModes;
using UnityEngine;

public abstract class KitchenLevelConfigBase : LevelConfigBase
{
	[Header("Order Parameters")]
	public float m_orderLifetime = 100f;

	public float m_timeBetweenOrders = 15f;

	public float m_plateReturnTime = 10f;

	[Range(0f, 20f)]
	[SerializeField]
	public int m_recipesBeforeTimerStarts;

	[Header("Freestyle Recipes")]
	public int[] m_freestyleTimes = new int[0];

	public int[] m_freestyleScoreThresholds = new int[0];

	public int m_freestyleComboInterval = 4;

	[Header("Game Mode Configs")]
	[SerializeField]
	public CampaignModeConfig m_campaignConfig = new CampaignModeConfig();

	[SerializeField]
	public PracticeModeConfig m_practiceConfig = new PracticeModeConfig();

	[SerializeField]
	public SurvivalModeConfig m_survivalConfig = new SurvivalModeConfig();

	public abstract RoundData GetRoundData();

	public abstract float GetTimeLimit();
}
