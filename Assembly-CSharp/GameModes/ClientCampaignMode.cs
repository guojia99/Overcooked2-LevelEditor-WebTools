#define ANALYTICS
using OrderController;
using UnityEngine;

namespace GameModes
{
	public class ClientCampaignMode : ClientGameModeBase
	{
		private ClientContext m_context;

		private CampaignModeConfig m_config;

		private Suppressor m_recipeDeliverySuppressor;

		private int m_recipesDelivered;

		private IClientRoundTimer m_roundTimer;

		private ScoreScreenOutroFlowroutine m_scoreScreenFlowroutine;

		private DataStore m_dataStore;

		private static readonly DataStore.Id k_scoreTipId = new DataStore.Id("score.tip");

		public ClientCampaignMode(Config config)
			: base(config)
		{
			m_config = (CampaignModeConfig)config;
		}

		private ClientOrderControllerBase ClientOrderControllerBuilder(RecipeFlowGUI _recipeUI)
		{
			ClientFixedTimeOrderController clientFixedTimeOrderController = new ClientFixedTimeOrderController(_recipeUI);
			clientFixedTimeOrderController.SetRoundTimer(m_roundTimer);
			return clientFixedTimeOrderController;
		}

		public override void Setup(ClientContext context, SessionConfig config, ref ClientSetupData setupData)
		{
			m_context = context;
			m_roundTimer = new ClientRoundTimer();
			m_scoreScreenFlowroutine = new ScoreScreenOutroFlowroutine();
			setupData.m_orderControllerBuilder = ClientOrderControllerBuilder;
			setupData.m_roundTimer = m_roundTimer;
			setupData.m_onSuccessfulDelivery = OnSuccessfulDelivery;
			setupData.m_onFailedDelivery = OnFailedDelivery;
			setupData.m_onOrderExpired = OnOrderExpired;
			setupData.m_onOutro = OnOutro;
			GameObject gameObject = GameUtils.InstantiateUIControllerOnScalingHUDCanvas(m_config.m_uiPrefab);
			m_dataStore = GameUtils.RequireManager<DataStore>();
		}

		public override void Begin()
		{
			if (ClientGameSetup.Mode == GameMode.Campaign && m_context.m_levelConfig.m_recipesBeforeTimerStarts > 0)
			{
				m_recipeDeliverySuppressor = m_roundTimer.Suppressor.AddSuppressor(m_context.m_gameObject);
			}
		}

		private void OnSuccessfulDelivery(TeamID teamID, OrderID orderID, float _timePropRemainingPercentage, int tip, bool wasCombo, ClientPlateStation station)
		{
			TeamTip teamTip = new TeamTip
			{
				m_team = teamID,
				m_tip = tip,
				m_station = station
			};
			m_dataStore.Write(k_scoreTipId, teamTip);
			if (m_recipeDeliverySuppressor != null)
			{
				m_recipesDelivered++;
				if (m_recipesDelivered >= m_context.m_levelConfig.m_recipesBeforeTimerStarts)
				{
					m_recipeDeliverySuppressor.Release();
					m_recipeDeliverySuppressor = null;
				}
			}
		}

		private void OnFailedDelivery(TeamID teamId, OrderID orderId)
		{
			TeamTip teamTip = new TeamTip
			{
				m_team = teamId,
				m_tip = 0
			};
			m_dataStore.Write(k_scoreTipId, teamTip);
		}

		private void OnOrderExpired(TeamID teamId, OrderID orderId)
		{
			TeamTip teamTip = new TeamTip
			{
				m_team = teamId,
				m_tip = 0
			};
			m_dataStore.Write(k_scoreTipId, teamTip);
		}

		private IFlowroutine OnOutro(GenericVoid onRestartRequest, int starRating)
		{
			ClientTeamMonitor monitorForTeam = m_context.m_flowController.GetMonitorForTeam(TeamID.One);
			GameSession gameSession = GameUtils.GetGameSession();
			TeamMonitor.TeamScoreStats score = monitorForTeam.Score;
			int totalScore = score.GetTotalScore();
			int levelID = GameUtils.GetLevelID();
			GameProgress.GameProgressData.LevelProgress levelProgress = gameSession.Progress.SaveData.GetLevelProgress(levelID);
			bool nGPEnabled = levelProgress.NGPEnabled;
			int num = ((levelProgress != null) ? levelProgress.ScoreStars : 0);
			GameProgress.UnlockData[] _unlocks = new GameProgress.UnlockData[0];
			gameSession.Progress.RecordLevelProgress(levelID, starRating, ref _unlocks);
			gameSession.Progress.RecordLevelScore(new GameProgress.HighScores.Score
			{
				iLevelID = levelID,
				iHighScore = totalScore,
				iSurvivalModeTime = 0
			});
			bool justUnlockedNGP = false;
			if (!nGPEnabled)
			{
				justUnlockedNGP = gameSession.Progress.SaveData.GetLevelProgress(levelID).NGPEnabled;
			}
			OvercookedAchievementManager overcookedAchievementManager = GameUtils.RequestManager<OvercookedAchievementManager>();
			if (overcookedAchievementManager != null)
			{
				overcookedAchievementManager.IncStat(16, 1f, ControlPadInput.PadNum.One);
				if (score.ComboMaintained && score.TotalCombo != 0)
				{
					overcookedAchievementManager.AddIDStat(13, 1, ControlPadInput.PadNum.One);
				}
				if (ClientGameSetup.Mode == GameMode.Party)
				{
					overcookedAchievementManager.IncStat(10, 1f, ControlPadInput.PadNum.One);
				}
			}
			if (ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession())
			{
				Analytics.LogEvent("Level Score", score.GetTotalScore(), Analytics.Flags.LevelName | Analytics.Flags.PlayerCount);
			}
			CoopStarRatingUIController.ScoreData scoreData = new CoopStarRatingUIController.ScoreData();
			scoreData.SuccessPoints = score.TotalBaseScore;
			scoreData.FailDeductions = score.TotalTimeExpireDeductions;
			scoreData.Tips = score.TotalTipsScore;
			scoreData.Score = totalScore;
			scoreData.StarRating = starRating;
			scoreData.StarRatingIncreased = starRating > num;
			scoreData.TotalSuccessfulDeliveries = score.TotalSuccessfulDeliveries;
			scoreData.JustUnlockedNGP = justUnlockedNGP;
			CoopStarRatingUIController.ScoreData scoreData2 = scoreData;
			CutsceneOutroFlowroutineBase component = m_context.m_flowController.GetComponent<CutsceneOutroFlowroutineBase>();
			if (component != null)
			{
				CampaignFlowController.OutroData setupData = new CampaignFlowController.OutroData(scoreData2, scoreData2.Score, scoreData2.StarRating, _unlocks);
				return component.BuildFlowroutine(setupData);
			}
			m_config.m_scoreScreenData.m_scoreData = scoreData2;
			m_config.m_scoreScreenData.m_points = scoreData2.Score;
			m_config.m_scoreScreenData.m_starsAwarded = scoreData2.StarRating;
			m_config.m_scoreScreenData.m_unlocks = _unlocks;
			m_scoreScreenFlowroutine.OnRestartRequest = onRestartRequest;
			return m_scoreScreenFlowroutine.BuildFlowroutine(m_config.m_scoreScreenData);
		}
	}
}
