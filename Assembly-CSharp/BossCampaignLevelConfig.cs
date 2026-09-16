using System;
using UnityEngine;

[Serializable]
public class BossCampaignLevelConfig : DynamicCampaignLevelConfigBase
{
	[Header("Stages")]
	public BossRoundData m_data = new BossRoundData();

	public override RoundData GetRoundData()
	{
		return m_data;
	}
}
