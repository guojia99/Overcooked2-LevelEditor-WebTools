using System;
using UnityEngine;

[Serializable]
public class CampaignLevelConfig : CampaignLevelConfigBase
{
	[Header("Rounds")]
	public RoundData[] m_rounds;

	public override RoundData GetRoundData()
	{
		return m_rounds[0];
	}
}
