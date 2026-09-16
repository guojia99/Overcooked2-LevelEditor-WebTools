using OrderController;

namespace GameModes
{
	public class ServerCampaignMode : ServerGameModeBase
	{
		private ServerContext m_context;

		private CampaignModeConfig m_config;

		private Suppressor m_recipeDeliverySuppressor;

		private int m_recipesDelivered;

		private IServerRoundTimer m_roundTimer;

		public ServerCampaignMode(Config config)
			: base(config)
		{
			m_config = (CampaignModeConfig)config;
		}

		private ServerOrderControllerBase ServerOrderControllerBuilder(VoidGeneric<OrderID> _addedCallback, VoidGeneric<OrderID> _timeoutCallback)
		{
			ServerFixedTimeOrderController.OrdersConfig _ordersConfig = BuildOrderConfig();
			ServerOrderControllerBase serverOrderControllerBase = new ServerFixedTimeOrderController(ref _ordersConfig, _addedCallback, _timeoutCallback);
			serverOrderControllerBase.SetRoundTimer(m_roundTimer);
			return serverOrderControllerBase;
		}

		private ServerFixedTimeOrderController.OrdersConfig BuildOrderConfig()
		{
			KitchenLevelConfigBase levelConfig = m_context.m_levelConfig;
			return new ServerFixedTimeOrderController.OrdersConfig
			{
				m_maxActiveOrders = 5,
				m_roundData = levelConfig.GetRoundData(),
				m_orderLifetime = levelConfig.m_orderLifetime,
				m_timeBetweenOrders = levelConfig.m_timeBetweenOrders
			};
		}

		public override void Setup(ServerContext context, SessionConfig config, ref ServerSetupData outputData)
		{
			m_context = context;
			m_roundTimer = new ServerRoundTimer();
			outputData.m_orderControllerBuilder = ServerOrderControllerBuilder;
			outputData.m_roundTimer = m_roundTimer;
			outputData.m_onSuccessfulDelivery = OnSuccessfulDelivery;
		}

		public override void Begin()
		{
			if (ClientGameSetup.Mode == GameMode.Campaign && m_context.m_levelConfig.m_recipesBeforeTimerStarts > 0)
			{
				m_recipeDeliverySuppressor = m_roundTimer.Suppressor.AddSuppressor(m_context.m_gameObject);
			}
		}

		private void OnSuccessfulDelivery(OrderID orderID, RecipeList.Entry entry, float timePropRemainingPercentage, bool wasCombo, ServerPlateStation station)
		{
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
	}
}
