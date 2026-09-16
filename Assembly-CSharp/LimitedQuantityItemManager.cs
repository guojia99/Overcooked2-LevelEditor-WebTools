using UnityEngine;

public class LimitedQuantityItemManager : Manager
{
	[SerializeField]
	public ParticleSystem m_DestroyPFXPrefab;

	[SerializeField]
	public int m_MaxObjects = 30;

	[Header("Limited Object Score Adjustments (highest score -> first to cull)")]
	[SerializeField]
	public float m_AttachedDeletionScoreModifier = -10000f;

	[SerializeField]
	public float m_OrderComplexityMultiplier = -50f;
}
