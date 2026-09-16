using System;
using OrderController;
using UnityEngine;

namespace GameModes
{
	public class ClientSurvivalMode : ClientGameModeBase
	{
		private ClientContext m_context;

		private SurvivalModeConfig m_config;

		private ClientModifiableRoundTimer m_roundTimer;

		private SurvivalModeOutroFlowroutine m_outroFlowroutine;

		private float m_timeSurvived;

		private DataStore m_dataStore;

		private static readonly DataStore.Id k_timeSurvivedId = new DataStore.Id("time.survived");

		private int m_defaultLayer;

		public ClientSurvivalMode(Config config)
			: base(config)
		{
			m_config = (SurvivalModeConfig)config;
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
			m_roundTimer = new ClientModifiableRoundTimer();
			setupData.m_orderControllerBuilder = ClientOrderControllerBuilder;
			setupData.m_roundTimer = m_roundTimer;
			setupData.m_onSuccessfulDelivery = OnSuccessfulDelivery;
			setupData.m_onOrderExpired = OnOrderExpired;
			setupData.m_onOutro = OnOutro;
			GameObject source = ((context.m_teamCount != 1) ? m_config.m_competitiveUIPrefab : m_config.m_uiPrefab);
			GameUtils.InstantiateUIControllerOnScalingHUDCanvas(source);
			m_outroFlowroutine = new SurvivalModeOutroFlowroutine();
			m_dataStore = GameUtils.RequireManager<DataStore>();
			m_defaultLayer = LayerMask.NameToLayer("Default");
		}

		public override void Update()
		{
			if (!TimeManager.IsPaused(TimeManager.PauseLayer.Main))
			{
				m_timeSurvived += TimeManager.GetDeltaTime(m_defaultLayer);
				m_dataStore.Write(k_timeSurvivedId, m_timeSurvived);
			}
		}

		private void OnSuccessfulDelivery(TeamID teamID, OrderID orderID, float timePropRemainingPercentage, int tip, bool wasCombo, ClientPlateStation station)
		{
			ClientTeamMonitor monitorForTeam = m_context.m_flowController.GetMonitorForTeam(teamID);
			RecipeList.Entry recipe = monitorForTeam.OrdersController.GetRecipe(orderID);
			float num = m_config.m_recipeTimes.Get(recipe.m_order);
			int num2 = SurvivalModeUtil.CalculateDeliveryBonus(m_config, timePropRemainingPercentage);
			m_roundTimer.AddTime((int)(m_config.m_timeMultiplier * num) + num2);
		}

		private void OnOrderExpired(TeamID teamID, OrderID orderID)
		{
			m_roundTimer.AddTime(-Math.Abs(m_config.m_recipeTimes.RecipeFailedPenalty));
		}

		private IFlowroutine OnOutro(GenericVoid onRestartRequest, int starRating)
		{
			ClientTeamMonitor monitorForTeam = m_context.m_flowController.GetMonitorForTeam(TeamID.One);
			GameSession gameSession = GameUtils.GetGameSession();
			TeamMonitor.TeamScoreStats score = monitorForTeam.Score;
			int levelID = GameUtils.GetLevelID();
			GameProgress.GameProgressData.LevelProgress levelProgress = gameSession.Progress.SaveData.GetLevelProgress(levelID);
			gameSession.Progress.RecordLevelScore(new GameProgress.HighScores.Score
			{
				iLevelID = levelID,
				iHighScore = int.MinValue,
				iSurvivalModeTime = Mathf.RoundToInt(m_timeSurvived)
			});
			SurvivalModeRatingUIController.ScoreData scoreData = new SurvivalModeRatingUIController.ScoreData
			{
				m_timeSurvived = Mathf.RoundToInt(m_timeSurvived),
				m_successPoints = score.TotalBaseScore,
				m_failDeductions = score.TotalTimeExpireDeductions,
				m_tips = score.TotalTipsScore,
				m_score = score.GetTotalScore(),
				m_totalSuccessfulDeliveries = score.TotalSuccessfulDeliveries
			};
			m_config.m_outroFlowroutineData.m_scoreData = scoreData;
			m_outroFlowroutine.OnRestartRequest = onRestartRequest;
			return m_outroFlowroutine.BuildFlowroutine(m_config.m_outroFlowroutineData);
		}
	}
}
