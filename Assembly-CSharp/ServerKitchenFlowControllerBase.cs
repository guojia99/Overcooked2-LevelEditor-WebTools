using OrderController;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public abstract class ServerKitchenFlowControllerBase : ServerFlowControllerBase, IKitchenOrderHandler
{
	private KitchenFlowControllerBase m_kitchenFlowController;

	private KitchenFlowMessage m_data = new KitchenFlowMessage();

	private const int c_scoreMultiplierMax = 4;

	private const int c_scoreMultiplierMultiplier = 2;

	protected IServerRoundTimer m_roundTimer;

	private PlateReturnController m_plateReturnController;

	public IServerRoundTimer RoundTimer
	{
		get
		{
			return m_roundTimer;
		}
	}

	public override EntityType GetEntityType()
	{
		return EntityType.FlowController;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_kitchenFlowController = (KitchenFlowControllerBase)synchronisedObject;
		m_roundTimer = new ServerRoundTimer();
		m_roundTimer.Initialise();
		PlateReturnController.PlateReturnControllerConfig _plateReturnControllerDesc = default(PlateReturnController.PlateReturnControllerConfig);
		_plateReturnControllerDesc.m_plateReturnTime = (base.LevelConfig as KitchenLevelConfigBase).m_plateReturnTime;
		m_plateReturnController = new PlateReturnController(ref _plateReturnControllerDesc);
		m_plateReturnController.Init();
	}

	private void SendOrderAdded(TeamID _teamID, Serialisable _orderData)
	{
		m_data.Initialise_OrderAdded(_teamID, _orderData);
		SendServerEvent(m_data);
	}

	private void SendOrderExpired(TeamID _teamID, OrderID _orderID)
	{
		m_data.Initialise_OrderExpired(_teamID, _orderID);
		m_data.SetScoreData(GetMonitorForTeam(_teamID).Score);
		SendServerEvent(m_data);
	}

	private void SendDeliverySuccess(TeamID _teamID, ServerPlateStation _plateStation, OrderID _orderID, float _timePropRemainingPercentage, int _tip, bool _wasCombo)
	{
		m_data.Initialise_DeliverySuccess(_teamID, _plateStation.gameObject, _orderID, _timePropRemainingPercentage, _tip, _wasCombo);
		m_data.SetScoreData(GetMonitorForTeam(_teamID).Score);
		SendServerEvent(m_data);
	}

	private void SendDeliveryFailed(TeamID _teamID, ServerPlateStation _plateStation)
	{
		m_data.Initialise_DeliveryFailed(_teamID, _plateStation.gameObject);
		m_data.SetScoreData(GetMonitorForTeam(_teamID).Score);
		SendServerEvent(m_data);
	}

	protected void SendScore(TeamID _teamID)
	{
		m_data.Initialise_ScoreOnly(_teamID);
		m_data.SetScoreData(GetMonitorForTeam(_teamID).Score);
		SendServerEvent(m_data);
	}

	protected virtual ServerOrderControllerBase BuildOrderController(VoidGeneric<OrderID> _addedCallback, VoidGeneric<OrderID> _timeoutCallback)
	{
		ServerFixedTimeOrderController.OrdersConfig _ordersConfig = BuildOrderConfig();
		ServerOrderControllerBase serverOrderControllerBase = new ServerFixedTimeOrderController(ref _ordersConfig, _addedCallback, _timeoutCallback);
		serverOrderControllerBase.SetRoundTimer(m_roundTimer);
		return serverOrderControllerBase;
	}

	protected ServerFixedTimeOrderController.OrdersConfig BuildOrderConfig()
	{
		KitchenLevelConfigBase kitchenLevelConfigBase = base.LevelConfig as KitchenLevelConfigBase;
		ServerFixedTimeOrderController.OrdersConfig result = default(ServerFixedTimeOrderController.OrdersConfig);
		result.m_maxActiveOrders = m_kitchenFlowController.m_maxOrdersAllowed;
		result.m_roundData = kitchenLevelConfigBase.GetRoundData();
		result.m_orderLifetime = kitchenLevelConfigBase.m_orderLifetime;
		result.m_timeBetweenOrders = kitchenLevelConfigBase.m_timeBetweenOrders;
		return result;
	}

	protected override void OnUpdateInRound()
	{
		base.OnUpdateInRound();
		if (m_roundTimer != null)
		{
			m_roundTimer.Update();
		}
		if (m_plateReturnController != null)
		{
			m_plateReturnController.Update();
		}
	}

	protected override bool HasFinished()
	{
		return m_roundTimer.TimeExpired() || base.HasFinished();
	}

	protected virtual void OnOrderAdded(TeamID _teamID, OrderID _orderID)
	{
		ServerTeamMonitor monitorForTeam = GetMonitorForTeam(_teamID);
		Serialisable serialisedOrderData = monitorForTeam.OrdersController.GetSerialisedOrderData(_orderID);
		SendOrderAdded(_teamID, serialisedOrderData);
	}

	protected virtual void OnOrderExpired(TeamID _teamID, OrderID _orderID)
	{
		ServerTeamMonitor monitorForTeam = GetMonitorForTeam(_teamID);
		int recipeTimeOutPointLoss = GameUtils.GetGameConfig().RecipeTimeOutPointLoss;
		monitorForTeam.Score.TotalTimeExpireDeductions += recipeTimeOutPointLoss;
		monitorForTeam.Score.TotalMultiplier = 0;
		monitorForTeam.Score.TotalCombo = 0;
		monitorForTeam.Score.ComboMaintained = false;
		monitorForTeam.OrdersController.ResetOrderLifetime(_orderID);
		SendOrderExpired(_teamID, _orderID);
	}

	public void FoodDelivered(AssembledDefinitionNode _definition, PlatingStepData _plateType, ServerPlateStation _station)
	{
		OnFoodDelivered(_definition, _plateType, _station);
	}

	protected virtual void OnFoodDelivered(AssembledDefinitionNode _definition, PlatingStepData _plateType, ServerPlateStation _station)
	{
		m_plateReturnController.FoodDelivered(_definition, _plateType, _station);
		ServerTeamMonitor monitorForTeam = GetMonitorForTeam(_station.GetTeamID());
		OrderID o_orderID;
		RecipeList.Entry o_entry;
		float o_timePropRemainingPercentage;
		bool o_wasCombo;
		if (monitorForTeam.OnFoodDelivered(_definition, _plateType, out o_orderID, out o_entry, out o_timePropRemainingPercentage, out o_wasCombo))
		{
			OnSuccessfulDelivery(o_orderID, o_entry, o_timePropRemainingPercentage, o_wasCombo, _station);
		}
		else
		{
			OnFailedDelivery(_station);
		}
	}

	protected virtual void OnSuccessfulDelivery(OrderID _orderID, RecipeList.Entry _entry, float _timePropRemainingPercentage, bool _wasCombo, ServerPlateStation _plateStation)
	{
		int num = m_kitchenFlowController.CalculateTip(_timePropRemainingPercentage);
		int num2 = m_kitchenFlowController.CalculateBaseScore(_entry);
		ServerTeamMonitor monitorForTeam = GetMonitorForTeam(_plateStation.GetTeamID());
		int num3 = num * Mathf.Max(monitorForTeam.Score.TotalMultiplier, 1);
		monitorForTeam.Score.TotalBaseScore += num2;
		monitorForTeam.Score.TotalTipsScore += num3;
		monitorForTeam.Score.TotalSuccessfulDeliveries++;
		if (_wasCombo)
		{
			monitorForTeam.Score.TotalCombo++;
			if (monitorForTeam.Score.TotalMultiplier < 4)
			{
				monitorForTeam.Score.TotalMultiplier++;
			}
		}
		else
		{
			monitorForTeam.Score.TotalMultiplier = 0;
			monitorForTeam.Score.TotalCombo = 0;
			monitorForTeam.Score.ComboMaintained = false;
		}
		SendDeliverySuccess(_plateStation.GetTeamID(), _plateStation, _orderID, _timePropRemainingPercentage, num3, _wasCombo);
	}

	protected virtual void OnFailedDelivery(ServerPlateStation _plateStation)
	{
		ServerTeamMonitor monitorForTeam = GetMonitorForTeam(_plateStation.GetTeamID());
		monitorForTeam.Score.TotalMultiplier = 0;
		monitorForTeam.Score.TotalCombo = 0;
		monitorForTeam.Score.ComboMaintained = false;
		SendDeliveryFailed(_plateStation.GetTeamID(), _plateStation);
	}

	public abstract ServerTeamMonitor GetMonitorForTeam(TeamID _team);

	public int GetPoints(TeamID _teamID)
	{
		ServerTeamMonitor monitorForTeam = GetMonitorForTeam(_teamID);
		return monitorForTeam.Score.GetTotalScore();
	}
}
