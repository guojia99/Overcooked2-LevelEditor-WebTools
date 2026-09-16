using System;
using UnityEngine;

public class BellowsSpray : SprayingUtensil
{
	[Serializable]
	public class KnockbackData
	{
		[SerializeField]
		public float Force = 10f;

		[SerializeField]
		public float Duration = 0.2f;
	}

	[SerializeField]
	public float m_heatIncrease;

	[SerializeField]
	public KnockbackData m_knockback = new KnockbackData();
}
