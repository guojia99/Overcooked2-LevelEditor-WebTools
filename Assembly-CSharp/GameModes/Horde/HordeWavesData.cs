using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameModes.Horde
{
	[Serializable]
	public struct HordeWavesData
	{
		[SerializeField]
		private List<HordeWaveData> m_waves;

		public HordeWaveData this[int idx]
		{
			get
			{
				return m_waves[idx];
			}
		}

		public int Count
		{
			get
			{
				return (m_waves != null) ? m_waves.Count : 0;
			}
		}
	}
}
