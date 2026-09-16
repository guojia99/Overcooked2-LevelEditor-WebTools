using System;
using OrderController;

namespace GameModes
{
	public class ServerSurvivalMode : ServerGameModeBase
	{
		private class SurvivalModeOutroProvider : CampaignFlowController.IOutroFlowSceneProvider
		{
			public string GetNextScene(out GameState o_loadState, out GameState o_loadEndState, out bool o_useLoadingScreen)
			{
				o_loadState = GameState.CampaignMap;
				o_loadEndState = GameState.RunMapUnfoldRoutine;
				o_useLoadingScreen = true;
				return GameUtils.GetGameSession().TypeSettings.WorldMapScene;
			}
		}

		private SurvivalModeConfig m_config;

		private LevelConfigBase m_levelConfig;

		private ServerModifiableRoundTimer m_roundTimer;

		public ServerSurvivalMode(Config config)
			: base(config)
		{
			m_config = (SurvivalModeConfig)config;
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
			KitchenLevelConfigBase kitchenLevelConfigBase = m_levelConfig as KitchenLevelConfigBase;
			return new ServerFixedTimeOrderController.OrdersConfig
			{
				m_maxActiveOrders = 5,
				m_roundData = kitchenLevelConfigBase.GetRoundData(),
				m_orderLifetime = kitchenLevelConfigBase.m_orderLifetime,
				m_timeBetweenOrders = kitchenLevelConfigBase.m_timeBetweenOrders
			};
		}

		public override void Setup(ServerContext context, SessionConfig config, ref ServerSetupData setupData)
		{
			m_levelConfig = context.m_levelConfig;
			m_roundTimer = new ServerModifiableRoundTimer();
			setupData.m_orderControllerBuilder = ServerOrderControllerBuilder;
			setupData.m_roundTimer = m_roundTimer;
			setupData.m_onSuccessfulDelivery = OnSuccessfulDelivery;
			setupData.m_onOrderExpired = OnOrderExpired;
			setupData.m_onOutroScene = OnOutroScene;
		}

		private void OnSuccessfulDelivery(OrderID orderID, RecipeList.Entry entry, float timePropRemainingPercentage, bool wasCombo, ServerPlateStation station)
		{
			float num = m_config.m_recipeTimes.Get(entry.m_order);
			int num2 = SurvivalModeUtil.CalculateDeliveryBonus(m_config, timePropRemainingPercentage);
			m_roundTimer.AddTime((int)(m_config.m_timeMultiplier * num) + num2);
		}

		private void OnOrderExpired(TeamID teamID, OrderID order)
		{
			m_roundTimer.AddTime(-Math.Abs(m_config.m_recipeTimes.RecipeFailedPenalty));
		}

		private CampaignFlowController.IOutroFlowSceneProvider OnOutroScene()
		{
			return new SurvivalModeOutroProvider();
		}
	}
}
