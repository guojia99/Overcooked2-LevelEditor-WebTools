using UnityEngine;

namespace GameModes
{
	[CreateAssetMenu(fileName = "recipe_money_data", menuName = "Team17/Game Mode/Recipe Money Data", order = 129)]
	public class RecipeMoneyData : KeyValueData<OrderDefinitionNode, int>
	{
		[SerializeField]
		private RecipeMoneyData m_baseData;

		public override int Get(OrderDefinitionNode key)
		{
			int num = IndexOf(key);
			if (num != -1)
			{
				return m_values[num];
			}
			if (m_baseData != null)
			{
				return m_baseData.Get(key);
			}
			return m_defaultValue;
		}
	}
}
