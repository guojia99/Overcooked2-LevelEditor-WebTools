using UnityEngine;

public class CakeTinContentsCosmeticDecisions : AnimationInspectionBase
{
	[SerializeField]
	public GameObject m_gameObject;

	[SerializeField]
	public GameObject m_contentsObject;

	[SerializeField]
	public string m_surfaceMaterialName;

	[SerializeField]
	public string m_bubbleMaterialName;

	[SerializeField]
	public float m_contentsYPositionWhenFull = 0.2f;

	[SerializeField]
	public float m_contentsYPositionWhenEmpty;
}
