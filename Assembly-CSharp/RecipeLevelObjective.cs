using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RecipeLevelObjective : LevelObjectiveBase
{
	[SerializeField]
	public int m_recipesRequired;

	[SerializeField]
	public float m_timeLimit;

	private bool m_objectiveComplete;

	private List<float> m_times = new List<float>();

	public override void Initialise()
	{
		SetCallback(true);
	}

	public override void CleanUp()
	{
		SetCallback(false);
	}

	private void SetCallback(bool bRegister)
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
				clientKitchenFlowControllerBase.m_onMealDelivered += OnMealDelivered;
			}
			else
			{
				clientKitchenFlowControllerBase.m_onMealDelivered -= OnMealDelivered;
			}
		}
	}

	private void OnMealDelivered(int mealId, bool bWasCombo)
	{
		IServerFlowController serverFlowController = GameUtils.RequireManagerInterface<IServerFlowController>();
		ServerKitchenFlowControllerBase serverKitchenFlowControllerBase = serverFlowController as ServerKitchenFlowControllerBase;
		if (serverKitchenFlowControllerBase != null)
		{
			m_times.Add(serverKitchenFlowControllerBase.RoundTimer.TimeElapsed);
		}
	}

	public override bool IsObjectiveComplete()
	{
		if (m_times.Count < m_recipesRequired)
		{
			return false;
		}
		for (int i = 0; i < m_times.Count && m_times.Count - i >= m_recipesRequired; i++)
		{
			float num = 0f;
			for (int j = i + 1; j < m_times.Count && j - i <= m_recipesRequired; j++)
			{
				num += m_times[j] - m_times[j - 1];
			}
			if (num <= m_timeLimit)
			{
				return true;
			}
		}
		return false;
	}
}
