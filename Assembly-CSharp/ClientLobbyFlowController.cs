#define ANALYTICS
using System;
using System.Collections;
using System.Collections.Generic;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientLobbyFlowController : MonoBehaviour
{
	protected class UserInput
	{
		public ILogicalButton m_changeTeamButton;

		public GamepadUser m_gamepad;

		public User m_user;
	}

	private LobbyFlowController m_lobbyFlow;

	private static ClientLobbyFlowController s_instance;

	private IPlayerManager m_IPlayerManager;

	private LobbySetupInfo m_lobbyInfo;

	private List<UserInput> m_localUserInput = new List<UserInput>();

	private float m_timeLeft;

	protected LobbyFlowController.LobbyState m_state;

	protected LobbyFlowController.ThemeChoice[] m_userChoices;

	protected bool m_bIsCoop = true;

	protected OnlineMultiplayerSessionVisibility m_sessionVisibility;

	protected OnlineMultiplayerConnectionMode m_connectionMode;

	private NetworkErrorDialog m_NetworkErrorDialog = new NetworkErrorDialog();

	public LobbyClientMessage m_message = new LobbyClientMessage();

	private GamepadUser m_lastPadForLeave;

	private ILogicalButton m_leaveButton;

	public T17DialogBox m_leaveDialog;

	private DLCManager m_dlcManager;

	private SceneDirectoryData.LevelTheme m_matchmakeSelection = SceneDirectoryData.LevelTheme.Count;

	public GenericVoid OnLeave;

	private IConnectionModeSwitchStatus m_lastStatus;

	public static ClientLobbyFlowController Instance
	{
		get
		{
			return s_instance;
		}
	}

	public void Awake()
	{
		if (s_instance != null)
		{
			UnityEngine.Object.Destroy(this);
		}
		else
		{
			s_instance = this;
		}
		m_lobbyFlow = LobbyFlowController.Instance;
		m_lobbyInfo = LobbySetupInfo.Instance;
		if (m_lobbyInfo == null)
		{
			GameObject gameObject = new GameObject("LobbySetupInfo");
			m_lobbyInfo = gameObject.AddComponent<LobbySetupInfo>();
			m_lobbyInfo.m_visiblity = OnlineMultiplayerSessionVisibility.eMatchmaking;
			m_lobbyInfo.m_gameType = GameSession.GameType.Cooperative;
		}
		m_lobbyFlow.m_themeSelectMenu.Hide();
		m_lobbyFlow.m_chosenThemes.SetActive(false);
		m_lobbyFlow.m_timerText.enabled = false;
		m_lobbyFlow.m_themeSelectMenu.CarouselButtonClicked += OnThemeButtonClicked;
		UpdateRequirementNotifications();
		m_userChoices = new LobbyFlowController.ThemeChoice[OnlineMultiplayerConfig.MaxPlayers];
		m_IPlayerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Combine(ClientUserSystem.usersChanged, new GenericVoid(OnUsersChanged));
		m_IPlayerManager.EngagementChangeCallback += OnEngagementChanged;
		m_dlcManager = GameUtils.RequireManager<DLCManager>();
		m_NetworkErrorDialog.Enable(OnNetworkErrorDismissed);
		Mailbox.Client.RegisterForMessageType(MessageType.LobbyServer, OnLobbyServerMessage);
	}

	private void OnEngagementChanged(EngagementSlot slot, GamepadUser oldUser, GamepadUser newUser)
	{
		if (oldUser == null && newUser != null)
		{
			SetupUserInput();
		}
	}

	private void Start()
	{
		m_lobbyFlow.m_stateStrings.Update(LobbyFlowController.StateStrings.State.None);
		SetupUserInput();
		if (ClientGameSetup.PrevScene != "StartScreen")
		{
			SaveManager saveManager = GameUtils.RequireManager<SaveManager>();
			saveManager.SaveMetaProgress(OnMetaSaveStatus);
		}
		if (!(m_lobbyInfo != null))
		{
			return;
		}
		m_bIsCoop = m_lobbyInfo.m_gameType == GameSession.GameType.Cooperative;
		m_lobbyFlow.m_lobbyNames.Update(m_bIsCoop);
		m_lobbyFlow.UpdateLegend(m_bIsCoop);
		if (!string.IsNullOrEmpty(NetworkErrors.CachedErrorMessage))
		{
			T17DialogBox dialog = T17DialogBoxManager.GetDialog(false);
			if (null != dialog)
			{
				dialog.Initialize(NetworkErrors.CachedErrorTitle, NetworkErrors.CachedErrorMessage, "Text.Button.Confirm", null, null);
				dialog.Show();
				NetworkErrors.CachedErrorMessage = null;
			}
		}
		if (m_lobbyInfo.m_connectionMode != OnlineMultiplayerConnectionMode.eNone && m_lobbyInfo.m_originalConnectionState != NetConnectionState.Offline && ConnectionModeSwitcher.GetRequestedConnectionState() == NetConnectionState.Offline)
		{
			return;
		}
		if (!ConnectionStatus.IsInSession() || ConnectionStatus.IsHost())
		{
			if (m_lobbyInfo.m_connectionMode == OnlineMultiplayerConnectionMode.eNone)
			{
				if (base.gameObject.GetComponent<ServerLobbyFlowController>() == null)
				{
					base.gameObject.AddComponent<ServerLobbyFlowController>();
				}
			}
			else if (ClientUserSystem.m_Users.Count > 1)
			{
				HostGame();
			}
			else
			{
				TryJoinGame();
			}
		}
		else if (ConnectionStatus.IsInSession() && !ConnectionStatus.IsHost())
		{
			SetState(LobbyFlowController.LobbyState.OnlineSetup);
			m_message.m_type = LobbyClientMessage.LobbyMessageType.StateRequest;
			ClientMessenger.LobbyMessage(m_message);
			UpdateUIColours();
		}
	}

	private void OnMetaSaveStatus(SaveSystemStatus _status)
	{
		if (!(this == null) && !(base.gameObject == null) && _status.Result != SaveLoadResult.Exists)
		{
			Leave();
		}
	}

	protected void OnDestroy()
	{
		if (s_instance == this)
		{
			s_instance = null;
		}
		StopAllCoroutines();
		m_NetworkErrorDialog.Disable();
		ConnectionModeSwitcher.InvalidateCallback(OnRequestConnectionStateJoinComplete);
		ConnectionModeSwitcher.InvalidateCallback(OnRequestConnectionStateServerComplete);
		Mailbox.Client.UnregisterForMessageType(MessageType.LobbyServer, OnLobbyServerMessage);
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Remove(ClientUserSystem.usersChanged, new GenericVoid(OnUsersChanged));
		m_IPlayerManager.EngagementChangeCallback -= OnEngagementChanged;
		HideLeaveDialog();
		m_lobbyFlow.m_themeSelectMenu.CarouselButtonClicked -= OnThemeButtonClicked;
	}

	private void OnThemeButtonClicked(CarouselButton _button)
	{
		ThemeSelectButton themeSelectButton = (ThemeSelectButton)_button;
		if (themeSelectButton != null)
		{
			DlcThemeSelectButton dlcThemeSelectButton = themeSelectButton as DlcThemeSelectButton;
			if (!(dlcThemeSelectButton != null) || !(dlcThemeSelectButton.DLCData != null) || m_dlcManager.IsDLCAvailable(dlcThemeSelectButton.DLCData))
			{
				SelectTheme(themeSelectButton.Theme);
			}
		}
	}

	protected void OnNetworkErrorDismissed()
	{
		ServerGameSetup.Mode = GameMode.OnlineKitchen;
		ServerMessenger.LoadLevel("StartScreen", GameState.MainMenu, true);
		UnityEngine.Object.Destroy(this);
	}

	protected void TryJoinGame()
	{
		SetState(LobbyFlowController.LobbyState.Matchmake);
		IPlayerManager playerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
		IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
		IOnlineMultiplayerSessionCoordinator onlineMultiplayerSessionCoordinator = onlinePlatformManager.OnlineMultiplayerSessionCoordinator();
		if (onlineMultiplayerSessionCoordinator != null)
		{
			ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Matchmake, new MatchmakeData
			{
				gameMode = ((!m_bIsCoop) ? GameMode.Versus : GameMode.Party),
				User = playerManager.GetUser(EngagementSlot.One),
				connectionMode = m_lobbyInfo.m_connectionMode
			}, OnRequestConnectionStateJoinComplete);
		}
	}

	private void OnRequestConnectionStateJoinComplete(IConnectionModeSwitchStatus status)
	{
		if (status.GetResult() == eConnectionModeSwitchResult.Success)
		{
			GameUtils.SendDiagnosticEvent("Automatchmake:Success");
			if (ConnectionStatus.IsHost())
			{
				HostGame();
				return;
			}
			SetState(LobbyFlowController.LobbyState.OnlineSetup);
			m_message.m_type = LobbyClientMessage.LobbyMessageType.StateRequest;
			ClientMessenger.LobbyMessage(m_message);
			m_lobbyFlow.RefreshUserColours(m_bIsCoop);
			UpdateUIColours();
			return;
		}
		if (status.DisplayPlatformDialog())
		{
			GameUtils.SendDiagnosticEvent("Automatchmake:Failure:PlatformError");
			Leave();
			return;
		}
		CompositeStatus compositeStatus = status as CompositeStatus;
		JoinSessionStatus joinSessionStatus = compositeStatus.m_TaskSubStatus as JoinSessionStatus;
		if (joinSessionStatus == null)
		{
			joinSessionStatus = compositeStatus.m_TaskSubStatus as AutoMatchmakingStatus;
		}
		if (joinSessionStatus != null && (joinSessionStatus.sessionJoinResult.m_returnCode == OnlineMultiplayerSessionJoinResult.eLostNetwork || joinSessionStatus.sessionJoinResult.m_returnCode == OnlineMultiplayerSessionJoinResult.eApplicationSuspended || joinSessionStatus.sessionJoinResult.m_returnCode == OnlineMultiplayerSessionJoinResult.eGoneOffline || joinSessionStatus.sessionJoinResult.m_returnCode == OnlineMultiplayerSessionJoinResult.eLoggedOut))
		{
			switch (joinSessionStatus.sessionJoinResult.m_returnCode)
			{
			case OnlineMultiplayerSessionJoinResult.eLostNetwork:
				GameUtils.SendDiagnosticEvent("Automatchmake:Failure:eLostNetwork");
				break;
			case OnlineMultiplayerSessionJoinResult.eApplicationSuspended:
				GameUtils.SendDiagnosticEvent("Automatchmake:Failure:eApplicationSuspended");
				break;
			case OnlineMultiplayerSessionJoinResult.eGoneOffline:
				GameUtils.SendDiagnosticEvent("Automatchmake:Failure:eGoneOffline");
				break;
			case OnlineMultiplayerSessionJoinResult.eLoggedOut:
				GameUtils.SendDiagnosticEvent("Automatchmake:Failure:eLoggedOut");
				break;
			default:
				GameUtils.SendDiagnosticEvent("Automatchmake:Failure:Generic");
				break;
			}
			m_lastStatus = status.Clone();
			ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Offline, null, OnRequestOfflineStateFollowingFailureComplete);
			return;
		}
		if (joinSessionStatus != null)
		{
			switch (joinSessionStatus.sessionJoinResult.m_returnCode)
			{
			case OnlineMultiplayerSessionJoinResult.eFull:
				GameUtils.SendDiagnosticEvent("Automatchmake:Failure:NonFatal_eFull");
				break;
			case OnlineMultiplayerSessionJoinResult.eNoLongerExists:
				GameUtils.SendDiagnosticEvent("Automatchmake:Failure:NonFatal_eNoLongerExists");
				break;
			case OnlineMultiplayerSessionJoinResult.eNoHostConnection:
				GameUtils.SendDiagnosticEvent("Automatchmake:Failure:NonFatal_eNoHostConnection");
				break;
			case OnlineMultiplayerSessionJoinResult.eLoggedOut:
				GameUtils.SendDiagnosticEvent("Automatchmake:Failure:NonFatal_eLoggedOut");
				break;
			case OnlineMultiplayerSessionJoinResult.eGenericFailure:
				GameUtils.SendDiagnosticEvent("Automatchmake:Failure:NonFatal_eGenericFailure");
				break;
			case OnlineMultiplayerSessionJoinResult.eClosed:
				GameUtils.SendDiagnosticEvent("Automatchmake:Failure:NonFatal_eClosed");
				break;
			case OnlineMultiplayerSessionJoinResult.eCodeVersionMismatch:
				GameUtils.SendDiagnosticEvent("Automatchmake:Failure:NonFatal_eCodeVersionMismatch");
				break;
			case OnlineMultiplayerSessionJoinResult.eNotEnoughRoomForAllLocalUsers:
				GameUtils.SendDiagnosticEvent("Automatchmake:Failure:NonFatal_eNotEnoughRoomForAllLocalUsers");
				break;
			default:
				GameUtils.SendDiagnosticEvent("Automatchmake:Failure:NonFatal_Unknown");
				break;
			}
		}
		else if (!GameUtils.s_RoomSearch_NoneAvailable)
		{
			GameUtils.SendDiagnosticEvent("Automatchmake:Failure:NonFatal_NotSpecified");
		}
		HostGame();
	}

	protected void HostGame()
	{
		SetState(LobbyFlowController.LobbyState.Matchmake);
		IPlayerManager playerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
		IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
		IOnlineMultiplayerSessionCoordinator onlineMultiplayerSessionCoordinator = onlinePlatformManager.OnlineMultiplayerSessionCoordinator();
		if (onlineMultiplayerSessionCoordinator != null)
		{
			ServerOptions serverOptions = new ServerOptions
			{
				gameMode = ((!m_bIsCoop) ? GameMode.Versus : GameMode.Party)
			};
			if (m_lobbyInfo.m_visiblity == OnlineMultiplayerSessionVisibility.ePrivate)
			{
				serverOptions.visibility = OnlineMultiplayerSessionVisibility.eClosed;
			}
			else
			{
				serverOptions.visibility = m_lobbyInfo.m_visiblity;
			}
			serverOptions.hostUser = playerManager.GetUser(EngagementSlot.One);
			serverOptions.connectionMode = m_lobbyInfo.m_connectionMode;
			ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Server, serverOptions, OnRequestConnectionStateServerComplete);
		}
	}

	private void OnRequestConnectionStateServerComplete(IConnectionModeSwitchStatus status)
	{
		if (status.GetResult() == eConnectionModeSwitchResult.Success)
		{
			if (base.gameObject.GetComponent<ServerLobbyFlowController>() == null)
			{
				base.gameObject.AddComponent<ServerLobbyFlowController>();
			}
		}
		else
		{
			m_lastStatus = status.Clone();
			ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Offline, null, OnRequestOfflineStateFollowingFailureComplete);
		}
	}

	private void OnRequestOfflineStateFollowingFailureComplete(IConnectionModeSwitchStatus status)
	{
		NetworkErrorDialog.ShowDialog(m_lastStatus);
		m_lastStatus = null;
	}

	protected void OnLobbyServerMessage(IOnlineMultiplayerSessionUserId _sender, Serialisable _data)
	{
		LobbyServerMessage lobbyServerMessage = (LobbyServerMessage)_data;
		if (lobbyServerMessage == null)
		{
			return;
		}
		switch (lobbyServerMessage.m_type)
		{
		case LobbyServerMessage.LobbyMessageType.StateChange:
			m_bIsCoop = lobbyServerMessage.m_stateChange.m_bIsCoop;
			m_sessionVisibility = lobbyServerMessage.m_stateChange.m_sessionVisibility;
			m_connectionMode = lobbyServerMessage.m_stateChange.m_connectionMode;
			m_lobbyFlow.m_lobbyNames.Update(m_bIsCoop);
			if (m_bIsCoop)
			{
				m_lobbyInfo.m_gameType = GameSession.GameType.Cooperative;
			}
			else
			{
				m_lobbyInfo.m_gameType = GameSession.GameType.Competitive;
			}
			m_lobbyFlow.UpdateLegend(m_bIsCoop);
			SetState(lobbyServerMessage.m_stateChange.m_state);
			break;
		case LobbyServerMessage.LobbyMessageType.ResetTimer:
			OnTimerReset(lobbyServerMessage.m_timerInfo.m_timerVal);
			break;
		case LobbyServerMessage.LobbyMessageType.TimerUpdate:
			SetTimer(lobbyServerMessage.m_timerInfo.m_timerVal);
			break;
		case LobbyServerMessage.LobbyMessageType.SelectionUpdate:
			OnThemeSelected(lobbyServerMessage.m_selectionUpdate.m_theme, lobbyServerMessage.m_selectionUpdate.m_chefIndex);
			break;
		case LobbyServerMessage.LobbyMessageType.FinalSelection:
			Mailbox.Client.RegisterForMessageType(MessageType.LevelLoadByIndex, OnLoadLevel);
			Mailbox.Client.RegisterForMessageType(MessageType.LevelLoadByName, OnLoadLevel);
			StartCoroutine(AnimateThemeSelection(lobbyServerMessage.m_selectionUpdate.m_chefIndex));
			break;
		case LobbyServerMessage.LobbyMessageType.CreateGameSession:
			m_lobbyFlow.CreateLobbySession(m_lobbyInfo.m_gameType, lobbyServerMessage.m_dlcID);
			break;
		}
	}

	private void OnLoadLevel(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		UserSystemUtils.BuildGameInputConfig();
		Mailbox.Client.UnregisterForMessageType(MessageType.LevelLoadByIndex, OnLoadLevel);
		Mailbox.Client.UnregisterForMessageType(MessageType.LevelLoadByName, OnLoadLevel);
	}

	protected void OnUsersChanged()
	{
		UpdateRequirementNotifications();
		for (int i = 0; i < m_lobbyFlow.m_themeSelections.Length; i++)
		{
			User user = ((i >= ClientUserSystem.m_Users.Count) ? null : ClientUserSystem.m_Users._items[i]);
			if (user != null && (i == 0 || UserSystemUtils.AnyRemoteUsers()))
			{
				if (m_state != LobbyFlowController.LobbyState.OnlineThemeSelected && m_state != LobbyFlowController.LobbyState.LocalThemeSelected)
				{
					m_lobbyFlow.m_themeSelections[i].gameObject.SetActive(true);
				}
			}
			else
			{
				m_lobbyFlow.m_themeSelections[i].gameObject.SetActive(false);
			}
		}
		SceneDirectoryData.LevelTheme levelTheme = SceneDirectoryData.LevelTheme.Count;
		for (int j = 0; j < ClientUserSystem.m_Users.Count; j++)
		{
			if (m_userChoices[j] != null && ClientUserSystem.m_Users._items[j].IsLocal)
			{
				levelTheme = m_userChoices[j].m_theme;
			}
		}
		if (levelTheme != SceneDirectoryData.LevelTheme.Count)
		{
			for (int k = 0; k < ClientUserSystem.m_Users.Count; k++)
			{
				if (m_userChoices[k] != null && ClientUserSystem.m_Users._items[k].IsLocal)
				{
					m_message.m_type = LobbyClientMessage.LobbyMessageType.ThemeSelected;
					m_message.m_theme = levelTheme;
					m_message.m_chefIndex = k;
					ClientMessenger.LobbyMessage(m_message);
				}
			}
		}
		if (m_lobbyInfo.m_visiblity == OnlineMultiplayerSessionVisibility.ePrivate && m_lobbyFlow.m_uiPlayerRoot.UIPlayers.Count != ClientUserSystem.m_Users.Count)
		{
			m_lobbyFlow.m_uiPlayerRoot.ReCreateUIPlayers();
		}
		m_lobbyFlow.RefreshUserColours(m_bIsCoop);
		SetupUserInput();
		UpdateUIColours();
	}

	private void UpdateUIColours()
	{
		for (int i = 0; i < ClientUserSystem.m_Users.Count; i++)
		{
			User user = ClientUserSystem.m_Users._items[i];
			UIPlayerMenuBehaviour uIPlayerForUser = m_lobbyFlow.m_uiPlayerRoot.GetUIPlayerForUser(user);
			if (uIPlayerForUser != null && user.SelectedChefData != null)
			{
				uIPlayerForUser.UpdateColour();
			}
		}
	}

	protected void SetState(LobbyFlowController.LobbyState _state)
	{
		m_lobbyFlow.m_lobbyNames.Update(m_bIsCoop);
		if (ConnectionStatus.IsInSession() && !ConnectionStatus.IsHost())
		{
			m_lobbyInfo = LobbySetupInfo.Instance;
			bool flag = false;
			if (m_lobbyInfo == null)
			{
				GameObject gameObject = new GameObject("LobbySetupInfo");
				m_lobbyInfo = gameObject.AddComponent<LobbySetupInfo>();
				flag = true;
			}
			GameSession.GameType gameType = ((!m_bIsCoop) ? GameSession.GameType.Competitive : GameSession.GameType.Cooperative);
			if (flag || m_lobbyInfo.m_gameType != gameType || m_lobbyInfo.m_visiblity != m_sessionVisibility || m_lobbyInfo.m_connectionMode != m_connectionMode)
			{
				m_lobbyInfo.m_gameType = gameType;
				m_lobbyInfo.m_visiblity = m_sessionVisibility;
				m_lobbyInfo.m_connectionMode = m_connectionMode;
				m_lobbyFlow.m_uiPlayerRoot.ReCreateUIPlayers();
			}
		}
		LobbyFlowController.LobbyState state = m_state;
		if (m_state == _state)
		{
			return;
		}
		m_state = _state;
		if (m_state == LobbyFlowController.LobbyState.Matchmake || (state != LobbyFlowController.LobbyState.Matchmake && (m_state == LobbyFlowController.LobbyState.LocalSetup || m_state == LobbyFlowController.LobbyState.OnlineSetup)))
		{
			GamepadUser primaryGamepad = GetPrimaryGamepad();
			m_lobbyFlow.m_themeSelectMenu.Show(primaryGamepad, null, null);
			m_lobbyFlow.m_uiPlayerRoot.Show(primaryGamepad, null, null);
			m_lobbyFlow.m_chosenThemes.SetActive(false);
		}
		switch (m_state)
		{
		case LobbyFlowController.LobbyState.LocalSetup:
			m_userChoices = new LobbyFlowController.ThemeChoice[OnlineMultiplayerConfig.MaxPlayers];
			m_lobbyFlow.m_timerText.enabled = false;
			SetupUserInput();
			break;
		case LobbyFlowController.LobbyState.OnlineSetup:
			m_userChoices = new LobbyFlowController.ThemeChoice[OnlineMultiplayerConfig.MaxPlayers];
			m_lobbyFlow.m_timerText.enabled = ClientUserSystem.m_Users.Count > 1;
			SetupUserInput();
			break;
		case LobbyFlowController.LobbyState.Matchmake:
			m_lobbyFlow.m_stateStrings.Update(LobbyFlowController.StateStrings.State.ChooseTheme);
			break;
		case LobbyFlowController.LobbyState.LocalThemeSelection:
			if (ServerUserSystem.m_Users.Count > 0)
			{
				TeamID team2 = ServerUserSystem.m_Users._items[0].Team;
				ServerUserSystem.m_Users._items[0].Team = ((team2 == TeamID.One) ? TeamID.Two : TeamID.One);
				ServerUserSystem.m_Users._items[0].Team = team2;
			}
			m_lobbyFlow.m_stateStrings.Update(LobbyFlowController.StateStrings.State.ChooseTheme);
			if (m_matchmakeSelection != SceneDirectoryData.LevelTheme.Count)
			{
				SelectTheme(m_matchmakeSelection);
			}
			break;
		case LobbyFlowController.LobbyState.OnlineThemeSelection:
			if (ServerUserSystem.m_Users.Count > 0)
			{
				TeamID team = ServerUserSystem.m_Users._items[0].Team;
				ServerUserSystem.m_Users._items[0].Team = ((team == TeamID.One) ? TeamID.Two : TeamID.One);
				ServerUserSystem.m_Users._items[0].Team = team;
			}
			m_lobbyFlow.m_stateStrings.Update(LobbyFlowController.StateStrings.State.ChooseTheme);
			if (m_matchmakeSelection != SceneDirectoryData.LevelTheme.Count)
			{
				SelectTheme(m_matchmakeSelection);
			}
			break;
		case LobbyFlowController.LobbyState.LocalThemeSelected:
			m_lobbyFlow.m_uiPlayerRoot.Show(GetPrimaryGamepad(), null, null);
			ShowSelectedThemes();
			m_lobbyFlow.m_stateStrings.Update(LobbyFlowController.StateStrings.State.PickingLevel);
			break;
		case LobbyFlowController.LobbyState.OnlineThemeSelected:
			m_lobbyFlow.m_timerText.text = "00";
			m_lobbyFlow.m_uiPlayerRoot.Show(GetPrimaryGamepad(), null, null);
			ShowSelectedThemes();
			m_lobbyFlow.m_stateStrings.Update(LobbyFlowController.StateStrings.State.PickingLevel);
			break;
		}
		UpdateRequirementNotifications();
	}

	public void SelectTheme(SceneDirectoryData.LevelTheme _theme)
	{
		if (m_state == LobbyFlowController.LobbyState.Matchmake)
		{
			m_matchmakeSelection = _theme;
			ThemeSelectButton buttonForTheme = m_lobbyFlow.m_themeSelectMenu.GetButtonForTheme(_theme);
			m_lobbyFlow.m_themeSelections[0].Theme = buttonForTheme.Theme;
			m_lobbyFlow.m_themeSelections[0].Sprite = buttonForTheme.ThemeSprite;
		}
		else
		{
			List<int> localChefIndices = GetLocalChefIndices();
			for (int i = 0; i < localChefIndices.Count; i++)
			{
				m_message.m_type = LobbyClientMessage.LobbyMessageType.ThemeSelected;
				m_message.m_theme = _theme;
				m_message.m_chefIndex = localChefIndices[i];
				ClientMessenger.LobbyMessage(m_message);
			}
			Analytics.LogEvent("Theme Vote", "Theme " + Enum.GetName(typeof(SceneDirectoryData.LevelTheme), _theme), (long)_theme);
		}
		m_lobbyFlow.m_uiPlayerRoot.Show(GetPrimaryGamepad(), null, null);
		ShowSelectedThemes();
		m_lobbyFlow.m_stateStrings.Update(LobbyFlowController.StateStrings.State.WaitingForOthers);
		UpdateRequirementNotifications();
	}

	protected void OnThemeSelected(SceneDirectoryData.LevelTheme _theme, int _chefIndex)
	{
		if (_theme != SceneDirectoryData.LevelTheme.Count)
		{
			ThemeSelectButton buttonForTheme = m_lobbyFlow.m_themeSelectMenu.GetButtonForTheme(_theme);
			if (buttonForTheme != null && _chefIndex > -1 && _chefIndex < m_userChoices.Length)
			{
				m_userChoices[_chefIndex] = new LobbyFlowController.ThemeChoice();
				m_userChoices[_chefIndex].m_theme = _theme;
				m_userChoices[_chefIndex].m_chefIndex = _chefIndex;
				m_lobbyFlow.m_themeSelections[_chefIndex].Theme = buttonForTheme.Theme;
				m_lobbyFlow.m_themeSelections[_chefIndex].Sprite = buttonForTheme.ThemeSprite;
			}
		}
		else if (_chefIndex > -1 && _chefIndex < m_userChoices.Length)
		{
			m_userChoices[_chefIndex] = null;
			m_lobbyFlow.m_themeSelections[_chefIndex].Theme = SceneDirectoryData.LevelTheme.Count;
			m_lobbyFlow.m_themeSelections[_chefIndex].Sprite = null;
		}
	}

	protected void ShowSelectedThemes()
	{
		T17EventSystem eventSystemForEngagementSlot = T17EventSystemsManager.Instance.GetEventSystemForEngagementSlot(EngagementSlot.One);
		if (eventSystemForEngagementSlot != null && eventSystemForEngagementSlot.currentSelectedGameObject != null && (eventSystemForEngagementSlot.currentSelectedGameObject == null || eventSystemForEngagementSlot.currentSelectedGameObject.IsInHierarchyOf(m_lobbyFlow.m_themeSelectMenu.gameObject)))
		{
			m_lobbyFlow.m_uiPlayerRoot.FocusOnFirstPlayer(true);
		}
		m_lobbyFlow.m_themeSelectMenu.Hide();
		if (UserSystemUtils.AnyRemoteUsers())
		{
			bool flag = true;
			if (m_lobbyFlow.UnanimousSelection(m_userChoices) && m_state == LobbyFlowController.LobbyState.OnlineThemeSelected)
			{
				flag = false;
			}
			m_lobbyFlow.m_chosenThemes.SetActive(true);
			for (int i = 0; i < m_lobbyFlow.m_themeSelections.Length; i++)
			{
				m_lobbyFlow.m_themeSelections[i].gameObject.SetActive((!flag) ? (i == 0) : (i < ClientUserSystem.m_Users.Count));
			}
		}
		else
		{
			m_lobbyFlow.m_chosenThemes.SetActive(true);
			for (int j = 0; j < m_lobbyFlow.m_themeSelections.Length; j++)
			{
				m_lobbyFlow.m_themeSelections[j].gameObject.SetActive(j == 0);
			}
		}
	}

	protected void UpdateRequirementNotifications()
	{
		if (m_state == LobbyFlowController.LobbyState.PreSetup)
		{
			m_lobbyFlow.m_localPlayerNotification.SetActive(false);
			m_lobbyFlow.m_netPlayerNotification.SetActive(false);
		}
		else if (m_lobbyFlow.IsLocalState(m_state))
		{
			m_lobbyFlow.m_localPlayerNotification.SetActive(!m_bIsCoop && ClientUserSystem.m_Users.Count < 2);
		}
		else
		{
			m_lobbyFlow.m_localPlayerNotification.SetActive(false);
			m_lobbyFlow.m_netPlayerNotification.SetActive(!UserSystemUtils.AnyRemoteUsers());
		}
	}

	private IEnumerator AnimateThemeSelection(int _chefIndex)
	{
		ShowSelectedThemes();
		List<ThemeChoiceElement> activeThemeChoices = new List<ThemeChoiceElement>();
		for (int i = 0; i < m_userChoices.Length; i++)
		{
			if (i < m_lobbyFlow.m_themeSelections.Length && m_userChoices[i] != null)
			{
				ThemeChoiceElement themeChoiceElement = m_lobbyFlow.m_themeSelections[i];
				if (themeChoiceElement != null && m_userChoices[i].m_theme != SceneDirectoryData.LevelTheme.Count && ((!m_lobbyFlow.UnanimousSelection(m_userChoices) && UserSystemUtils.AnyRemoteUsers()) || activeThemeChoices.Count == 0))
				{
					activeThemeChoices.Add(themeChoiceElement);
				}
			}
		}
		if (activeThemeChoices.Count == 1)
		{
			activeThemeChoices[0].ShowAsSelected(true);
			GameUtils.TriggerAudio(GameOneShotAudioTag.UI_Roulette_Confirm, base.gameObject.layer);
		}
		else if (activeThemeChoices.Count > 1)
		{
			AnimationCurve selectionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
			int numIterations = 40 + _chefIndex + 1;
			for (int j = 0; j < numIterations; j++)
			{
				for (int k = 0; k < activeThemeChoices.Count; k++)
				{
					activeThemeChoices[k].ShowAsSelected(k == j % activeThemeChoices.Count);
				}
				GameUtils.TriggerAudio(GameOneShotAudioTag.UI_Roulette_Click, base.gameObject.layer);
				yield return new WaitForSeconds(2f * m_lobbyFlow.m_themeSelectionDuration / (float)numIterations * selectionCurve.Evaluate((float)j / (float)numIterations));
			}
			GameUtils.TriggerAudio(GameOneShotAudioTag.UI_Roulette_Confirm, base.gameObject.layer);
		}
		if (_chefIndex > -1 && _chefIndex < activeThemeChoices.Count)
		{
			m_lobbyFlow.m_stateStrings.Update(LobbyFlowController.StateStrings.State.None);
			ThemeChoiceElement pickedChoice = activeThemeChoices[_chefIndex];
			for (int l = 0; l < m_lobbyFlow.m_themeSelections.Length; l++)
			{
				m_lobbyFlow.m_themeSelections[l].gameObject.SetActive(l == _chefIndex);
			}
			Vector3 selectedEndScale = pickedChoice.transform.localScale * m_lobbyFlow.m_selectedEndScale;
			float progress = 0f;
			while (progress < 1f)
			{
				progress = Mathf.Min(1f, progress + TimeManager.GetDeltaTime(base.gameObject));
				pickedChoice.transform.localScale = Vector3.Lerp(pickedChoice.transform.localScale, selectedEndScale, progress);
				yield return null;
			}
			yield break;
		}
		string text = string.Empty;
		for (int m = 0; m < activeThemeChoices.Count; m++)
		{
			text += activeThemeChoices[m].Theme;
			if (m < activeThemeChoices.Count - 1)
			{
				text += ", ";
			}
		}
	}

	private GamepadUser GetPrimaryGamepad()
	{
		GamepadUser user = m_IPlayerManager.GetUser(EngagementSlot.One);
		if (user != null && T17EventSystemsManager.Instance.GetEventSystemForGamepadUser(user) == null)
		{
			T17EventSystemsManager.Instance.AssignFreeEventSystemToGamepadUser(user);
		}
		return user;
	}

	protected void SetupUserInput()
	{
		for (int i = 0; i < ClientUserSystem.m_Users.Count; i++)
		{
			User user = ClientUserSystem.m_Users._items[i];
			if (!user.IsLocal)
			{
				continue;
			}
			bool flag = true;
			for (int num = m_localUserInput.Count - 1; num >= 0; num--)
			{
				UserInput userInput = m_localUserInput[num];
				if (userInput.m_user == user)
				{
					if (userInput.m_gamepad == user.GamepadUser)
					{
						flag = false;
					}
					else
					{
						m_localUserInput.RemoveAt(num);
					}
					break;
				}
			}
			if (flag)
			{
				UserInput userInput2 = new UserInput();
				if (user.Split == User.SplitStatus.NotSplit || user.Split == User.SplitStatus.SplitPadHost)
				{
					userInput2.m_changeTeamButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.WorkstationInteract, (PlayerInputLookup.Player)user.Engagement);
				}
				else
				{
					userInput2.m_changeTeamButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.PlayerSwitch, (PlayerInputLookup.Player)user.Engagement);
				}
				userInput2.m_gamepad = user.GamepadUser;
				userInput2.m_user = user;
				m_localUserInput.Add(userInput2);
			}
		}
		for (int num2 = m_localUserInput.Count - 1; num2 >= 0; num2--)
		{
			UserInput userInput3 = m_localUserInput[num2];
			if (!userInput3.m_user.IsLocal)
			{
				m_localUserInput.RemoveAt(num2);
			}
		}
		GamepadUser gamepadUser = ((m_IPlayerManager == null) ? null : m_IPlayerManager.GetUser(EngagementSlot.One));
		if (gamepadUser != m_lastPadForLeave)
		{
			m_leaveButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UICancel);
			m_lastPadForLeave = gamepadUser;
		}
	}

	public void ShowLeaveDialog()
	{
		if (m_leaveDialog == null)
		{
			m_leaveDialog = T17DialogBoxManager.GetDialog(false);
			if (m_leaveDialog != null)
			{
				m_leaveDialog.Initialize("Text.Lobby.Leave.Title", "Text.Lobby.Leave.Message", "Text.Button.Leave", null, "Text.Button.Cancel", T17DialogBox.Symbols.Unassigned);
				T17DialogBox leaveDialog = m_leaveDialog;
				leaveDialog.OnConfirm = (T17DialogBox.DialogEvent)Delegate.Combine(leaveDialog.OnConfirm, new T17DialogBox.DialogEvent(OnLeaveConfirmed));
				T17DialogBox leaveDialog2 = m_leaveDialog;
				leaveDialog2.OnCancel = (T17DialogBox.DialogEvent)Delegate.Combine(leaveDialog2.OnCancel, new T17DialogBox.DialogEvent(HideLeaveDialog));
				m_leaveDialog.Show();
			}
		}
	}

	private void HideLeaveDialog()
	{
		if (m_leaveDialog != null)
		{
			if (m_leaveDialog.IsActive)
			{
				m_leaveDialog.Hide();
			}
			m_leaveDialog = null;
		}
	}

	private void OnLeaveConfirmed()
	{
		if (OnLeave != null)
		{
			OnLeave();
		}
		if (ConnectionStatus.IsHost())
		{
			ServerUserSystem.RemoveMatchmadeUsers();
			bool flag = false;
			for (int i = 0; i < ServerUserSystem.m_Users.Count; i++)
			{
				if (!ServerUserSystem.m_Users._items[i].IsLocal)
				{
					flag = true;
					break;
				}
			}
			if (flag)
			{
				IPlayerManager playerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
				ServerOptions serverOptions = (ServerOptions)ConnectionModeSwitcher.GetAgentData();
				serverOptions.visibility = OnlineMultiplayerSessionVisibility.ePrivate;
				serverOptions.gameMode = GameMode.OnlineKitchen;
				serverOptions.hostUser = playerManager.GetUser(EngagementSlot.One);
				ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Server, serverOptions, OnLeaveConfirmedServerConnectionState);
			}
			else
			{
				ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Offline, null, OnLeaveConfirmedOfflineConnectionState);
			}
		}
		else
		{
			ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Offline, null, OnLeaveConfirmedOfflineConnectionState);
		}
	}

	private void OnLeaveConfirmedServerConnectionState(IConnectionModeSwitchStatus result)
	{
		if (result.GetProgress() == eConnectionModeSwitchProgress.Complete && result.GetResult() == eConnectionModeSwitchResult.Success)
		{
			Leave();
		}
		else
		{
			ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Offline, null, OnLeaveConfirmedOfflineConnectionState);
		}
	}

	private void OnLeaveConfirmedOfflineConnectionState(IConnectionModeSwitchStatus result)
	{
		Leave();
	}

	private void Leave()
	{
		ServerGameSetup.Mode = GameMode.OnlineKitchen;
		ServerMessenger.LoadLevel("StartScreen", GameState.MainMenu, false);
		UnityEngine.Object.Destroy(this);
	}

	public void Update()
	{
		if (!T17DialogBoxManager.HasAnyOpenDialogs())
		{
			for (int i = 0; i < m_localUserInput.Count; i++)
			{
				if (m_localUserInput[i].m_changeTeamButton.JustPressed())
				{
					ChangeTeam(m_localUserInput[i].m_user);
				}
			}
		}
		if (m_leaveButton != null && m_leaveButton.JustPressed())
		{
			ShowLeaveDialog();
		}
		switch (m_state)
		{
		case LobbyFlowController.LobbyState.LocalThemeSelection:
			break;
		case LobbyFlowController.LobbyState.OnlineThemeSelection:
			if (UserSystemUtils.AnyRemoteUsers())
			{
				m_lobbyFlow.m_timerText.enabled = true;
				SetTimer(m_timeLeft - TimeManager.GetDeltaTime(base.gameObject));
			}
			else
			{
				m_lobbyFlow.m_timerText.enabled = false;
			}
			break;
		case LobbyFlowController.LobbyState.LocalThemeSelected:
			m_lobbyFlow.m_timerText.enabled = true;
			SetTimer(m_timeLeft - TimeManager.GetDeltaTime(base.gameObject));
			break;
		case LobbyFlowController.LobbyState.OnlineThemeSelected:
			m_lobbyFlow.m_timerText.enabled = true;
			SetTimer(m_timeLeft - TimeManager.GetDeltaTime(base.gameObject));
			break;
		}
	}

	protected virtual void OnTimerReset(float _timerVal)
	{
		SetTimer(_timerVal);
	}

	protected void SetTimer(float _timerVal)
	{
		m_timeLeft = _timerVal;
		if (m_timeLeft > 0f)
		{
			string text = Mathf.RoundToInt(m_timeLeft).ToString();
			text = text.PadLeft(2, '0');
			m_lobbyFlow.m_timerText.text = text;
		}
		else
		{
			m_lobbyFlow.m_timerText.text = "00";
		}
	}

	protected void ChangeTeam(User _user)
	{
		if (!m_bIsCoop && (m_state == LobbyFlowController.LobbyState.LocalThemeSelection || m_state == LobbyFlowController.LobbyState.OnlineThemeSelection))
		{
			int chefIndex = ClientUserSystem.m_Users.FindIndex((User x) => x == _user);
			ChangeTeam(chefIndex);
		}
	}

	protected void ChangeTeam(int _chefIndex)
	{
		if (!m_bIsCoop && (m_state == LobbyFlowController.LobbyState.LocalThemeSelection || m_state == LobbyFlowController.LobbyState.OnlineThemeSelection) && _chefIndex > -1 && _chefIndex < ClientUserSystem.m_Users.Count)
		{
			m_message.m_type = LobbyClientMessage.LobbyMessageType.TeamChangeRequest;
			m_message.m_chefIndex = _chefIndex;
			ClientMessenger.LobbyMessage(m_message);
		}
	}

	protected List<int> GetLocalChefIndices()
	{
		List<int> list = new List<int>();
		for (int i = 0; i < ClientUserSystem.m_Users.Count; i++)
		{
			if (ClientUserSystem.m_Users._items[i] != null && ClientUserSystem.m_Users._items[i].IsLocal)
			{
				list.Add(i);
			}
		}
		return list;
	}
}
