using System;
using UnityEngine;

[RequireComponent(typeof(PlayerControls))]
public class PlayerSlipBehaviour : MonoBehaviour
{
	[Serializable]
	public class PFXReferences
	{
		public GameObject m_slipEffect;

		public GameObject m_streakEffect;

		public GameObject m_impactEffect;
	}

	private const float c_lerpEpsilon = 0.01f;

	[SerializeField]
	public float m_downTime;

	[SerializeField]
	[ReadOnly]
	public float m_fallTime = 0.38f;

	[SerializeField]
	[ReadOnly]
	public float m_standTime = 0.42f;

	[SerializeField]
	[ReadOnly]
	public string m_impactTrigger = "FallImpact";

	[SerializeField]
	public PFXReferences m_pfxReferences = new PFXReferences();
}
