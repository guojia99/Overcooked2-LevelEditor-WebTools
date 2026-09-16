using System;
using UnityEngine;

public class CookableProperties : MonoBehaviour
{
	[SerializeField]
	private CookingStepData[] AllowedCookingSteps = new CookingStepData[0];

	public bool AllowsCookingStep(CookingStepData _stepData)
	{
		return Array.FindIndex(AllowedCookingSteps, (CookingStepData x) => x != null && x.m_uID == _stepData.m_uID) != -1;
	}
}
