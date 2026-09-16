using OrderController;

namespace GameModes
{
	public class ServerPracticeMode : ServerGameModeBase
	{
		private ServerContext m_context;

		private PracticeModeConfig m_config;

		private SessionConfig m_sessionConfig;

		private ServerUnlimitedRoundTimer m_roundTimer;

		public ServerPracticeMode(Config config)
			: base(config)
		{
			m_config = (PracticeModeConfig)config;
		}

		private ServerOrderControllerBase ServerOrderControllerBuilder(VoidGeneric<OrderID> addedCallback, VoidGeneric<OrderID> timeoutCallback)
		{
			KitchenLevelConfigBase levelConfig = m_context.m_levelConfig;
			ServerFixedTimeOrderController.OrdersConfig _ordersConfig = new ServerFixedTimeOrderController.OrdersConfig
			{
				m_maxActiveOrders = 5,
				m_roundData = levelConfig.GetRoundData(),
				m_orderLifetime = levelConfig.m_orderLifetime,
				m_timeBetweenOrders = levelConfig.m_timeBetweenOrders
			};
			ServerFixedTimeOrderController serverFixedTimeOrderController = new ServerFixedTimeOrderController(ref _ordersConfig, addedCallback, timeoutCallback);
			serverFixedTimeOrderController.SetRoundTimer(m_roundTimer);
			return serverFixedTimeOrderController;
		}

		public override void Setup(ServerContext context, SessionConfig sessionConfig, ref ServerSetupData outputData)
		{
			m_context = context;
			m_sessionConfig = sessionConfig;
			m_roundTimer = new ServerUnlimitedRoundTimer();
			outputData.m_orderControllerBuilder = ServerOrderControllerBuilder;
			outputData.m_roundTimer = m_roundTimer;
			outputData.m_onSessionConfigChangedCallback = OnSessionConfigChanged;
		}

		public override void Begin()
		{
			GameSession gameSession = GameUtils.GetGameSession();
			for (int i = 0; i < m_context.m_teamCount; i++)
			{
				ServerTeamMonitor monitorForTeam = m_context.m_flowController.GetMonitorForTeam((TeamID)i);
				monitorForTeam.OrdersController.EnableOrderExpiration = m_sessionConfig.m_settings[2];
			}
		}

		private void OnSessionConfigChanged(SessionConfig sessionConfig)
		{
			m_sessionConfig = sessionConfig;
			for (int i = 0; i < m_context.m_teamCount; i++)
			{
				ServerTeamMonitor monitorForTeam = m_context.m_flowController.GetMonitorForTeam((TeamID)i);
				monitorForTeam.OrdersController.EnableOrderExpiration = m_sessionConfig.m_settings[2];
			}
		}
	}
}
