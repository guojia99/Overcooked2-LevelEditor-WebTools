using OrderController;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public abstract class ClientKitchenFlowControllerBase : ClientFlowControllerBase, IRecipeListCache
{
	public delegate void MealDeliveredCallback(int mealId, bool bWasCombo);

	public delegate void FailedDeliveryCallback();

	private KitchenFlowControllerBase m_kitchenFlowController;

	private DataStore m_dataStore;

	private static readonly DataStore.Id k_scoreTeamId = new DataStore.Id("score.team");

	protected IClientRoundTimer m_roundTimer;

	private OrderDefinitionNode[] m_cachedRecipeList = new OrderDefinitionNode[0];

	private AssembledDefinitionNode[] m_cachedAssembledRecipes = new AssembledDefinitionNode[0];

	private CookingStepData[] m_cachedCookingStepList = new CookingStepData[0];

	public IClientRoundTimer RoundTimer
	{
		get
		{
			return m_roundTimer;
		}
	}

	public event MealDeliveredCallback m_onMealDelivered;

	public event FailedDeliveryCallback m_onFailedDelivery;

	public override EntityType GetEntityType()
	{
		return EntityType.FlowController;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_kitchenFlowController = (KitchenFlowControllerBase)synchronisedObject;
		m_roundTimer = new ClientRoundTimer();
		m_roundTimer.Initialise();
		m_dataStore = GameUtils.RequireManager<DataStore>();
		CacheRecipeListData();
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		KitchenFlowMessage kitchenFlowMessage = (KitchenFlowMessage)serialisable;
		switch (kitchenFlowMessage.m_msgType)
		{
		case KitchenFlowMessage.MsgType.Delivery:
		{
			ClientTeamMonitor monitorForTeam3 = GetMonitorForTeam(kitchenFlowMessage.m_teamID);
			monitorForTeam3.Score.Copy(kitchenFlowMessage.m_teamScore);
			if (kitchenFlowMessage.m_success)
			{
				ClientPlateStation station = kitchenFlowMessage.m_plateStation.RequireComponent<ClientPlateStation>();
				OnSuccessfulDelivery(kitchenFlowMessage.m_teamID, kitchenFlowMessage.m_orderID, kitchenFlowMessage.m_timePropRemainingPercentage, kitchenFlowMessage.m_tip, kitchenFlowMessage.m_wasCombo, station);
			}
			else
			{
				OnFailedDelivery(kitchenFlowMessage.m_teamID, kitchenFlowMessage.m_orderID);
			}
			break;
		}
		case KitchenFlowMessage.MsgType.OrderAdded:
			OnOrderAdded(kitchenFlowMessage.m_teamID, kitchenFlowMessage.m_orderData);
			break;
		case KitchenFlowMessage.MsgType.OrderExpired:
		{
			ClientTeamMonitor monitorForTeam2 = GetMonitorForTeam(kitchenFlowMessage.m_teamID);
			monitorForTeam2.Score.Copy(kitchenFlowMessage.m_teamScore);
			OnOrderExpired(kitchenFlowMessage.m_teamID, kitchenFlowMessage.m_orderID);
			break;
		}
		case KitchenFlowMessage.MsgType.ScoreOnly:
		{
			ClientTeamMonitor monitorForTeam = GetMonitorForTeam(kitchenFlowMessage.m_teamID);
			monitorForTeam.Score.Copy(kitchenFlowMessage.m_teamScore);
			break;
		}
		}
	}

	public OrderDefinitionNode[] GetCachedRecipeList()
	{
		return m_cachedRecipeList;
	}

	public AssembledDefinitionNode[] GetCachedAssembledRecipes()
	{
		return m_cachedAssembledRecipes;
	}

	public CookingStepData[] GetCachedCookingSteps()
	{
		return m_cachedCookingStepList;
	}

	private void CacheRecipeListData()
	{
		LevelConfigBase levelConfig = GetLevelConfig();
		if (levelConfig != null && levelConfig.m_recipeMatchingList != null)
		{
			m_cachedRecipeList = new OrderDefinitionNode[levelConfig.m_recipeMatchingList.m_recipes.Length];
			m_cachedCookingStepList = new CookingStepData[levelConfig.m_recipeMatchingList.m_cookingSteps.Length];
			levelConfig.m_recipeMatchingList.m_recipes.CopyTo(m_cachedRecipeList, 0);
			levelConfig.m_recipeMatchingList.m_cookingSteps.CopyTo(m_cachedCookingStepList, 0);
			for (int i = 0; i < levelConfig.m_recipeMatchingList.m_includeLists.Length; i++)
			{
				m_cachedRecipeList = m_cachedRecipeList.Union(levelConfig.m_recipeMatchingList.m_includeLists[i].m_recipes);
				m_cachedCookingStepList = m_cachedCookingStepList.Union(levelConfig.m_recipeMatchingList.m_includeLists[i].m_cookingSteps);
			}
			m_cachedAssembledRecipes = new AssembledDefinitionNode[m_cachedRecipeList.Length];
			for (int j = 0; j < m_cachedRecipeList.Length; j++)
			{
				m_cachedAssembledRecipes[j] = m_cachedRecipeList[j].Convert().Simpilfy();
			}
		}
	}

	protected override void OnUpdateInRound()
	{
		base.OnUpdateInRound();
		m_roundTimer.Update();
	}

	protected virtual ClientOrderControllerBase BuildOrderController(RecipeFlowGUI _recipeUI)
	{
		ClientFixedTimeOrderController clientFixedTimeOrderController = new ClientFixedTimeOrderController(_recipeUI);
		clientFixedTimeOrderController.SetRoundTimer(m_roundTimer);
		return clientFixedTimeOrderController;
	}

	protected virtual void OnSuccessfulDelivery(TeamID _teamID, OrderID _orderID, float _timePropRemainingPercentage, int _tip, bool _wasCombo, ClientPlateStation _station)
	{
		GameUtils.TriggerAudio(GameOneShotAudioTag.SuccessfulDelivery, base.gameObject.layer);
		ClientTeamMonitor monitorForTeam = GetMonitorForTeam(_teamID);
		int uID = monitorForTeam.OrdersController.GetRecipe(_orderID).m_order.m_uID;
		monitorForTeam.OrdersController.OnFoodDelivered(true, _orderID);
		UpdateScoreUI(_teamID);
		if (this.m_onMealDelivered != null)
		{
			this.m_onMealDelivered(uID, _wasCombo);
		}
		OvercookedAchievementManager overcookedAchievementManager = GameUtils.RequestManager<OvercookedAchievementManager>();
		if (overcookedAchievementManager != null)
		{
			overcookedAchievementManager.IncStat(1, 1f, ControlPadInput.PadNum.One);
			overcookedAchievementManager.AddIDStat(22, uID, ControlPadInput.PadNum.One);
			overcookedAchievementManager.AddIDStat(100, uID, ControlPadInput.PadNum.One);
			overcookedAchievementManager.AddIDStat(500, uID, ControlPadInput.PadNum.One);
			overcookedAchievementManager.AddIDStat(801, uID, ControlPadInput.PadNum.One);
		}
	}

	protected virtual void OnFailedDelivery(TeamID _teamID, OrderID _orderID)
	{
		ClientTeamMonitor monitorForTeam = GetMonitorForTeam(_teamID);
		monitorForTeam.OrdersController.OnFoodDelivered(false, _orderID);
		UpdateScoreUI(_teamID);
		if (this.m_onFailedDelivery != null)
		{
			this.m_onFailedDelivery();
		}
	}

	protected virtual void OnOrderAdded(TeamID _teamID, Serialisable _orderData)
	{
		ClientTeamMonitor monitorForTeam = GetMonitorForTeam(_teamID);
		monitorForTeam.OrdersController.AddNewOrder(_orderData);
		UpdateScoreUI(_teamID);
	}

	protected virtual void OnOrderExpired(TeamID _teamID, OrderID _orderID)
	{
		ClientTeamMonitor monitorForTeam = GetMonitorForTeam(_teamID);
		monitorForTeam.OrdersController.OnOrderExpired(_orderID);
		UpdateScoreUI(_teamID);
	}

	public abstract ClientTeamMonitor GetMonitorForTeam(TeamID _team);

	protected void UpdateScoreUI(TeamID team)
	{
		ClientTeamMonitor monitorForTeam = GetMonitorForTeam(team);
		TeamScore teamScore = new TeamScore
		{
			m_team = team,
			m_score = monitorForTeam.Score
		};
		m_dataStore.Write(k_scoreTeamId, teamScore);
	}

	protected virtual void FinaliseRoundTimer()
	{
		RoundTimer.Zero();
	}
}
