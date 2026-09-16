using UnityEngine;

[RequireComponent(typeof(Stack))]
[RequireComponent(typeof(HandlePickupReferral))]
[RequireComponent(typeof(HandlePlacementReferral))]
public class CleanPlateStack : PlateStackBase
{
	public override PlatingStepData GetPlatingStep()
	{
		Plate plate = m_platePrefab.RequestComponent<Plate>();
		return plate.m_platingStep;
	}
}
