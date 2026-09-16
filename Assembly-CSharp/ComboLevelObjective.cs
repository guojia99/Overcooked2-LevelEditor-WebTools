using System;
using UnityEngine;

[Serializable]
public class ComboLevelObjective : LevelObjectiveBase
{
	[SerializeField]
	public int m_comboRequired;

	private int m_runningCombo;

	private int m_maxAttainedCombo;

	public override void Initialise()
	{
		SetCallbacks(true);
	}

	public override void CleanUp()
	{
		SetCallbacks(false);
	}

	private void SetCallbacks(bool bRegister)
	{
		IFlowController flowController = GameUtils.RequestManagerInterface<IFlowController>();
		if (flowController == null)
		{
			return;
		}
		MonoBehaviour monoBehaviour = flowController as MonoBehaviour;
		if (flowController != null && monoBehaviour.gameObject != null)
		{
			ClientKitchenFlowControllerBase clientKitchenFlowControllerBase = monoBehaviour.gameObject.RequireComponent<ClientKitchenFlowControllerBase>();
			if (bRegister)
			{
				clientKitchenFlowControllerBase.m_onMealDelivered += OnSuccessfulDelivery;
				clientKitchenFlowControllerBase.m_onFailedDelivery += OnFailedDelivery;
			}
			else
			{
				clientKitchenFlowControllerBase.m_onMealDelivered -= OnSuccessfulDelivery;
				clientKitchenFlowControllerBase.m_onFailedDelivery -= OnFailedDelivery;
			}
		}
	}

	private void OnSuccessfulDelivery(int mealId, bool bWasCombo)
	{
		SetRunningCombo(bWasCombo ? (m_runningCombo + 1) : 0);
	}

	private void OnFailedDelivery()
	{
		SetRunningCombo(0);
	}

	private void SetRunningCombo(int value)
	{
		m_runningCombo = value;
		m_maxAttainedCombo = Mathf.Max(m_maxAttainedCombo, m_runningCombo);
	}

	public override bool IsObjectiveComplete()
	{
		return m_maxAttainedCombo >= m_comboRequired;
	}
}
