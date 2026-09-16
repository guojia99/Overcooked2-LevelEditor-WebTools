using System.Collections.Generic;
using UnityEngine;

[ExecutionDependency(typeof(BootstrapManager))]
[ExecutionDependency(typeof(CompetitiveKitchenLoaderManager))]
[ExecutionDependency(typeof(CampaignKitchenLoaderManager))]
public abstract class KitchenFlowControllerBase : FlowControllerBase
{
	[SerializeField]
	public int m_maxOrdersAllowed = 5;

	public int CalculateBaseScore(RecipeList.Entry _entry)
	{
		return _entry.m_scoreForMeal;
	}

	public int CalculateTip(float _remainingTimePercent)
	{
		RecipeTipBoundary[] array = m_gameConfig.TipBoundaries.FindAll((RecipeTipBoundary x) => x.PercentageTimeRemaining < _remainingTimePercent);
		KeyValuePair<int, RecipeTipBoundary> keyValuePair = array.FindHighestScoring((RecipeTipBoundary x) => x.PercentageTimeRemaining);
		if (keyValuePair.Value != null)
		{
			return keyValuePair.Value.ScoreValue;
		}
		return 0;
	}
}
