#define ANALYTICS
using System.Collections;
using OrderController;
using Team17.Online;
using UnityEngine;

public class ClientCompetitiveFlowController : ClientKitchenFlowControllerBase
{
	private CompetitiveFlowController m_competitiveFlowController;

	private DataStore m_dataStore;

	private static readonly DataStore.Id k_scoreTipId = new DataStore.Id("score.tip");

	private ClientTeamMonitor m_teamOneMonitor = new ClientTeamMonitor();

	private ClientTeamMonitor m_teamTwoMonitor = new ClientTeamMonitor();

	private ILogicalButton m_cancelButton;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_competitiveFlowController = (CompetitiveFlowController)synchronisedObject;
		m_teamOneMonitor.Initialise(m_competitiveFlowController.m_teamOneData, TeamID.One, BuildOrderController);
		m_teamTwoMonitor.Initialise(m_competitiveFlowController.m_teamTwoData, TeamID.Two, BuildOrderController);
		m_dataStore = GameUtils.RequireManager<DataStore>();
	}

	public override ClientTeamMonitor GetMonitorForTeam(TeamID _team)
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

	protected override void OnUpdateInRound()
	{
		base.OnUpdateInRound();
		m_teamOneMonitor.Update();
		m_teamTwoMonitor.Update();
	}

	protected override IEnumerator RunLevelOutro()
	{
		FinaliseRoundTimer();
		if (m_competitiveFlowController.m_outroFlowroutine != null)
		{
			OvercookedAchievementManager overcookedAchievementManager = GameUtils.RequestManager<OvercookedAchievementManager>();
			if (overcookedAchievementManager != null)
			{
				overcookedAchievementManager.IncStat(16, 1f, ControlPadInput.PadNum.One);
				overcookedAchievementManager.IncStat(11, 1f, ControlPadInput.PadNum.One);
				int totalScore = GetMonitorForTeam(TeamID.One).Score.GetTotalScore();
				int totalScore2 = GetMonitorForTeam(TeamID.Two).Score.GetTotalScore();
				User user = ClientUserSystem.m_Users.Find((User x) => x.IsLocal);
				if (user != null)
				{
					if (totalScore > totalScore2 && user.Team == TeamID.One)
					{
						overcookedAchievementManager.IncStat(9, 1f, ControlPadInput.PadNum.One);
					}
					else if (totalScore < totalScore2 && user.Team == TeamID.Two)
					{
						overcookedAchievementManager.IncStat(9, 1f, ControlPadInput.PadNum.One);
					}
				}
			}
			TeamMonitor.TeamScoreStats score = m_teamOneMonitor.Score;
			TeamMonitor.TeamScoreStats score2 = m_teamTwoMonitor.Score;
			CompetitiveScoreboardUIController.ScoreData scoreData = new CompetitiveScoreboardUIController.ScoreData();
			scoreData.TeamOneData = new TeamMonitor.TeamScoreStats();
			scoreData.TeamTwoData = new TeamMonitor.TeamScoreStats();
			scoreData.TeamOneData.Copy(score);
			scoreData.TeamTwoData.Copy(score2);
			if (ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession())
			{
				Analytics.LogEvent("Level Score", Mathf.Max(score.GetTotalScore(), score2.GetTotalScore()), Analytics.Flags.LevelName | Analytics.Flags.PlayerCount);
			}
			CompetitiveFlowController.OutroData setupData = new CompetitiveFlowController.OutroData(scoreData);
			return m_competitiveFlowController.m_outroFlowroutine.BuildFlowroutine(setupData);
		}
		return null;
	}

	protected override void OnSuccessfulDelivery(TeamID _teamID, OrderID _orderID, float _timePropRemainingPercentage, int _tip, bool _wasCombo, ClientPlateStation _station)
	{
		base.OnSuccessfulDelivery(_teamID, _orderID, _timePropRemainingPercentage, _tip, _wasCombo, _station);
		TeamTip teamTip = new TeamTip
		{
			m_team = _teamID,
			m_tip = _tip,
			m_station = _station
		};
		m_dataStore.Write(k_scoreTipId, teamTip);
	}

	protected override void OnFailedDelivery(TeamID _teamID, OrderID _orderID)
	{
		base.OnFailedDelivery(_teamID, _orderID);
		TeamTip teamTip = new TeamTip
		{
			m_team = _teamID,
			m_tip = 0
		};
		m_dataStore.Write(k_scoreTipId, teamTip);
	}

	protected override void OnOrderExpired(TeamID _teamID, OrderID _orderID)
	{
		base.OnOrderExpired(_teamID, _orderID);
		TeamTip teamTip = new TeamTip
		{
			m_team = _teamID,
			m_tip = 0
		};
		m_dataStore.Write(k_scoreTipId, teamTip);
	}
}
