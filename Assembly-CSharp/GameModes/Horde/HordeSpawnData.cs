using System;
using UnityEngine;

namespace GameModes.Horde
{
	[Serializable]
	public struct HordeSpawnData
	{
		[SerializeField]
		public int m_spawnTimeSeconds;

		[SerializeField]
		public GameObject m_prefab;

		public bool CanSpawn(double time)
		{
			return (double)m_spawnTimeSeconds <= time;
		}
	}
}
