using UnityEngine;

namespace GameModes
{
	[CreateAssetMenu(fileName = "RecipeIntData", menuName = "Team17/Game Mode/Recipe Int Data", order = 129)]
	public class RecipeIntData : KeyValueData<OrderDefinitionNode, int>
	{
		[SerializeField]
		private RecipeIntData m_baseData;

		[SerializeField]
		private int m_recipeFailedPenalty = 1;

		[SerializeField]
		private int[] m_recipeDeliveryBonuses = new int[3] { 1, 2, 3 };

		public int RecipeFailedPenalty
		{
			get
			{
				return m_recipeFailedPenalty;
			}
		}

		public int[] RecipeDeliveryBonuses
		{
			get
			{
				return m_recipeDeliveryBonuses;
			}
		}

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
