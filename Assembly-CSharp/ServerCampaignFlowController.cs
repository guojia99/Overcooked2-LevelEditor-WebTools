using System;
using GameModes;
using OrderController;
using UnityEngine;

public class ServerCampaignFlowController : ServerKitchenFlowControllerBase
{
	private CampaignFlowController m_campaignFlowController;

	private IServerMode m_gameMode;

	private ServerContext m_gameModeContext;

	private ServerSetupData m_gameModeSetupData = default(ServerSetupData);

	private bool m_LevelRestartRequested;

	private ServerTeamMonitor m_teamMonitor = new ServerTeamMonitor();

	private VoidGeneric<RecipeList.Entry> m_successCallback = delegate
	{
	};

	public void OnLevelRestartRequested()
	{
		m_LevelRestartRequested = true;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_campaignFlowController = (CampaignFlowController)synchronisedObject;
		GameSession gameSession = GameUtils.GetGameSession();
		m_gameMode = gameSession.GetGameModeServer(base.LevelConfig as KitchenLevelConfigBase);
		m_gameModeContext = new ServerContext
		{
			m_gameObject = base.gameObject,
			m_levelConfig = (base.LevelConfig as KitchenLevelConfigBase),
			m_teamCount = 1,
			m_flowController = this
		};
		m_gameMode.Setup(m_gameModeContext, gameSession.GameModeSessionConfig, ref m_gameModeSetupData);
		m_roundTimer = m_gameModeSetupData.m_roundTimer;
		m_roundTimer.Initialise();
		if (m_gameModeSetupData.m_onSessionConfigChangedCallback != null)
		{
			gameSession.OnGameModeSessionConfigChanged = (OnSessionConfigChanged)Delegate.Combine(gameSession.OnGameModeSessionConfigChanged, m_gameModeSetupData.m_onSessionConfigChangedCallback);
		}
		m_teamMonitor.Initialise(m_campaignFlowController.m_teamMonitor, BuildOrderController, delegate(OrderID _order)
		{
			OnOrderAdded(TeamID.One, _order);
		}, delegate(OrderID _order)
		{
			OnOrderExpired(TeamID.One, _order);
		});
		m_gameMode.Begin();
	}

	protected override ServerOrderControllerBase BuildOrderController(VoidGeneric<OrderID> _addedCallback, VoidGeneric<OrderID> _timeoutCallback)
	{
		return m_gameModeSetupData.m_orderControllerBuilder(_addedCallback, _timeoutCallback);
	}

	public override void StopSynchronising()
	{
		GameSession gameSession = GameUtils.GetGameSession();
		if (m_gameModeSetupData.m_onSessionConfigChangedCallback != null)
		{
			gameSession.OnGameModeSessionConfigChanged = (OnSessionConfigChanged)Delegate.Remove(gameSession.OnGameModeSessionConfigChanged, m_gameModeSetupData.m_onSessionConfigChangedCallback);
		}
		if (m_gameMode != null)
		{
			m_gameMode.End();
		}
		base.StopSynchronising();
	}

	public void RegisterOnSuccessfulDeliveryCallback(VoidGeneric<RecipeList.Entry> _callback)
	{
		m_successCallback = (VoidGeneric<RecipeList.Entry>)Delegate.Combine(m_successCallback, _callback);
	}

	public void UnregisterOnSuccessfulDeliveryCallback(VoidGeneric<RecipeList.Entry> _callback)
	{
		m_successCallback = (VoidGeneric<RecipeList.Entry>)Delegate.Remove(m_successCallback, _callback);
	}

	protected override void OnUpdateInRound()
	{
		base.OnUpdateInRound();
		m_teamMonitor.Update();
		m_gameMode.Update();
	}

	public override ServerTeamMonitor GetMonitorForTeam(TeamID _team)
	{
		return m_teamMonitor;
	}

	protected override void OnSuccessfulDelivery(OrderID _orderID, RecipeList.Entry _entry, float _timePropRemainingPercentage, bool _wasCombo, ServerPlateStation _plateStation)
	{
		if (m_gameModeSetupData.m_onSuccessfulDelivery != null)
		{
			m_gameModeSetupData.m_onSuccessfulDelivery(_orderID, _entry, _timePropRemainingPercentage, _wasCombo, _plateStation);
		}
		m_successCallback(_entry);
		base.OnSuccessfulDelivery(_orderID, _entry, _timePropRemainingPercentage, _wasCombo, _plateStation);
	}

	protected override void OnFailedDelivery(ServerPlateStation _plateStation)
	{
		if (m_gameModeSetupData.m_onFailedDelivery != null)
		{
			m_gameModeSetupData.m_onFailedDelivery(_plateStation);
		}
		base.OnFailedDelivery(_plateStation);
	}

	protected override void OnOrderAdded(TeamID _teamID, OrderID _orderID)
	{
		if (m_gameModeSetupData.m_onOrderAdded != null)
		{
			m_gameModeSetupData.m_onOrderAdded(_teamID, _orderID);
		}
		base.OnOrderAdded(_teamID, _orderID);
	}

	protected override void OnOrderExpired(TeamID _teamID, OrderID _orderID)
	{
		if (m_gameModeSetupData.m_onOrderExpired != null)
		{
			m_gameModeSetupData.m_onOrderExpired(_teamID, _orderID);
		}
		base.OnOrderExpired(_teamID, _orderID);
	}

	public virtual void SkipLevel(int _stars)
	{
		GameSession gameSession = GameUtils.GetGameSession();
		GameSession.GameLevelSettings levelSettings = gameSession.LevelSettings;
		SceneDirectoryData.PerPlayerCountDirectoryEntry sceneDirectoryVarientEntry = levelSettings.SceneDirectoryVarientEntry;
		int pointsForStar = sceneDirectoryVarientEntry.GetPointsForStar(_stars);
		m_teamMonitor.Score.TotalBaseScore = pointsForStar;
		m_teamMonitor.Score.TotalTipsScore = 0;
		m_teamMonitor.Score.TotalTimeExpireDeductions = 0;
		SkipToEnd();
	}

	public override void SkipToEnd()
	{
		SendScore(TeamID.One);
		base.SkipToEnd();
	}

	protected override string GetNextScene(out GameState o_loadState, out GameState o_loadEndState, out bool o_useLoadingScreen)
	{
		o_loadState = GameState.NotSet;
		o_loadEndState = GameState.NotSet;
		o_useLoadingScreen = true;
		if (m_LevelRestartRequested)
		{
			o_loadState = GameState.LoadKitchen;
			o_loadEndState = GameState.RunLevelIntro;
			o_useLoadingScreen = true;
			return GameUtils.GetGameSession().LevelSettings.SceneDirectoryVarientEntry.SceneName;
		}
		if (ServerGameSetup.Mode == GameMode.Campaign)
		{
			CampaignFlowController.IOutroFlowSceneProvider outroFlowSceneProvider = null;
			outroFlowSceneProvider = ((m_gameModeSetupData.m_onOutroScene == null) ? base.gameObject.RequestInterface<CampaignFlowController.IOutroFlowSceneProvider>() : m_gameModeSetupData.m_onOutroScene());
			if (outroFlowSceneProvider != null)
			{
				return outroFlowSceneProvider.GetNextScene(out o_loadState, out o_loadEndState, out o_useLoadingScreen);
			}
			o_loadState = GameState.CampaignMap;
			o_loadEndState = GameState.RunMapUnfoldRoutine;
			return GameUtils.GetGameSession().TypeSettings.WorldMapScene;
		}
		if (ServerGameSetup.Mode == GameMode.Party)
		{
			o_loadState = GameState.PartyLobby;
			o_loadEndState = GameState.NotSet;
			return GameUtils.GetGameSession().TypeSettings.WorldMapScene;
		}
		return string.Empty;
	}

	public void SetOrdersAutoProgress(bool _autoProgress)
	{
		m_teamMonitor.OrdersController.SetAutoProgress(_autoProgress);
	}

	public void AddNextOrder()
	{
		m_teamMonitor.OrdersController.AddNewOrder();
	}
}
