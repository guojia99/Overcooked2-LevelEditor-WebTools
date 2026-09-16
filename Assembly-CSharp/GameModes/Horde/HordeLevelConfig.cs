using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameModes.Horde
{
	[Serializable]
	[CreateAssetMenu(fileName = "horde_level_config", menuName = "Team17/Game Mode/Horde/Level Config")]
	public class HordeLevelConfig : LevelConfigBase
	{
		[SerializeField]
		public float m_plateReturnTime = 10f;

		[Header("Horde Target Data")]
		[SerializeField]
		public int m_targetHealth = 100;

		[SerializeField]
		public float m_targetRepairSpeed = 0.5f;

		[SerializeField]
		public float m_targetRepairThreshold = 10f;

		[SerializeField]
		public int m_targetRepairCostMax = 200;

		[Header("Horde Level Data")]
		[SerializeField]
		public int m_health = 100;

		[SerializeField]
		public RecipeMoneyData m_recipeMoney;

		[SerializeField]
		public HordeOutroFlowroutineData m_flowroutineData;

		[SerializeField]
		public HordeWavesData m_waves;

		public override List<OrderDefinitionNode> GetAllRecipes()
		{
			List<OrderDefinitionNode> list = new List<OrderDefinitionNode>();
			for (int i = 0; i < m_waves.Count; i++)
			{
				HordeWaveData hordeWaveData = m_waves[i];
				for (int j = 0; j < hordeWaveData.m_recipes.m_recipes.Length; j++)
				{
					list.Add(hordeWaveData.m_recipes.m_recipes[j].m_order);
				}
			}
			return list;
		}
	}
}
