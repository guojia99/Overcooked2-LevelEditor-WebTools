using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ScoreLevelObjective : LevelObjectiveBase
{
	public int m_scoreRequired;

	public float m_timeLimit;

	private bool m_objectiveComplete;

	private List<float> m_times = new List<float>();

	private List<int> m_scores = new List<int>();

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
			m_scores.Add(serverKitchenFlowControllerBase.GetPoints(TeamID.One));
		}
	}

	public override bool IsObjectiveComplete()
	{
		IServerFlowController serverFlowController = GameUtils.RequireManagerInterface<IServerFlowController>();
		ServerKitchenFlowControllerBase serverKitchenFlowControllerBase = serverFlowController as ServerKitchenFlowControllerBase;
		if (serverKitchenFlowControllerBase != null && serverKitchenFlowControllerBase.GetPoints(TeamID.One) < m_scoreRequired)
		{
			return false;
		}
		for (int i = 0; i < m_times.Count; i++)
		{
			float num = 0f;
			int num2 = ((i == 0) ? m_scores[i] : 0);
			for (int j = i + 1; j < m_times.Count; j++)
			{
				num += m_times[j] - m_times[j - 1];
				num2 += m_scores[j] - m_scores[j - 1];
				if (num <= m_timeLimit && num2 >= m_scoreRequired)
				{
					return true;
				}
			}
		}
		return false;
	}
}
