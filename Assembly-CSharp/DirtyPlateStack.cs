using UnityEngine;

[RequireComponent(typeof(Stack))]
[RequireComponent(typeof(HandlePlacementReferral))]
public class DirtyPlateStack : PlateStackBase
{
	[SerializeField]
	public PlatingStepData m_plateType;

	[SerializeField]
	public GameObject m_washedPrefab;

	[SerializeField]
	public GameObject m_cleanPlatePrefab;

	public override PlatingStepData GetPlatingStep()
	{
		return m_plateType;
	}
}
