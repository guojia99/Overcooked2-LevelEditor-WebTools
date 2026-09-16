using System.Collections.Generic;

public abstract class CampaignLevelConfigBase : KitchenLevelConfigBase
{
	public override float GetTimeLimit()
	{
		RoundData roundData = GetRoundData();
		return roundData.m_roundTimer;
	}

	public override List<OrderDefinitionNode> GetAllRecipes()
	{
		RoundData roundData = GetRoundData();
		List<OrderDefinitionNode> list = new List<OrderDefinitionNode>();
		for (int i = 0; i < roundData.m_recipes.m_recipes.Length; i++)
		{
			list.Add(roundData.m_recipes.m_recipes[i].m_order);
		}
		return list;
	}
}
