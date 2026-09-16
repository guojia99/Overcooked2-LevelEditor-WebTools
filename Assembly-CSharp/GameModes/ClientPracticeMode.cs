using OrderController;
using UnityEngine;

namespace GameModes
{
	public class ClientPracticeMode : ClientGameModeBase
	{
		private ClientContext m_context;

		private PracticeModeConfig m_config;

		private SessionConfig m_sessionConfig;

		private ClientUnlimitedRoundTimer m_roundTimer;

		private ScoreUIController[] m_scoreUIControllers = new ScoreUIController[0];

		private DisplayTimeUIController[] m_levelTimerUIControllers = new DisplayTimeUIController[0];

		private DataStore m_dataStore;

		private static readonly DataStore.Id k_scoreTipId = new DataStore.Id("score.tip");

		public ClientPracticeMode(Config config)
			: base(config)
		{
			m_config = (PracticeModeConfig)config;
		}

		private ClientOrderControllerBase ClientOrderControllerBuilder(RecipeFlowGUI recipeFlowGUI)
		{
			ClientFixedTimeOrderController clientFixedTimeOrderController = new ClientFixedTimeOrderController(recipeFlowGUI);
			clientFixedTimeOrderController.SetRoundTimer(m_roundTimer);
			return clientFixedTimeOrderController;
		}

		public override void Setup(ClientContext context, SessionConfig config, ref ClientSetupData setupData)
		{
			m_context = context;
			m_sessionConfig = config;
			m_roundTimer = new ClientUnlimitedRoundTimer();
			setupData.m_orderControllerBuilder = ClientOrderControllerBuilder;
			setupData.m_roundTimer = m_roundTimer;
			setupData.m_onSessionConfigChangedCallback = OnSessionConfigChanged;
			setupData.m_onSuccessfulDelivery = OnSuccessfulDelivery;
			setupData.m_onFailedDelivery = OnFailedDelivery;
			setupData.m_onOrderExpired = OnOrderExpired;
			GameObject obj = GameUtils.InstantiateUIControllerOnScalingHUDCanvas(m_config.m_uiPrefab);
			m_scoreUIControllers = obj.RequestComponentsRecursive<ScoreUIController>();
			m_levelTimerUIControllers = obj.RequestComponentsRecursive<DisplayTimeUIController>();
			m_dataStore = GameUtils.RequireManager<DataStore>();
		}

		public override void Begin()
		{
			SetUIControllersActive(m_scoreUIControllers, m_sessionConfig.m_settings[1]);
			SetUIControllersActive(m_levelTimerUIControllers, m_sessionConfig.m_settings[0]);
			for (int i = 0; i < m_context.m_teamCount; i++)
			{
				ClientTeamMonitor monitorForTeam = m_context.m_flowController.GetMonitorForTeam((TeamID)i);
				monitorForTeam.OrdersController.EnableOrderExpiration = m_sessionConfig.m_settings[2];
			}
		}

		private void SetUIControllersActive<T>(T[] uiControllers, bool enable) where T : UIControllerBase
		{
			if (uiControllers != null && uiControllers.Length > 0)
			{
				for (int i = 0; i < uiControllers.Length; i++)
				{
					uiControllers[i].gameObject.SetActive(enable);
				}
			}
		}

		private void OnSessionConfigChanged(SessionConfig config)
		{
			m_sessionConfig = config;
			SetUIControllersActive(m_scoreUIControllers, m_sessionConfig.m_settings[1]);
			SetUIControllersActive(m_levelTimerUIControllers, m_sessionConfig.m_settings[0]);
			for (int i = 0; i < m_context.m_teamCount; i++)
			{
				ClientTeamMonitor monitorForTeam = m_context.m_flowController.GetMonitorForTeam((TeamID)i);
				monitorForTeam.OrdersController.EnableOrderExpiration = m_sessionConfig.m_settings[2];
			}
		}

		private void OnSuccessfulDelivery(TeamID teamId, OrderID orderId, float timePropRemainingPercentage, int tip, bool wasCombo, ClientPlateStation station)
		{
			TeamTip teamTip = new TeamTip
			{
				m_team = teamId,
				m_tip = tip,
				m_station = station
			};
			m_dataStore.Write(k_scoreTipId, teamTip);
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
	}
}
