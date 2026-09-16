using System;
using System.Collections;
using System.Collections.Generic;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerLobbyFlowController : MonoBehaviour
{
	private LobbyFlowController m_lobbyFlow;

	private static ServerLobbyFlowController s_instance;

	private IPlayerManager m_IPlayerManager;

	private LobbySetupInfo m_lobbyInfo;

	private LobbyServerMessage m_message = new LobbyServerMessage();

	private float m_timeLeft;

	protected LobbyFlowController.LobbyState m_state;

	protected LobbyFlowController.ThemeChoice[] m_userChoices;

	protected bool m_bIsCoop = true;

	private Coroutine m_delayedLevelLoad;

	public static ServerLobbyFlowController Instance
	{
		get
		{
			return s_instance;
		}
	}

	private void Awake()
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
		m_IPlayerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
		m_lobbyInfo = LobbySetupInfo.Instance;
		if (m_lobbyInfo == null)
		{
			GameObject gameObject = new GameObject("LobbySetupInfo");
			m_lobbyInfo = gameObject.AddComponent<LobbySetupInfo>();
			m_lobbyInfo.m_visiblity = OnlineMultiplayerSessionVisibility.eMatchmaking;
			m_lobbyInfo.m_gameType = GameSession.GameType.Cooperative;
		}
		ServerUserSystem.OnUserAdded = (GenericVoid<User>)Delegate.Combine(ServerUserSystem.OnUserAdded, new GenericVoid<User>(OnUserAdded));
		ServerUserSystem.OnUserRemovedWithIndex = (GenericVoid<User, int>)Delegate.Combine(ServerUserSystem.OnUserRemovedWithIndex, new GenericVoid<User, int>(OnUserRemoved));
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Combine(ClientUserSystem.usersChanged, new GenericVoid(OnUsersChanged));
		Mailbox.Server.RegisterForMessageType(MessageType.LobbyClient, OnLobbyClientMessage);
		ClientLobbyFlowController clientLobbyFlowController = base.gameObject.RequireComponent<ClientLobbyFlowController>();
		clientLobbyFlowController.OnLeave = (GenericVoid)Delegate.Combine(clientLobbyFlowController.OnLeave, new GenericVoid(OnLeave));
		PreSetup();
	}

	protected void OnLobbyClientMessage(IOnlineMultiplayerSessionUserId _sender, Serialisable _data)
	{
		LobbyClientMessage lobbyClientMessage = (LobbyClientMessage)_data;
		if (lobbyClientMessage == null)
		{
			return;
		}
		switch (lobbyClientMessage.m_type)
		{
		case LobbyClientMessage.LobbyMessageType.ThemeSelected:
		{
			SceneDirectoryData.LevelTheme theme = lobbyClientMessage.m_theme;
			int chefIndex = lobbyClientMessage.m_chefIndex;
			SelectTheme(theme, chefIndex);
			break;
		}
		case LobbyClientMessage.LobbyMessageType.StateRequest:
			SetState(m_state);
			InformAllOfCurrentSelections();
			break;
		case LobbyClientMessage.LobbyMessageType.TeamChangeRequest:
			if (m_state == LobbyFlowController.LobbyState.LocalThemeSelection || m_state == LobbyFlowController.LobbyState.OnlineThemeSelection)
			{
				ChangeTeam(lobbyClientMessage.m_chefIndex);
			}
			break;
		}
	}

	protected void PreSetup()
	{
		m_bIsCoop = m_lobbyInfo.m_gameType == GameSession.GameType.Cooperative;
		for (int i = 0; i < ServerUserSystem.m_Users.Count; i++)
		{
			ServerUserSystem.m_Users._items[i].Team = TeamID.None;
		}
		if (!m_bIsCoop)
		{
			for (int j = 0; j < ServerUserSystem.m_Users.Count; j++)
			{
				ServerUserSystem.m_Users._items[j].Colour = m_lobbyFlow.m_noTeamColourIndex;
			}
		}
		ResetTimer(m_lobbyFlow.m_timeLimit);
		if (ConnectionStatus.IsInSession())
		{
			SetState(LobbyFlowController.LobbyState.OnlineSetup);
		}
		else
		{
			SetState(LobbyFlowController.LobbyState.LocalSetup);
		}
	}

	protected void SetState(LobbyFlowController.LobbyState _state)
	{
		LobbyFlowController.LobbyState state = m_state;
		m_state = _state;
		OnlineMultiplayerSessionVisibility sessionVisibility = m_lobbyInfo.m_visiblity;
		if (m_state == LobbyFlowController.LobbyState.OnlineThemeSelected)
		{
			sessionVisibility = OnlineMultiplayerSessionVisibility.eClosed;
		}
		m_message.m_type = LobbyServerMessage.LobbyMessageType.StateChange;
		m_message.m_stateChange.m_state = _state;
		m_message.m_stateChange.m_bIsCoop = m_bIsCoop;
		m_message.m_stateChange.m_sessionVisibility = sessionVisibility;
		m_message.m_stateChange.m_connectionMode = m_lobbyInfo.m_connectionMode;
		ServerMessenger.LobbyMessage(m_message);
		if (state != m_state)
		{
			switch (m_state)
			{
			case LobbyFlowController.LobbyState.LocalSetup:
				m_userChoices = new LobbyFlowController.ThemeChoice[OnlineMultiplayerConfig.MaxPlayers];
				ResetTimer(m_lobbyFlow.m_timeLimit);
				SetState(LobbyFlowController.LobbyState.LocalThemeSelection);
				break;
			case LobbyFlowController.LobbyState.OnlineSetup:
				m_userChoices = new LobbyFlowController.ThemeChoice[OnlineMultiplayerConfig.MaxPlayers];
				ResetTimer(m_lobbyFlow.m_timeLimit);
				SetState(LobbyFlowController.LobbyState.OnlineThemeSelection);
				break;
			case LobbyFlowController.LobbyState.LocalThemeSelection:
				break;
			case LobbyFlowController.LobbyState.OnlineThemeSelection:
				break;
			case LobbyFlowController.LobbyState.LocalThemeSelected:
				ResetTimer(m_lobbyFlow.m_themeSelectionDuration);
				StartLevel();
				break;
			case LobbyFlowController.LobbyState.OnlineThemeSelected:
			{
				IPlayerManager playerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
				ServerOptions serverOptions = new ServerOptions
				{
					gameMode = ((!m_bIsCoop) ? GameMode.Versus : GameMode.Party),
					visibility = OnlineMultiplayerSessionVisibility.eClosed,
					hostUser = playerManager.GetUser(EngagementSlot.One),
					connectionMode = m_lobbyInfo.m_connectionMode
				};
				ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Server, serverOptions);
				OnlineMultiplayerSessionVisibility visiblity = m_lobbyInfo.m_visiblity;
				m_lobbyInfo.m_visiblity = serverOptions.visibility;
				m_lobbyFlow.m_uiPlayerRoot.ReCreateUIPlayers();
				m_lobbyInfo.m_visiblity = visiblity;
				ResetTimer(m_lobbyFlow.m_themeSelectionDuration);
				StartLevel();
				break;
			}
			}
		}
	}

	protected void FillUndecidedUserChoices()
	{
		for (int i = 0; i < ServerUserSystem.m_Users.Count; i++)
		{
			if (ServerUserSystem.m_Users._items[i] != null && (m_userChoices[i] == null || m_userChoices[i].m_theme == SceneDirectoryData.LevelTheme.Count))
			{
				SelectTheme(SceneDirectoryData.LevelTheme.Random, i);
			}
		}
	}

	protected void ResetTimer(float time)
	{
		m_timeLeft = time;
		m_message.m_type = LobbyServerMessage.LobbyMessageType.ResetTimer;
		m_message.m_timerInfo = default(LobbyServerMessage.TimerInfo);
		m_message.m_timerInfo.m_timerVal = m_timeLeft;
		ServerMessenger.LobbyMessage(m_message);
	}

	public void SelectTheme(SceneDirectoryData.LevelTheme _theme, int _chefIndex)
	{
		if (_chefIndex > -1 && _chefIndex < m_userChoices.Length)
		{
			m_userChoices[_chefIndex] = new LobbyFlowController.ThemeChoice();
			m_userChoices[_chefIndex].m_theme = _theme;
			m_userChoices[_chefIndex].m_chefIndex = _chefIndex;
			SendAllUserThemeSelection(_theme, _chefIndex);
		}
		if (AllUsersSelected() && (!UserSystemUtils.AnyRemoteUsers() || m_lobbyInfo.m_visiblity == OnlineMultiplayerSessionVisibility.ePrivate || ServerUserSystem.m_Users.Count == 4))
		{
			if (m_state == LobbyFlowController.LobbyState.LocalThemeSelection)
			{
				SetState(LobbyFlowController.LobbyState.LocalThemeSelected);
			}
			else if (m_state == LobbyFlowController.LobbyState.OnlineThemeSelection)
			{
				SetState(LobbyFlowController.LobbyState.OnlineThemeSelected);
			}
		}
	}

	protected void SendAllUserThemeSelection(SceneDirectoryData.LevelTheme _theme, int _chefIndex)
	{
		m_message.m_type = LobbyServerMessage.LobbyMessageType.SelectionUpdate;
		m_message.m_selectionUpdate = default(LobbyServerMessage.SelectionUpdate);
		m_message.m_selectionUpdate.m_theme = _theme;
		m_message.m_selectionUpdate.m_chefIndex = _chefIndex;
		ServerMessenger.LobbyMessage(m_message);
	}

	protected void ChangeTeam(int _chefIndex)
	{
		if (m_bIsCoop || _chefIndex <= -1 || _chefIndex >= ServerUserSystem.m_Users.Count)
		{
			return;
		}
		User user = ServerUserSystem.m_Users._items[_chefIndex];
		if (user == null)
		{
			return;
		}
		int num = 0;
		int num2 = 0;
		for (int i = 0; i < ServerUserSystem.m_Users.Count; i++)
		{
			if (ServerUserSystem.m_Users._items[i].Team == TeamID.One)
			{
				num++;
			}
			else if (ServerUserSystem.m_Users._items[i].Team == TeamID.Two)
			{
				num2++;
			}
		}
		switch (user.Team)
		{
		case TeamID.None:
			if (num < Mathf.CeilToInt((float)ServerUserSystem.m_Users.Count / 2f))
			{
				user.Team = TeamID.One;
				user.Colour = m_lobbyFlow.m_redTeamColourIndex;
			}
			else
			{
				user.Team = TeamID.Two;
				user.Colour = m_lobbyFlow.m_blueTeamColourIndex;
			}
			break;
		case TeamID.One:
			if (num2 < Mathf.CeilToInt((float)ServerUserSystem.m_Users.Count / 2f))
			{
				user.Team = TeamID.Two;
				user.Colour = m_lobbyFlow.m_blueTeamColourIndex;
			}
			else
			{
				user.Team = TeamID.None;
				user.Colour = m_lobbyFlow.m_noTeamColourIndex;
			}
			break;
		case TeamID.Two:
			user.Team = TeamID.None;
			user.Colour = m_lobbyFlow.m_noTeamColourIndex;
			break;
		}
	}

	protected void OnUserAdded(User _user)
	{
		if (!_user.IsLocal)
		{
			FastList<User> fastList = ServerUserSystem.m_Users.FindAll((User x) => !x.IsLocal && x != _user);
			if (fastList.Count == 0)
			{
				ResetTimer(m_lobbyFlow.m_timeLimit);
			}
		}
		int num = ServerUserSystem.m_Users.FindIndex((User x) => x == _user);
		if (num != -1)
		{
			m_userChoices[num] = null;
		}
		m_lobbyFlow.RefreshUserColours(m_bIsCoop);
		InformAllOfCurrentSelections();
		SetState(m_state);
	}

	protected void OnUserRemoved(User _user, int _idx)
	{
		if (m_state == LobbyFlowController.LobbyState.OnlineThemeSelected)
		{
			bool flag = false;
			for (int i = 0; i < ServerUserSystem.m_Users.Count; i++)
			{
				if (ServerUserSystem.m_Users._items[i] != _user && !ServerUserSystem.m_Users._items[i].IsLocal)
				{
					flag = true;
				}
			}
			if (!flag)
			{
				if (m_delayedLevelLoad != null)
				{
					StopCoroutine(m_delayedLevelLoad);
					m_delayedLevelLoad = null;
				}
				if (m_bIsCoop)
				{
					ServerMessenger.LoadLevel("Lobbies", GameState.PartyLobby, false);
				}
				else
				{
					ServerMessenger.LoadLevel("Lobbies", GameState.VSLobby, false);
				}
				UnityEngine.Object.DestroyImmediate(base.gameObject);
				return;
			}
		}
		for (int j = _idx; j < m_userChoices.Length - 1; j++)
		{
			m_userChoices[j] = m_userChoices[j + 1];
		}
		for (int k = ServerUserSystem.m_Users.Count; k < m_userChoices.Length; k++)
		{
			m_userChoices[k] = new LobbyFlowController.ThemeChoice();
		}
		InformAllOfCurrentSelections();
		EnsureTeamsCanBeBalanced();
	}

	private void EnsureTeamsCanBeBalanced()
	{
		if (m_bIsCoop)
		{
			return;
		}
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		for (int i = 0; i < ServerUserSystem.m_Users.Count; i++)
		{
			switch (ServerUserSystem.m_Users._items[i].Team)
			{
			case TeamID.One:
				num++;
				break;
			case TeamID.Two:
				num2++;
				break;
			case TeamID.None:
			case TeamID.Count:
				num3++;
				break;
			}
		}
		if (num3 == 0 && (num == 0 || num2 == 0))
		{
			for (int j = 0; j < ServerUserSystem.m_Users.Count; j++)
			{
				User user = ServerUserSystem.m_Users._items[j];
				user.Team = TeamID.None;
				user.Colour = m_lobbyFlow.m_noTeamColourIndex;
			}
		}
	}

	protected void OnUsersChanged()
	{
		if (UserSystemUtils.AnyRemoteUsers())
		{
			SetState(m_state);
		}
		for (int i = 0; i < ServerUserSystem.m_Users.Count; i++)
		{
			m_lobbyFlow.RefreshUserColour(ServerUserSystem.m_Users._items[i], m_bIsCoop);
		}
	}

	protected void InformAllOfCurrentSelections()
	{
		for (int i = 0; i < m_userChoices.Length; i++)
		{
			SendAllUserThemeSelection((m_userChoices[i] == null) ? SceneDirectoryData.LevelTheme.Count : m_userChoices[i].m_theme, i);
		}
	}

	public void Update()
	{
		switch (m_state)
		{
		case LobbyFlowController.LobbyState.LocalThemeSelection:
			break;
		case LobbyFlowController.LobbyState.OnlineThemeSelection:
		{
			float timeLeft2 = m_timeLeft;
			if (m_lobbyInfo.m_visiblity == OnlineMultiplayerSessionVisibility.ePrivate && AllUsersSelected())
			{
				SetState(LobbyFlowController.LobbyState.OnlineThemeSelected);
			}
			else
			{
				if (ServerUserSystem.m_Users.Count <= 1 || !UserSystemUtils.AnyRemoteUsers())
				{
					break;
				}
				m_timeLeft -= TimeManager.GetDeltaTime(base.gameObject);
				if (m_timeLeft > 0f)
				{
					if (Mathf.FloorToInt(m_timeLeft) < Mathf.FloorToInt(timeLeft2))
					{
						m_message.m_type = LobbyServerMessage.LobbyMessageType.TimerUpdate;
						m_message.m_timerInfo = default(LobbyServerMessage.TimerInfo);
						m_message.m_timerInfo.m_timerVal = m_timeLeft;
						ServerMessenger.LobbyMessage(m_message);
					}
				}
				else
				{
					SetState(LobbyFlowController.LobbyState.OnlineThemeSelected);
				}
			}
			break;
		}
		case LobbyFlowController.LobbyState.LocalThemeSelected:
			break;
		case LobbyFlowController.LobbyState.OnlineThemeSelected:
		{
			float timeLeft = m_timeLeft;
			m_timeLeft -= TimeManager.GetDeltaTime(base.gameObject);
			if (m_timeLeft > 0f && Mathf.FloorToInt(m_timeLeft) < Mathf.FloorToInt(timeLeft))
			{
				m_message.m_type = LobbyServerMessage.LobbyMessageType.TimerUpdate;
				m_message.m_timerInfo = default(LobbyServerMessage.TimerInfo);
				m_message.m_timerInfo.m_timerVal = m_timeLeft;
				ServerMessenger.LobbyMessage(m_message);
			}
			break;
		}
		}
	}

	protected void StartLevel()
	{
		FillUndecidedUserChoices();
		SceneDirectoryData.LevelTheme levelTheme = PickTheme();
		List<int> list = new List<int>();
		for (int i = 0; i < m_userChoices.Length; i++)
		{
			if (m_userChoices[i] != null && m_userChoices[i].m_theme == levelTheme)
			{
				list.Add(i);
			}
		}
		int chefIndex = 0;
		if (!m_lobbyFlow.UnanimousSelection(m_userChoices) && UserSystemUtils.AnyRemoteUsers())
		{
			chefIndex = list[UnityEngine.Random.Range(0, list.Count)];
		}
		if (m_lobbyInfo.m_gameType == GameSession.GameType.Competitive)
		{
			AssignUsersToTeams();
		}
		m_message.m_type = LobbyServerMessage.LobbyMessageType.FinalSelection;
		m_message.m_selectionUpdate = default(LobbyServerMessage.SelectionUpdate);
		m_message.m_selectionUpdate.m_theme = levelTheme;
		m_message.m_selectionUpdate.m_chefIndex = chefIndex;
		ServerMessenger.LobbyMessage(m_message);
		PickLevel(levelTheme);
	}

	protected void AssignUsersToTeams()
	{
		int[] array = new int[ClientUserSystem.m_Users.Count];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = i;
		}
		array.ShuffleContents();
		foreach (int num in array)
		{
			if (ClientUserSystem.m_Users._items[num].Team == TeamID.None)
			{
				ChangeTeam(num);
			}
		}
	}

	protected SceneDirectoryData.LevelTheme PickTheme()
	{
		if (m_state == LobbyFlowController.LobbyState.LocalThemeSelected)
		{
			ThemeSelectButton buttonForTheme = m_lobbyFlow.m_themeSelectMenu.GetButtonForTheme(m_userChoices[0].m_theme);
			return buttonForTheme.Theme;
		}
		if (m_state == LobbyFlowController.LobbyState.OnlineThemeSelected)
		{
			LobbyFlowController.ThemeChoice[] items = m_userChoices.FindAll((LobbyFlowController.ThemeChoice x) => x != null && x.m_theme != SceneDirectoryData.LevelTheme.Count);
			LobbyFlowController.ThemeChoice randomElement = items.GetRandomElement();
			if (randomElement != null)
			{
				ThemeSelectButton buttonForTheme2 = m_lobbyFlow.m_themeSelectMenu.GetButtonForTheme(randomElement.m_theme);
				return buttonForTheme2.Theme;
			}
		}
		return SceneDirectoryData.LevelTheme.Random;
	}

	protected void PickLevel(SceneDirectoryData.LevelTheme _theme)
	{
		Predicate<SceneDirectoryData.SceneDirectoryEntry> match = delegate(SceneDirectoryData.SceneDirectoryEntry entry)
		{
			if (!entry.AvailableInLobby)
			{
				return false;
			}
			if (entry.Theme == SceneDirectoryData.LevelTheme.Null)
			{
				return false;
			}
			return entry.Theme != SceneDirectoryData.LevelTheme.Count && (entry.Theme == _theme || _theme == SceneDirectoryData.LevelTheme.Random);
		};
		FastList<SceneDirectoryData.SceneDirectoryEntry> fastList = new FastList<SceneDirectoryData.SceneDirectoryEntry>(60);
		SceneDirectoryData[] sceneDirectories = m_lobbyFlow.GetSceneDirectories();
		DLCManager dLCManager = GameUtils.RequireManager<DLCManager>();
		List<DLCFrontendData> allDlc = dLCManager.AllDlc;
		GameSession.GameType gameType = ((!m_bIsCoop) ? GameSession.GameType.Competitive : GameSession.GameType.Cooperative);
		int[] array = new int[sceneDirectories.Length];
		for (int num = 0; num < sceneDirectories.Length; num++)
		{
			DLCFrontendData dLCFrontendData = null;
			int dLCIDFromSceneDirIndex = m_lobbyFlow.GetDLCIDFromSceneDirIndex(gameType, num);
			if (_theme == SceneDirectoryData.LevelTheme.Random)
			{
				for (int num2 = 0; num2 < allDlc.Count; num2++)
				{
					DLCFrontendData dLCFrontendData2 = allDlc[num2];
					if (dLCFrontendData2.m_DLCID == dLCIDFromSceneDirIndex)
					{
						dLCFrontendData = dLCFrontendData2;
						break;
					}
				}
			}
			if (dLCFrontendData == null || dLCManager.IsDLCAvailable(dLCFrontendData))
			{
				fastList.AddRange(sceneDirectories[num].Scenes.FindAll(match));
			}
			array[num] = fastList.Count;
		}
		int num3 = UnityEngine.Random.Range(0, fastList.Count);
		int idx = -1;
		for (int num4 = 0; num4 < array.Length; num4++)
		{
			if (num3 < array[num4])
			{
				idx = num4;
				break;
			}
		}
		SceneDirectoryData.SceneDirectoryEntry sceneDirectoryEntry = fastList._items[num3];
		int dLCIDFromSceneDirIndex2 = m_lobbyFlow.GetDLCIDFromSceneDirIndex(gameType, idx);
		SceneDirectoryData.PerPlayerCountDirectoryEntry sceneVarient = sceneDirectoryEntry.GetSceneVarient(ServerUserSystem.m_Users.Count);
		if (sceneVarient == null)
		{
			if (m_bIsCoop)
			{
				return;
			}
			T17DialogBox dialog = T17DialogBoxManager.GetDialog(false);
			dialog.Initialize("Text.Versus.NotEnoughPlayers.Title", "Text.Versus.NotEnoughPlayers.Message", "Text.Button.Confirm", null, null);
			dialog.OnConfirm = (T17DialogBox.DialogEvent)Delegate.Combine(dialog.OnConfirm, (T17DialogBox.DialogEvent)delegate
			{
				ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Offline, null, delegate(IConnectionModeSwitchStatus _status)
				{
					if (_status.GetProgress() == eConnectionModeSwitchProgress.Complete)
					{
						ServerGameSetup.Mode = GameMode.OnlineKitchen;
						ServerMessenger.LoadLevel("StartScreen", GameState.MainMenu, true);
					}
				});
			});
			dialog.Show();
		}
		else
		{
			m_delayedLevelLoad = StartCoroutine(DelayedLevelLoad(sceneVarient.SceneName, dLCIDFromSceneDirIndex2));
		}
	}

	private IEnumerator DelayedLevelLoad(string _levelName, int _dlcID)
	{
		m_message.m_type = LobbyServerMessage.LobbyMessageType.CreateGameSession;
		m_message.m_dlcID = _dlcID;
		ServerMessenger.LobbyMessage(m_message);
		IEnumerator delay = CoroutineUtils.TimerRoutine(m_lobbyFlow.m_themeSelectionDuration + 1f, base.gameObject.layer);
		while (delay.MoveNext())
		{
			yield return null;
		}
		if (InviteMonitor.CheckStatus(InviteMonitor.StatusFlags.HandlerIsWaitingOnUserInput))
		{
			InviteMonitor.SwitchHandlerType(InviteMonitor.HandlerType.None);
		}
		else if (!InviteMonitor.CheckStatus(InviteMonitor.StatusFlags.HandlerIsValid | InviteMonitor.StatusFlags.HandlerIsIdle) || ServerGameSetup.Mode == GameMode.OnlineKitchen)
		{
			m_delayedLevelLoad = null;
			yield break;
		}
		GameSession gameSession = GameUtils.GetGameSession();
		if (gameSession != null)
		{
			PersistentObject persistentObject = gameSession.gameObject.RequestComponent<PersistentObject>();
			if (persistentObject != null)
			{
				persistentObject.AddPersistingLevel("Lobbies");
				persistentObject.AddPersistingLevel("Loading");
				persistentObject.AddPersistingLevel(_levelName);
			}
			gameSession.TypeSettings.WorldMapScene = "Lobbies";
		}
		PersistentObject lobbyPersist = m_lobbyInfo.gameObject.RequireComponent<PersistentObject>();
		lobbyPersist.AddPersistingLevel(_levelName);
		ServerMessenger.LoadLevel(_levelName, GameState.LoadKitchen, true, GameState.RunKitchen);
	}

	protected bool AllUsersSelected()
	{
		if (m_lobbyFlow.IsLocalState(m_state))
		{
			return m_bIsCoop || ServerUserSystem.m_Users.Count > 1;
		}
		if (UserSystemUtils.AnyRemoteUsers())
		{
			for (int i = 0; i < ServerUserSystem.m_Users.Count; i++)
			{
				if (m_userChoices[i] == null)
				{
					return false;
				}
				if (m_userChoices[i].m_theme == SceneDirectoryData.LevelTheme.Count)
				{
					return false;
				}
				if (m_userChoices[i].m_chefIndex < 0 || m_userChoices[i].m_chefIndex > m_userChoices.Length - 1)
				{
					return false;
				}
			}
			return true;
		}
		return false;
	}

	protected void OnLeave()
	{
		if (m_delayedLevelLoad != null)
		{
			StopCoroutine(m_delayedLevelLoad);
			m_delayedLevelLoad = null;
		}
		UnityEngine.Object.DestroyImmediate(base.gameObject);
	}

	private void OnDestroy()
	{
		if (s_instance == this)
		{
			s_instance = null;
		}
		Mailbox.Server.UnregisterForMessageType(MessageType.LobbyClient, OnLobbyClientMessage);
		ClientLobbyFlowController clientLobbyFlowController = base.gameObject.RequestComponent<ClientLobbyFlowController>();
		if (clientLobbyFlowController != null)
		{
			clientLobbyFlowController.OnLeave = (GenericVoid)Delegate.Remove(clientLobbyFlowController.OnLeave, new GenericVoid(OnLeave));
		}
		ServerUserSystem.OnUserAdded = (GenericVoid<User>)Delegate.Remove(ServerUserSystem.OnUserAdded, new GenericVoid<User>(OnUserAdded));
		ServerUserSystem.OnUserRemovedWithIndex = (GenericVoid<User, int>)Delegate.Remove(ServerUserSystem.OnUserRemovedWithIndex, new GenericVoid<User, int>(OnUserRemoved));
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Remove(ClientUserSystem.usersChanged, new GenericVoid(OnUsersChanged));
	}
}
