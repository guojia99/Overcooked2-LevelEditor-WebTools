using System.Collections.Generic;

public abstract class DynamicCampaignLevelConfigBase : CampaignLevelConfigBase
{
	public override List<OrderDefinitionNode> GetAllRecipes()
	{
		DynamicRoundData dynamicRoundData = (DynamicRoundData)GetRoundData();
		List<OrderDefinitionNode> list = new List<OrderDefinitionNode>();
		for (int i = 0; i < dynamicRoundData.Phases.Length; i++)
		{
			for (int j = 0; j < dynamicRoundData.Phases[i].Recipes.m_recipes.Length; j++)
			{
				list.Add(dynamicRoundData.Phases[i].Recipes.m_recipes[j].m_order);
			}
		}
		return list;
	}
}
