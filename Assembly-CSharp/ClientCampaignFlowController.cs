using System;
using System.Collections;
using GameModes;
using OrderController;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientCampaignFlowController : ClientKitchenFlowControllerBase
{
	private CampaignFlowController m_campaignFlowController;

	private IClientMode m_gameMode;

	private ClientContext m_gameModeContext;

	private ClientSetupData m_gameModeSetupData = default(ClientSetupData);

	private ClientTeamMonitor m_teamMonitor = new ClientTeamMonitor();

	private VoidGeneric<RecipeList.Entry> m_successCallback = delegate
	{
	};

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_campaignFlowController = (CampaignFlowController)synchronisedObject;
		GameSession gameSession = GameUtils.GetGameSession();
		m_gameMode = gameSession.GetGameModeClient(base.LevelConfig as KitchenLevelConfigBase);
		m_gameModeContext = new ClientContext
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
		m_teamMonitor.Initialise(m_campaignFlowController.m_teamMonitor, TeamID.One, BuildOrderController);
		m_gameMode.Begin();
	}

	protected override ClientOrderControllerBase BuildOrderController(RecipeFlowGUI _recipeUI)
	{
		return m_gameModeSetupData.m_orderControllerBuilder(_recipeUI);
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

	public override ClientTeamMonitor GetMonitorForTeam(TeamID _team)
	{
		return m_teamMonitor;
	}

	protected override void OnUpdateInRound()
	{
		base.OnUpdateInRound();
		m_teamMonitor.Update();
		m_gameMode.Update();
	}

	protected override void OnSuccessfulDelivery(TeamID _teamID, OrderID _orderID, float _timePropRemainingPercentage, int _tip, bool _wasCombo, ClientPlateStation _station)
	{
		RecipeList.Entry recipe = m_teamMonitor.OrdersController.GetRecipe(_orderID);
		if (m_gameModeSetupData.m_onSuccessfulDelivery != null)
		{
			m_gameModeSetupData.m_onSuccessfulDelivery(_teamID, _orderID, _timePropRemainingPercentage, _tip, _wasCombo, _station);
		}
		m_successCallback(recipe);
		base.OnSuccessfulDelivery(_teamID, _orderID, _timePropRemainingPercentage, _tip, _wasCombo, _station);
	}

	protected override void OnFailedDelivery(TeamID _teamID, OrderID _orderID)
	{
		if (m_gameModeSetupData.m_onFailedDelivery != null)
		{
			m_gameModeSetupData.m_onFailedDelivery(_teamID, _orderID);
		}
		base.OnFailedDelivery(_teamID, _orderID);
	}

	protected override void OnOrderAdded(TeamID _teamID, Serialisable _orderData)
	{
		if (m_gameModeSetupData.m_onOrderAdded != null)
		{
			m_gameModeSetupData.m_onOrderAdded(_teamID, _orderData);
		}
		base.OnOrderAdded(_teamID, _orderData);
	}

	protected override void OnOrderExpired(TeamID _teamID, OrderID _orderID)
	{
		if (m_gameModeSetupData.m_onOrderExpired != null)
		{
			m_gameModeSetupData.m_onOrderExpired(_teamID, _orderID);
		}
		base.OnOrderExpired(_teamID, _orderID);
	}

	protected override IEnumerator RunLevelOutro()
	{
		FinaliseRoundTimer();
		TeamMonitor.TeamScoreStats score = m_teamMonitor.Score;
		if (m_gameModeSetupData.m_onOutro != null)
		{
			return m_gameModeSetupData.m_onOutro(OnLevelRestartRequested, GetStarRating(score.GetTotalScore()));
		}
		return null;
	}

	private void OnLevelRestartRequested()
	{
		bool flag = !ConnectionStatus.IsHost() && ConnectionStatus.IsInSession();
		bool flag2 = ClientGameSetup.Mode != GameMode.Campaign;
		if (!flag && !flag2)
		{
			base.gameObject.RequireComponent<ServerCampaignFlowController>().OnLevelRestartRequested();
		}
	}

	protected virtual int GetStarRating(int _points)
	{
		GameSession gameSession = GameUtils.GetGameSession();
		GameSession.GameLevelSettings levelSettings = gameSession.LevelSettings;
		SceneDirectoryData.PerPlayerCountDirectoryEntry sceneDirectoryVarientEntry = levelSettings.SceneDirectoryVarientEntry;
		bool inNGPlus = gameSession.Progress.SaveData.IsNGPEnabledForLevel(GameUtils.GetLevelID()) && gameSession.Progress.SaveData.NewGamePlusDialogShown;
		return sceneDirectoryVarientEntry.GetStarForPoints(_points, inNGPlus);
	}
}
