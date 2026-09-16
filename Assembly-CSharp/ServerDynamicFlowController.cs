using System.Collections;
using OrderController;
using UnityEngine;

public class ServerDynamicFlowController : ServerCampaignFlowController
{
	private DynamicFlowController m_dynamicFlowController;

	private DynamicLevelMessage m_data = new DynamicLevelMessage();

	private IEnumerator m_runLevel;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_dynamicFlowController = (DynamicFlowController)synchronisedObject;
		DynamicCampaignLevelConfigBase dynamicCampaignLevelConfigBase = base.LevelConfig as DynamicCampaignLevelConfigBase;
		DynamicRoundData dynamicRoundData = (DynamicRoundData)dynamicCampaignLevelConfigBase.GetRoundData();
		m_runLevel = BuildRunLevelRoutine();
	}

	private void SendPhaseMessage(int _phase)
	{
		m_data.Initialise(_phase);
		ServerMessenger.DynamicLevelMessage(m_data);
	}

	protected override ServerOrderControllerBase BuildOrderController(VoidGeneric<OrderID> _addedCallback, VoidGeneric<OrderID> _timeoutCallback)
	{
		ServerFixedTimeOrderController.OrdersConfig _ordersConfig = BuildOrderConfig();
		ServerOrderControllerBase serverOrderControllerBase = new ServerDynamicOrderController(ref _ordersConfig, _addedCallback, _timeoutCallback);
		serverOrderControllerBase.SetRoundTimer(base.RoundTimer);
		return serverOrderControllerBase;
	}

	protected virtual IEnumerator BuildRunLevelRoutine()
	{
		int phaseNumber = 0;
		while (true)
		{
			IEnumerator phaseRoutine = BuildPhaseRoutine(phaseNumber);
			while (phaseRoutine.MoveNext())
			{
				yield return null;
			}
			ServerDynamicOrderController orderController = GetOrderController();
			if (orderController.HasMorePhases())
			{
				phaseNumber++;
				SendPhaseMessage(phaseNumber);
				orderController.MoveToNextPhase();
				continue;
			}
			break;
		}
		while (true)
		{
			yield return null;
		}
	}

	protected virtual IEnumerator BuildPhaseRoutine(int _phase)
	{
		ServerDynamicOrderController orderController = GetOrderController();
		float currentPhaseDuration = orderController.GetCurrentPhaseDuration();
		return CoroutineUtils.TimerRoutine(currentPhaseDuration, LayerMask.NameToLayer("Default"));
	}

	protected override void OnUpdateInRound()
	{
		m_runLevel.MoveNext();
		base.OnUpdateInRound();
	}

	private ServerDynamicOrderController GetOrderController()
	{
		return GetMonitorForTeam(TeamID.One).OrdersController as ServerDynamicOrderController;
	}
}
