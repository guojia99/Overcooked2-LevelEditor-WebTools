using OrderController;
using UnityEngine;

public class ServerCompetitiveFlowController : ServerKitchenFlowControllerBase
{
	private CompetitiveFlowController m_competitiveFlowController;

	private ServerTeamMonitor m_teamOneMonitor = new ServerTeamMonitor();

	private ServerTeamMonitor m_teamTwoMonitor = new ServerTeamMonitor();

	private ILogicalButton m_cancelButton;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_competitiveFlowController = (CompetitiveFlowController)synchronisedObject;
		m_teamOneMonitor.Initialise(m_competitiveFlowController.m_teamOneData, BuildOrderController, delegate(OrderID _order)
		{
			OnOrderAdded(TeamID.One, _order);
		}, delegate(OrderID _order)
		{
			OnOrderExpired(TeamID.One, _order);
		});
		m_teamTwoMonitor.Initialise(m_competitiveFlowController.m_teamTwoData, BuildOrderController, delegate(OrderID _order)
		{
			OnOrderAdded(TeamID.Two, _order);
		}, delegate(OrderID _order)
		{
			OnOrderExpired(TeamID.Two, _order);
		});
	}

	protected override void OnUpdateInRound()
	{
		base.OnUpdateInRound();
		m_teamOneMonitor.Update();
		m_teamTwoMonitor.Update();
	}

	public override ServerTeamMonitor GetMonitorForTeam(TeamID _team)
	{
		switch (_team)
		{
		case TeamID.One:
			return m_teamOneMonitor;
		case TeamID.Two:
			return m_teamTwoMonitor;
		default:
			return null;
		}
	}

	protected override string GetNextScene(out GameState o_loadState, out GameState o_loadEndState, out bool o_useLoadingScreen)
	{
		o_loadState = GameState.NotSet;
		o_loadEndState = GameState.NotSet;
		o_useLoadingScreen = false;
		if (ServerGameSetup.Mode == GameMode.Versus)
		{
			o_loadState = GameState.VSLobby;
			o_loadEndState = GameState.NotSet;
			o_useLoadingScreen = true;
			return GameUtils.GetGameSession().TypeSettings.WorldMapScene;
		}
		return string.Empty;
	}
}
