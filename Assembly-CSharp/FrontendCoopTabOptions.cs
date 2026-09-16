using Team17.Online;
using UnityEngine;

public class FrontendCoopTabOptions : FrontendMenuBehaviour
{
	public T17Button m_couchPlayButton;

	public T17Button m_onlinePublicButton;

	public T17Button m_onlinePrivateButton;

	public T17Button m_localPlayButton;

	private T17DialogBox m_progressBox;

	private OnlineMultiplayerSessionVisibility m_PendingVisibility = OnlineMultiplayerSessionVisibility.eClosed;

	private GenericVoid<IConnectionModeSwitchStatus> m_OfflineAgentPrivilegeChecksCompleteCallback;

	private GenericVoid<IConnectionModeSwitchStatus> m_OfflineAgentCouchPlayCompleteCallback;

	private OnlineMultiplayerConnectionMode m_desiredConnectionMode;

	protected override void Awake()
	{
		base.Awake();
		m_OfflineAgentPrivilegeChecksCompleteCallback = OnPrivilegeChecksComplete;
		m_OfflineAgentCouchPlayCompleteCallback = OnRequestConnectionStateOfflineForCouchPlayComplete;
		SetupForConnectionMode();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		ConnectionModeSwitcher.InvalidateCallback(m_OfflineAgentPrivilegeChecksCompleteCallback);
		ConnectionModeSwitcher.InvalidateCallback(m_OfflineAgentCouchPlayCompleteCallback);
	}

	protected override void Start()
	{
		base.Start();
	}

	public void OnCouchPlayClicked()
	{
		if (ConnectionStatus.IsInSession())
		{
			if (UserSystemUtils.AnyRemoteUsers())
			{
				NetworkDialogHelper.ShowGoingOfflineDialog(GoOfflineBeforeCouchPlay);
			}
			else
			{
				GoOfflineBeforeCouchPlay();
			}
		}
		else if (ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession())
		{
			ServerGameSetup.Mode = GameMode.Party;
			m_desiredConnectionMode = OnlineMultiplayerConnectionMode.eNone;
			SetupLobbyInfo(OnlineMultiplayerSessionVisibility.eClosed, m_desiredConnectionMode);
			SetupGameSession();
			ClientTime.Update();
			ServerMessenger.TimeSync(ClientTime.Time());
			ServerMessenger.LoadLevel("Lobbies", GameState.PartyLobby, false);
		}
	}

	private void GoOfflineBeforeCouchPlay()
	{
		ShowProgressSpinnerDialog();
		OnlineMultiplayerConnectionMode value = ((ConnectionStatus.CurrentConnectionMode() == OnlineMultiplayerConnectionMode.eInternet) ? OnlineMultiplayerConnectionMode.eInternet : OnlineMultiplayerConnectionMode.eNone);
		ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Offline, new OfflineOptions
		{
			hostUser = GameUtils.RequireManagerInterface<IPlayerManager>().GetUser(EngagementSlot.One),
			connectionMode = value
		}, m_OfflineAgentCouchPlayCompleteCallback);
	}

	private void OnRequestConnectionStateOfflineForCouchPlayComplete(IConnectionModeSwitchStatus status)
	{
		HideProgressSpinnerDialog();
		if (status.GetResult() == eConnectionModeSwitchResult.Success)
		{
			OnCouchPlayClicked();
		}
	}

	public void OnOnlinePublicClicked()
	{
		if (UserSystemUtils.AtMaxUserCount() && !UserSystemUtils.AnyRemoteUsers())
		{
			NetworkDialogHelper.ShowFullLobbyDialog();
		}
		else if (UserSystemUtils.AnySplitPadUsers())
		{
			NetworkDialogHelper.ShowRemoveSplitPadUsersDialog(StartOnlinePublic, null);
		}
		else
		{
			StartOnlinePublic();
		}
	}

	private void StartOnlinePublic()
	{
		UserSystemUtils.RemoveAllSplitPadGuestUsers();
		m_desiredConnectionMode = OnlineMultiplayerConnectionMode.eInternet;
		if (!ConnectionStatus.IsInSession())
		{
			DoPrivilegeCheck(OnlineMultiplayerSessionVisibility.eMatchmaking);
		}
		else if (ConnectionStatus.IsHost())
		{
			if (ConnectionModeSwitcher.GetRequestedConnectionState() == NetConnectionState.Server)
			{
				ServerOptions serverOptions = (ServerOptions)ConnectionModeSwitcher.GetAgentData();
				serverOptions.visibility = OnlineMultiplayerSessionVisibility.eClosed;
				ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Server, serverOptions);
			}
			LoadLobby(OnlineMultiplayerSessionVisibility.eMatchmaking);
		}
	}

	public void OnOnlinePrivateClicked()
	{
		if (UserSystemUtils.AtMaxUserCount() && !UserSystemUtils.AnyRemoteUsers())
		{
			NetworkDialogHelper.ShowFullLobbyDialog();
		}
		else if (!UserSystemUtils.AnyRemoteUsers())
		{
			NetworkDialogHelper.ShowNoOnlineUsersDialog(OnNotEnoughUsersDialogDismissed);
		}
		else if (UserSystemUtils.AnySplitPadUsers())
		{
			NetworkDialogHelper.ShowRemoveSplitPadUsersDialog(StartPrivateOnline, null);
		}
		else
		{
			StartPrivateOnline();
		}
	}

	private void OnNotEnoughUsersDialogDismissed()
	{
		T17FrontendFlow instance = T17FrontendFlow.Instance;
		if (instance != null)
		{
			instance.FocusOnMultiplayerKitchen();
		}
	}

	private void StartPrivateOnline()
	{
		UserSystemUtils.RemoveAllSplitPadGuestUsers();
		m_desiredConnectionMode = OnlineMultiplayerConnectionMode.eInternet;
		if (ConnectionStatus.IsHost())
		{
			if (ConnectionModeSwitcher.GetRequestedConnectionState() == NetConnectionState.Server)
			{
				ServerOptions serverOptions = (ServerOptions)ConnectionModeSwitcher.GetAgentData();
				serverOptions.visibility = OnlineMultiplayerSessionVisibility.eClosed;
				ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Server, serverOptions);
			}
			LoadLobby(OnlineMultiplayerSessionVisibility.ePrivate);
		}
	}

	public void OnLocalPlayClicked()
	{
	}

	protected void DoPrivilegeCheck(OnlineMultiplayerSessionVisibility _visiblity)
	{
		m_PendingVisibility = _visiblity;
		ShowProgressSpinnerDialog();
		IPlayerManager playerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
		GamepadUser user = playerManager.GetUser(EngagementSlot.One);
		ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Offline, new OfflineOptions
		{
			hostUser = user,
			eAdditionalAction = OfflineOptions.AdditionalAction.PrivilegeCheckAllUsers,
			connectionMode = OnlineMultiplayerConnectionMode.eInternet
		}, m_OfflineAgentPrivilegeChecksCompleteCallback);
	}

	private void OnPrivilegeChecksComplete(IConnectionModeSwitchStatus status)
	{
		if (status.GetResult() == eConnectionModeSwitchResult.Success)
		{
			LoadLobby(m_PendingVisibility);
		}
		else
		{
			CompositeStatus compositeStatus = status as CompositeStatus;
			ConnectionModeStatus connectionModeStatus = null;
			if (compositeStatus != null)
			{
				connectionModeStatus = compositeStatus.m_TaskSubStatus as ConnectionModeStatus;
			}
			if (connectionModeStatus == null || connectionModeStatus.m_Result.m_returnCode != OnlineMultiplayerConnectionModeConnectResult.eCancelledByUser)
			{
				NetworkErrorDialog.ShowDialog(status);
			}
			ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Offline);
		}
		HideProgressSpinnerDialog();
	}

	private void LoadLobby(OnlineMultiplayerSessionVisibility _visiblity)
	{
		if (ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession())
		{
			ServerGameSetup.Mode = GameMode.Party;
		}
		SetupLobbyInfo(_visiblity, m_desiredConnectionMode);
		SetupGameSession();
		ClientTime.Update();
		ServerMessenger.TimeSync(ClientTime.Time());
		ServerMessenger.LoadLevel("Lobbies", GameState.PartyLobby, false);
	}

	protected override void Update()
	{
		base.Update();
		if (m_progressBox != null)
		{
			string localisedProgressDescription = ConnectionModeSwitcher.GetStatus().GetLocalisedProgressDescription();
			m_progressBox.SetMessage(localisedProgressDescription, false);
		}
	}

	private void SetupForConnectionMode()
	{
		m_localPlayButton.gameObject.SetActive(false);
		m_onlinePublicButton.gameObject.SetActive(true);
		m_onlinePrivateButton.gameObject.SetActive(true);
	}

	private void ShowProgressSpinnerDialog()
	{
		if (m_progressBox == null)
		{
			m_progressBox = T17DialogBoxManager.GetDialog(false);
			if (m_progressBox != null)
			{
				m_progressBox.Initialize("Text.PleaseWait", string.Empty, null, null, null, T17DialogBox.Symbols.Spinner, true, false);
				m_progressBox.Show();
			}
		}
	}

	private void HideProgressSpinnerDialog()
	{
		if (m_progressBox != null)
		{
			m_progressBox.Hide();
			m_progressBox = null;
		}
	}

	private void ShowErrorDialog(string strErrorString)
	{
		T17DialogBox dialog = T17DialogBoxManager.GetDialog(false);
		if (dialog != null)
		{
			dialog.Initialize("Text.PleaseWait", strErrorString, null, null, null, T17DialogBox.Symbols.Spinner);
			dialog.Show();
		}
	}

	protected void SetupLobbyInfo(OnlineMultiplayerSessionVisibility _visibility, OnlineMultiplayerConnectionMode _connectionMode)
	{
		LobbySetupInfo instance = LobbySetupInfo.Instance;
		if (instance != null)
		{
			Object.DestroyImmediate(instance.gameObject);
		}
		GameObject gameObject = new GameObject("LobbySetupInfo");
		instance = gameObject.AddComponent<LobbySetupInfo>();
		instance.m_visiblity = _visibility;
		instance.m_connectionMode = _connectionMode;
		instance.m_gameType = GameSession.GameType.Cooperative;
		instance.m_originalConnectionState = ConnectionModeSwitcher.GetRequestedConnectionState();
	}

	protected void SetupGameSession()
	{
		GameSession gameSession = T17FrontendFlow.Instance.StartEmptySession(GameSession.GameType.Cooperative, -1);
		gameSession.TypeSettings.WorldMapScene = "Lobbies";
	}
}
