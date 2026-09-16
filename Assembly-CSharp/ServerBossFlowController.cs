using System.Collections;
using GameModes;
using OrderController;
using UnityEngine;

public class ServerBossFlowController : ServerDynamicFlowController
{
	private BossFlowController m_bossFlowController;

	private BossLevelMessage m_data = new BossLevelMessage();

	private bool m_bHasFinished;

	private Kind m_gameModeKind;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_bossFlowController = (BossFlowController)synchronisedObject;
		GameSession gameSession = GameUtils.GetGameSession();
		m_gameModeKind = gameSession.GameModeKind;
	}

	private void SendSuccessMessage()
	{
		ServerMessenger.BossLevelMessage(m_data);
	}

	protected override ServerOrderControllerBase BuildOrderController(VoidGeneric<OrderID> _addedCallback, VoidGeneric<OrderID> _timeoutCallback)
	{
		ServerFixedTimeOrderController.OrdersConfig _ordersConfig = BuildOrderConfig();
		ServerOrderControllerBase serverOrderControllerBase = new ServerBossOrderController(ref _ordersConfig, _addedCallback, _timeoutCallback);
		serverOrderControllerBase.SetRoundTimer(base.RoundTimer);
		return serverOrderControllerBase;
	}

	protected override IEnumerator BuildPhaseRoutine(int _phase)
	{
		ServerBossOrderController orderController = GetOrderController();
		while (!orderController.CurrentPhaseComplete())
		{
			yield return null;
		}
	}

	protected override void OnUpdateInRound()
	{
		base.OnUpdateInRound();
		if (m_gameModeKind != Kind.Practice && !m_bHasFinished)
		{
			ServerBossOrderController orderController = GetOrderController();
			if (orderController.HasFinished())
			{
				m_bHasFinished = true;
				SendSuccessMessage();
			}
		}
	}

	protected override bool HasFinished()
	{
		ServerBossOrderController orderController = GetOrderController();
		return (orderController.HasFinished() || base.HasFinished()) && m_gameModeKind != Kind.Practice;
	}

	private ServerBossOrderController GetOrderController()
	{
		return GetMonitorForTeam(TeamID.One).OrdersController as ServerBossOrderController;
	}
}
