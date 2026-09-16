using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameModes.Horde
{
	[Serializable]
	public struct HordeWaveData
	{
		[SerializeField]
		public RecipeList m_recipes;

		[SerializeField]
		public int m_intervalSeconds;

		[SerializeField]
		public List<HordeSpawnData> m_spawns;
	}
}
