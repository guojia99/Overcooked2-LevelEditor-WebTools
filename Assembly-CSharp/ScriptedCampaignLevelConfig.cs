using System;
using UnityEngine;

[Serializable]
public class ScriptedCampaignLevelConfig : CampaignLevelConfigBase
{
	[Header("Rounds")]
	public ScriptedRoundData[] m_rounds;

	public override RoundData GetRoundData()
	{
		return m_rounds[0];
	}
}
