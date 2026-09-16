using UnityEngine;

namespace GameModes
{
	internal static class SurvivalModeUtil
	{
		public static int CalculateDeliveryBonus(SurvivalModeConfig config, float percentage)
		{
			return config.m_recipeTimes.RecipeDeliveryBonuses[Mathf.Clamp(Mathf.FloorToInt(percentage * (float)config.m_recipeTimes.RecipeDeliveryBonuses.Length), 0, config.m_recipeTimes.RecipeDeliveryBonuses.Length - 1)];
		}
	}
}
