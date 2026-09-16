using System;
using UnityEngine;

[Serializable]
public class DynamicCampaignLevelConfig : DynamicCampaignLevelConfigBase
{
	[Header("Stages")]
	public DynamicRoundData m_data = new DynamicRoundData();

	public override RoundData GetRoundData()
	{
		return m_data;
	}
}
