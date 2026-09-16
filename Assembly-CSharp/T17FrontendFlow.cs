using System;
using System.Collections;
using System.Collections.Generic;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class T17FrontendFlow : MonoBehaviour
{
	[Serializable]
	private class DLCSerializedGameSessions : DLCSerializedData<GameSession>
	{
	}

	public enum CameraState
	{
		ePushedForward = 0,
		ePulledBack = 1,
		eFocusedOnKitchen = 2,
		eUnFocusedOnKitchen = 3,
		eTransitioning = 4
	}

	private static T17FrontendFlow s_Instance;

	[Header("Game Sessions")]
	[SerializeField]
	private DLCSerializedGameSessions m_CoopGameSessionPrefabs = new DLCSerializedGameSessions();

	[SerializeField]
	private DLCSerializedGameSessions m_CompetitiveGameSessionPrefabs = new DLCSerializedGameSessions();

	[Header("Scene References")]
	[SerializeField]
	private Animator m_CameraAnimator;

	public FrontendRootMenu m_Rootmenu;

	public FrontendPlayerLobby m_PlayerLobby;

	public FrontendPlayerLobby m_PlayerLobbySwitch;

	[SerializeField]
	private GameObject m_FocusKitchenImage;

	[Space]
	[SerializeField]
	private SelectSaveDialog m_OnlineClientSaveDialog;

	[SerializeField]
	private FrontendMenuBehaviour m_saveWaitDialog;

	private const float c_ClientVisibleCountdownDuration = 60f;

	private const float c_LoadSaveSlotsAllowanceCountdownDuration = 5f;

	private const float c_LoadSaveGameAllowanceCountdownDuration = 5f;

	private float m_ServerCountdownDuration;

	private bool m_bServerCountdownRunning;

	private bool m_bRegisteredForGameStateMessage;

	private bool m_bShouldClientCountdown;

	private SelectSaveDialog m_saveDialog;

	private Suppressor m_ClientSaveWaitSuppressor;

	private IPlayerManager m_IPlayerManager;

	private T17EventSystem m_eventSystem;

	private GamepadEngagementManager m_gamepadEngagementManager;

	private ILogicalButton m_ToggleMultichefMenu;

	private ILogicalButton m_UnfocusMultichefMenu;

	private ILogicalButton m_quitButton;

	private bool m_allowMultichefMenu = true;

	private bool m_autoOpenChefSelectionMenu;

	private CameraState m_PostTransitionState = CameraState.ePulledBack;

	private CameraState m_CurrentCameraState;

	private CameraState m_PendingCameraState = CameraState.ePulledBack;

	private bool m_bBlockFocusKitchen;

	private GameStateMessage.ClientSavePayload m_ClientSavePayload;

	public static T17FrontendFlow Instance
	{
		get
		{
			return s_Instance;
		}
	}

	public float ServerCountdown { get; private set; }

	public float ClientCountdown { get; private set; }

	public bool ClientCountdownRunning
	{
		get
		{
			return m_bShouldClientCountdown;
		}
	}

	public bool allowMultichefMenu
	{
		set
		{
			m_allowMultichefMenu = value;
		}
	}

	public DLCFrontendData AutoOpenFrontendDlcData { get; private set; }

	public bool BlockFocusKitchen
	{
		get
		{
			return m_bBlockFocusKitchen;
		}
		set
		{
			m_bBlockFocusKitchen = value;
		}
	}

	public void AutoOpenChefSelectionMenu(DLCFrontendData data)
	{
		AutoOpenFrontendDlcData = data;
		if (data != null)
		{
			m_autoOpenChefSelectionMenu = true;
			m_CurrentCameraState = CameraState.eTransitioning;
		}
		else
		{
			m_autoOpenChefSelectionMenu = false;
		}
	}

	public bool IsCameraTransitioning()
	{
		return m_CurrentCameraState == CameraState.eTransitioning;
	}

	public void Awake()
	{
		if (s_Instance != null)
		{
			UnityEngine.Object.Destroy(this);
		}
		else
		{
			s_Instance = this;
		}
		if (m_Rootmenu != null)
		{
			m_Rootmenu.Hide();
			m_saveDialog = m_Rootmenu.SearchAllForMenuOfType<SelectSaveDialog>();
		}
		if (m_PlayerLobbySwitch != null)
		{
			UnityEngine.Object.Destroy(m_PlayerLobbySwitch.gameObject);
			m_PlayerLobbySwitch = null;
		}
		if (m_PlayerLobby != null)
		{
			m_PlayerLobby.SetupNetworking();
			m_PlayerLobby.Hide();
		}
		m_IPlayerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
		m_bServerCountdownRunning = false;
		m_ServerCountdownDuration = 71f;
		UserSystemUtils.OnServerChangedGameState = (GenericVoid<GameState, GameStateMessage.GameStatePayload>)Delegate.Combine(UserSystemUtils.OnServerChangedGameState, new GenericVoid<GameState, GameStateMessage.GameStatePayload>(OnServerGameStateChanged));
	}

	private void OnDestroy()
	{
		if (m_gamepadEngagementManager != null)
		{
			m_gamepadEngagementManager.CanManuallyChangeEngagement = false;
		}
		m_IPlayerManager.EngagementChangeCallback -= OnEngagementChangedReopenMenus;
		DisconnectionHandler.SessionConnectionLostEvent = (GenericVoid)Delegate.Remove(DisconnectionHandler.SessionConnectionLostEvent, new GenericVoid(OnSessionConnectionLost));
		DisconnectionHandler.ConnectionModeErrorEvent = (GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult>>)Delegate.Remove(DisconnectionHandler.ConnectionModeErrorEvent, new GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult>>(OnConnectionModeError));
		DisconnectionHandler.KickedFromSessionEvent = (GenericVoid)Delegate.Remove(DisconnectionHandler.KickedFromSessionEvent, new GenericVoid(OnKickedFromSession));
		DisconnectionHandler.LocalDisconnectionEvent = (GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult>>)Delegate.Remove(DisconnectionHandler.LocalDisconnectionEvent, new GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult>>(OnLocalDisconnection));
		InviteMonitor.InviteAccepted = (GenericVoid)Delegate.Remove(InviteMonitor.InviteAccepted, new GenericVoid(OnInviteAccepted));
		InviteMonitor.InviteJoinComplete = (GenericVoid)Delegate.Remove(InviteMonitor.InviteJoinComplete, new GenericVoid(OnInviteJoinComplete));
		Mailbox.Client.UnregisterForMessageType(MessageType.GameState, OnGameStateChanged);
		m_OnlineClientSaveDialog.Hide();
		if (s_Instance != null)
		{
			UnityEngine.Object.Destroy(s_Instance);
			s_Instance = null;
		}
		UserSystemUtils.OnServerChangedGameState = (GenericVoid<GameState, GameStateMessage.GameStatePayload>)Delegate.Remove(UserSystemUtils.OnServerChangedGameState, new GenericVoid<GameState, GameStateMessage.GameStatePayload>(OnServerGameStateChanged));
	}

	private void Start()
	{
		if (!m_IPlayerManager.HasPlayer())
		{
		}
		m_eventSystem = GetPrimaryEventSystem();
		GamepadUser user = m_IPlayerManager.GetUser(EngagementSlot.One);
		if (user == null)
		{
			m_IPlayerManager.EngagementChangeCallback += OnEngagementChangedReopenMenus;
		}
		m_ToggleMultichefMenu = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UIMultiChefMenu);
		m_UnfocusMultichefMenu = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UICancel);
		if (m_PlayerLobby != null)
		{
			m_PlayerLobby.Show(user, null, null);
		}
		if (m_Rootmenu != null)
		{
			m_Rootmenu.Show(user, null, null);
		}
		m_allowMultichefMenu = true;
		m_gamepadEngagementManager = GameUtils.RequireManager<GamepadEngagementManager>();
		if (m_gamepadEngagementManager != null)
		{
			m_gamepadEngagementManager.CanManuallyChangeEngagement = true;
		}
		Mailbox.Client.RegisterForMessageType(MessageType.GameState, OnGameStateChanged);
		DisconnectionHandler.SessionConnectionLostEvent = (GenericVoid)Delegate.Combine(DisconnectionHandler.SessionConnectionLostEvent, new GenericVoid(OnSessionConnectionLost));
		DisconnectionHandler.ConnectionModeErrorEvent = (GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult>>)Delegate.Combine(DisconnectionHandler.ConnectionModeErrorEvent, new GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult>>(OnConnectionModeError));
		DisconnectionHandler.KickedFromSessionEvent = (GenericVoid)Delegate.Combine(DisconnectionHandler.KickedFromSessionEvent, new GenericVoid(OnKickedFromSession));
		DisconnectionHandler.LocalDisconnectionEvent = (GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult>>)Delegate.Combine(DisconnectionHandler.LocalDisconnectionEvent, new GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult>>(OnLocalDisconnection));
		InviteMonitor.InviteAccepted = (GenericVoid)Delegate.Combine(InviteMonitor.InviteAccepted, new GenericVoid(OnInviteAccepted));
		InviteMonitor.InviteJoinComplete = (GenericVoid)Delegate.Combine(InviteMonitor.InviteJoinComplete, new GenericVoid(OnInviteJoinComplete));
	}

	private void OnSessionConnectionLost()
	{
		HandleDisconnection();
	}

	private void OnConnectionModeError(OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult> _error)
	{
		HandleDisconnection();
	}

	private void OnKickedFromSession()
	{
		HandleDisconnection();
	}

	private void OnLocalDisconnection(OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult> _error)
	{
		HandleDisconnection();
	}

	public void OnSessionLeft()
	{
		HandleDisconnection();
	}

	public void HandleDisconnection()
	{
		HideWaitingForPlayers();
		if (m_saveDialog.gameObject.activeInHierarchy)
		{
			m_saveDialog.HandleDisconnection(OnSaveDialogDisconnectionHandled);
		}
		else if (m_OnlineClientSaveDialog.gameObject.activeInHierarchy)
		{
			m_OnlineClientSaveDialog.HandleDisconnection(OnSaveDialogDisconnectionHandled);
		}
	}

	private void OnSaveDialogDisconnectionHandled()
	{
		if (m_Rootmenu != null)
		{
			m_Rootmenu.HideMenuStack();
			m_Rootmenu.ExpandCurrentTab();
		}
		if (m_bServerCountdownRunning)
		{
			StopServerCountdown();
		}
	}

	public void ShowClientSaveDialog()
	{
		if (m_ClientSavePayload == null || ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession())
		{
			return;
		}
		BaseMenuBehaviour currentOpenMenu = m_Rootmenu.GetCurrentOpenMenu();
		if (currentOpenMenu is FrontendSettingsTabOptions)
		{
			FrontendOptionsMenu frontendOptionsMenu = m_Rootmenu.SearchAllForMenuOfType<FrontendOptionsMenu>();
			if (null != frontendOptionsMenu)
			{
				frontendOptionsMenu.ResetAndClose();
			}
			FrontendControllerOptionsMenu frontendControllerOptionsMenu = m_Rootmenu.SearchAllForMenuOfType<FrontendControllerOptionsMenu>();
			if (null != frontendControllerOptionsMenu)
			{
				frontendControllerOptionsMenu.CancelAndCloseAllDialogs();
			}
		}
		m_OnlineClientSaveDialog.Mode = SaveDialogMode.LoadGame;
		m_OnlineClientSaveDialog.DLC = m_ClientSavePayload.DLCID;
		m_Rootmenu.HideMenuStack();
		m_Rootmenu.OpenFrontendMenu(m_OnlineClientSaveDialog);
		ClientCountdown = 60f;
		m_bShouldClientCountdown = false;
		m_ClientSavePayload = null;
	}

	private bool CanShowClientSaveDialog()
	{
		return ConnectionStatus.IsInSession() && !ConnectionStatus.IsHost() && m_ClientSavePayload != null && ConnectionModeSwitcher.GetStatus().GetProgress() == eConnectionModeSwitchProgress.Complete && ConnectionModeSwitcher.GetStatus().GetResult() == eConnectionModeSwitchResult.Success && !m_PlayerLobby.IsKitchenConnectionTaskRunning();
	}

	private void OnGameStateChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		GameStateMessage gameStateMessage = (GameStateMessage)message;
		if (gameStateMessage.m_State == GameState.SelectCampaignMapSave)
		{
			if (ConnectionStatus.IsInSession() && !ConnectionStatus.IsHost())
			{
				m_ClientSavePayload = (GameStateMessage.ClientSavePayload)gameStateMessage.Payload;
			}
			if (CanShowClientSaveDialog())
			{
				ShowClientSaveDialog();
			}
		}
		else if (gameStateMessage.m_State == GameState.CampaignMap)
		{
			m_OnlineClientSaveDialog.Hide();
		}
	}

	private void OnInviteAccepted()
	{
		if (!ConnectionStatus.IsInSession() || ConnectionStatus.IsHost())
		{
			StopServerCountdown();
		}
		m_bShouldClientCountdown = false;
	}

	private void OnInviteJoinComplete()
	{
		if (m_Rootmenu != null)
		{
			m_Rootmenu.ExpandCurrentTab();
		}
	}

	private void Update()
	{
		if (T17DialogBoxManager.HasAnyOpenDialogs())
		{
			if (m_ToggleMultichefMenu != null)
			{
				m_ToggleMultichefMenu.ClaimPressEvent();
				m_ToggleMultichefMenu.ClaimReleaseEvent();
			}
			if (m_UnfocusMultichefMenu != null)
			{
				m_UnfocusMultichefMenu.ClaimPressEvent();
				m_UnfocusMultichefMenu.ClaimReleaseEvent();
			}
			if (m_quitButton != null)
			{
				m_quitButton.ClaimPressEvent();
				m_quitButton.ClaimReleaseEvent();
			}
		}
		if (m_eventSystem == null)
		{
			m_eventSystem = GetPrimaryEventSystem();
		}
		bool flag = false;
		if (!m_bBlockFocusKitchen && m_CurrentCameraState != CameraState.eTransitioning && m_eventSystem != null && !m_eventSystem.IsDisabled())
		{
			if (m_ToggleMultichefMenu != null && m_ToggleMultichefMenu.JustPressed())
			{
				flag = true;
			}
			GameObject lastRequestedSelectedGameobject = m_eventSystem.GetLastRequestedSelectedGameobject();
			if (lastRequestedSelectedGameobject != null)
			{
				bool isFocused = m_PlayerLobby.IsFocused;
				if (isFocused && m_Rootmenu.IsChildMenuOpen())
				{
					flag = true;
				}
				else if (!isFocused && lastRequestedSelectedGameobject.IsInHierarchyOf(m_PlayerLobby.gameObject))
				{
					flag = true;
				}
			}
		}
		if (flag)
		{
			if (!m_PlayerLobby.IsFocused)
			{
				if (m_allowMultichefMenu)
				{
					FocusOnMultiplayerKitchen();
				}
			}
			else
			{
				FocusOnMainMenu();
			}
		}
		if (m_CurrentCameraState == CameraState.eFocusedOnKitchen && !m_PlayerLobby.IsFocused)
		{
			UnFocusKitchenLobby();
		}
		else if (m_CurrentCameraState != CameraState.eFocusedOnKitchen && m_PlayerLobby.IsFocused)
		{
			FocusOnKitchenLobby();
		}
		if (!m_bBlockFocusKitchen && m_UnfocusMultichefMenu != null && m_UnfocusMultichefMenu.JustPressed() && m_PlayerLobby.IsFocused && !m_PlayerLobby.IsPlayerSlotMenuOpen)
		{
			FocusOnMainMenu();
		}
		if (m_bServerCountdownRunning && ConnectionStatus.IsHost() && UserSystemUtils.AreAnyUsersInGameState(ServerUserSystem.m_Users, GameState.SelectCampaignMapSave))
		{
			ServerCountdown -= Time.deltaTime;
			ClientCountdown -= Time.deltaTime;
			if (ServerCountdown <= 0f)
			{
				ServerLoadCampaign();
			}
		}
		if (ConnectionStatus.IsInSession() && !ConnectionStatus.IsHost())
		{
			if (CanShowClientSaveDialog())
			{
				ShowClientSaveDialog();
			}
			if (m_bShouldClientCountdown)
			{
				ClientCountdown -= Time.deltaTime;
			}
		}
		if (!m_saveWaitDialog.gameObject.activeInHierarchy)
		{
			return;
		}
		if (T17DialogBoxManager.HasAnyOpenDialogs())
		{
			if (m_ClientSaveWaitSuppressor != null)
			{
				m_ClientSaveWaitSuppressor.Release();
				m_ClientSaveWaitSuppressor = null;
			}
		}
		else if (m_ClientSaveWaitSuppressor == null)
		{
			m_ClientSaveWaitSuppressor = m_eventSystem.Disable(m_saveWaitDialog);
		}
	}

	private void LateUpdate()
	{
		if (m_CurrentCameraState == CameraState.eTransitioning)
		{
			if (m_CameraAnimator.GetBool("Transitioning"))
			{
				return;
			}
			m_CurrentCameraState = m_PostTransitionState;
			if (m_PostTransitionState == CameraState.ePulledBack && m_autoOpenChefSelectionMenu)
			{
				m_autoOpenChefSelectionMenu = false;
				FrontendMenuBehaviour frontendMenuBehaviour = m_Rootmenu.SearchAllForMenuOfType<FrontendChefMenu>();
				if (frontendMenuBehaviour != null)
				{
					m_Rootmenu.OpenFrontendMenu(frontendMenuBehaviour);
				}
			}
		}
		else if (m_PendingCameraState != m_CurrentCameraState)
		{
			switch (m_PendingCameraState)
			{
			case CameraState.ePulledBack:
				DoPullBackCamera();
				break;
			case CameraState.eFocusedOnKitchen:
				DoFocusOnKitchenLobby();
				break;
			case CameraState.ePushedForward:
				DoPushForwardCamera();
				break;
			case CameraState.eUnFocusedOnKitchen:
				DoUnFocusKitchenLobby();
				break;
			}
		}
	}

	public void FocusOnMultiplayerKitchen(bool bForce = false)
	{
		if (bForce || (!m_PlayerLobby.IsFocused && m_allowMultichefMenu))
		{
			m_Rootmenu.CollapseCurrentTab();
			m_PlayerLobby.FocusMenu();
			FocusOnKitchenLobby();
			m_Rootmenu.SetLegendText(m_PlayerLobby.GetLegendText());
			if (m_FocusKitchenImage != null)
			{
				m_FocusKitchenImage.SetActive(false);
			}
		}
	}

	public void FocusOnMainMenu()
	{
		if (m_PlayerLobby.IsFocused)
		{
			m_PlayerLobby.UnFocusMenu();
			m_Rootmenu.ExpandCurrentTab();
			UnFocusKitchenLobby();
			if (m_FocusKitchenImage != null)
			{
				m_FocusKitchenImage.SetActive(true);
			}
		}
	}

	public GameSession StartEmptySession(GameSession.GameType gameType, int dlcNum)
	{
		int saveSlot = 0;
		GameSession gameSession = GameUtils.GetGameSession();
		if (gameSession != null)
		{
			saveSlot = gameSession.SaveSlot;
			UnityEngine.Object.DestroyImmediate(gameSession.gameObject);
		}
		GameSession[] array = null;
		switch (gameType)
		{
		case GameSession.GameType.Cooperative:
			array = m_CoopGameSessionPrefabs.AllData;
			break;
		case GameSession.GameType.Competitive:
			array = m_CompetitiveGameSessionPrefabs.AllData;
			break;
		}
		array = array.AllRemoved_Predicate((GameSession x) => x == null);
		GameSession gameSession2 = Array.Find(array, (GameSession x) => x.DLC == dlcNum);
		if (gameSession2 != null)
		{
			GameObject obj = gameSession2.gameObject.InstantiateOnParent(null);
			GameSession gameSession3 = obj.RequireComponent<GameSession>();
			gameSession3.TypeSettings.Type = gameType;
			gameSession3.SaveSlot = saveSlot;
			return gameSession3;
		}
		return null;
	}

	public void CheckSessionForSaveFile(GameSession session, GenericVoid<GameSession, SaveLoadResult> onComplete)
	{
		if (session != null)
		{
			StartCoroutine(CheckForSaveFile(session, onComplete));
		}
	}

	private IEnumerator CheckForSaveFile(GameSession session, GenericVoid<GameSession, SaveLoadResult> onComplete)
	{
		ReturnValue<SaveLoadResult> result = new ReturnValue<SaveLoadResult>();
		IEnumerator hasSaveRoutine = session.HasSaveFile(result);
		while (hasSaveRoutine.MoveNext())
		{
			yield return null;
		}
		if (onComplete != null)
		{
			onComplete(session, result.Value);
		}
	}

	public void PullBackCamera()
	{
		m_PendingCameraState = CameraState.ePulledBack;
	}

	private void DoPullBackCamera()
	{
		m_CameraAnimator.SetTrigger("Pull Back");
		m_CameraAnimator.ResetTrigger("Push Forward");
		m_CameraAnimator.ResetTrigger("Focus Kitchen");
		m_CameraAnimator.ResetTrigger("UnFocus Kitchen");
		m_CurrentCameraState = CameraState.eTransitioning;
		m_PostTransitionState = CameraState.ePulledBack;
		m_CameraAnimator.Update(0f);
	}

	public void PushForwardCamera()
	{
		m_PendingCameraState = CameraState.ePushedForward;
	}

	private void DoPushForwardCamera()
	{
		m_CameraAnimator.SetTrigger("Push Forward");
		m_CameraAnimator.ResetTrigger("Pull Back");
		m_CameraAnimator.ResetTrigger("Focus Kitchen");
		m_CameraAnimator.ResetTrigger("UnFocus Kitchen");
		m_CurrentCameraState = CameraState.eTransitioning;
		m_PostTransitionState = CameraState.ePushedForward;
		m_CameraAnimator.Update(0f);
	}

	public void FocusOnKitchenLobby()
	{
		m_PendingCameraState = CameraState.eFocusedOnKitchen;
	}

	private void DoFocusOnKitchenLobby()
	{
		m_CameraAnimator.SetTrigger("Focus Kitchen");
		m_CameraAnimator.ResetTrigger("Push Forward");
		m_CameraAnimator.ResetTrigger("Pull Back");
		m_CameraAnimator.ResetTrigger("UnFocus Kitchen");
		m_CurrentCameraState = CameraState.eTransitioning;
		m_PostTransitionState = CameraState.eFocusedOnKitchen;
		m_CameraAnimator.Update(0f);
	}

	public void UnFocusKitchenLobby()
	{
		m_PendingCameraState = CameraState.eUnFocusedOnKitchen;
	}

	private void DoUnFocusKitchenLobby()
	{
		m_CameraAnimator.SetTrigger("UnFocus Kitchen");
		m_CameraAnimator.ResetTrigger("Push Forward");
		m_CameraAnimator.ResetTrigger("Pull Back");
		m_CameraAnimator.ResetTrigger("Focus Kitchen");
		m_CurrentCameraState = CameraState.eTransitioning;
		m_PostTransitionState = CameraState.ePulledBack;
		m_CameraAnimator.Update(0f);
		m_PendingCameraState = CameraState.ePulledBack;
	}

	public void ResetCamera()
	{
		m_PostTransitionState = CameraState.ePushedForward;
		m_CurrentCameraState = CameraState.ePushedForward;
		m_CameraAnimator.SetTrigger("Reset");
		m_CameraAnimator.ResetTrigger("UnFocus Kitchen");
		m_CameraAnimator.ResetTrigger("Push Forward");
		m_CameraAnimator.ResetTrigger("Pull Back");
		m_CameraAnimator.ResetTrigger("Focus Kitchen");
	}

	protected T17EventSystem GetPrimaryEventSystem()
	{
		GamepadUser user = m_IPlayerManager.GetUser(EngagementSlot.One);
		T17EventSystem t17EventSystem = null;
		if (user != null)
		{
			t17EventSystem = T17EventSystemsManager.Instance.GetEventSystemForGamepadUser(user);
			if (t17EventSystem == null)
			{
				T17EventSystemsManager.Instance.AssignFreeEventSystemToGamepadUser(user);
				t17EventSystem = T17EventSystemsManager.Instance.GetEventSystemForGamepadUser(user);
			}
		}
		return t17EventSystem;
	}

	private void OnEngagementChangedReopenMenus(EngagementSlot _slot, GamepadUser _prevUser, GamepadUser _newUser)
	{
		if (_slot == EngagementSlot.One && _prevUser == null && _newUser != null)
		{
			if (m_PlayerLobby != null)
			{
				m_PlayerLobby.Hide();
				m_PlayerLobby.Show(_newUser, null, null);
			}
			if (m_Rootmenu != null)
			{
				m_Rootmenu.Hide();
				m_Rootmenu.Show(_newUser, null, null);
			}
			if (ClientCountdownRunning)
			{
				m_Rootmenu.OpenFrontendMenu(m_OnlineClientSaveDialog);
			}
			m_IPlayerManager.EngagementChangeCallback -= OnEngagementChangedReopenMenus;
		}
	}

	private void OnServerGameStateChanged(GameState state, GameStateMessage.GameStatePayload payload)
	{
		if (state == GameState.SelectCampaignMapSave)
		{
			StartServerCountdown();
		}
	}

	protected void OnServerReceivedClientGameState(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		GameStateMessage gameStateMessage = message as GameStateMessage;
		if (gameStateMessage.m_State == GameState.LoadedCampaignMapSave)
		{
			ServerTryLoadCampaign();
		}
	}

	private void ServerTryLoadCampaign()
	{
		if (m_bServerCountdownRunning)
		{
			FastList<User> users = ServerUserSystem.m_Users;
			User.MachineID s_LocalMachineId = ServerUserSystem.s_LocalMachineId;
			User user = UserSystemUtils.FindUser(users, null, s_LocalMachineId, EngagementSlot.One);
			if (user != null && (!ConnectionStatus.IsInSession() || (ConnectionStatus.IsHost() && UserSystemUtils.AreAllUsersInGameState(ServerUserSystem.m_Users, GameState.LoadedCampaignMapSave))))
			{
				ServerLoadCampaign();
			}
		}
	}

	private void ServerLoadCampaign()
	{
		StopServerCountdown();
		m_saveDialog.LoadReadySlot();
	}

	private void OnUserRemoved(User removed)
	{
		ServerTryLoadCampaign();
	}

	private void StartServerCountdown()
	{
		if (!m_bRegisteredForGameStateMessage)
		{
			ServerCountdown = m_ServerCountdownDuration;
			ClientCountdown = 60f;
			ServerUserSystem.OnUserRemoved = (GenericVoid<User>)Delegate.Combine(ServerUserSystem.OnUserRemoved, new GenericVoid<User>(OnUserRemoved));
			Mailbox.Server.RegisterForMessageType(MessageType.GameState, OnServerReceivedClientGameState);
			m_bRegisteredForGameStateMessage = true;
			m_bServerCountdownRunning = true;
		}
	}

	private void StopServerCountdown()
	{
		if (m_bRegisteredForGameStateMessage)
		{
			ServerUserSystem.OnUserRemoved = (GenericVoid<User>)Delegate.Remove(ServerUserSystem.OnUserRemoved, new GenericVoid<User>(OnUserRemoved));
			Mailbox.Server.UnregisterForMessageType(MessageType.GameState, OnServerReceivedClientGameState);
			m_bRegisteredForGameStateMessage = false;
			m_bServerCountdownRunning = false;
		}
	}

	public void StartClientCountdown()
	{
		m_bShouldClientCountdown = true;
	}

	public void ShowWaitingForPlayers()
	{
		m_saveDialog.Hide();
		m_Rootmenu.OpenFrontendMenu(m_saveWaitDialog);
		if (m_ClientSaveWaitSuppressor == null)
		{
			m_ClientSaveWaitSuppressor = m_eventSystem.Disable(m_saveWaitDialog);
		}
	}

	public void HideWaitingForPlayers()
	{
		if (m_bServerCountdownRunning)
		{
			StopServerCountdown();
		}
		m_saveWaitDialog.Hide();
		if (m_ClientSaveWaitSuppressor != null)
		{
			m_eventSystem.ReleaseSuppressor(m_ClientSaveWaitSuppressor);
			m_ClientSaveWaitSuppressor = null;
		}
	}

	public void PromptGameExit()
	{
		if (Instance.IsCameraTransitioning() || (m_PlayerLobby != null && m_PlayerLobby.IsFocused))
		{
			if (!m_bBlockFocusKitchen && m_PlayerLobby != null && m_PlayerLobby.IsFocused && !m_PlayerLobby.m_slotMenuClosedThisFrame)
			{
				FocusOnMainMenu();
			}
			return;
		}
		T17DialogBox dialog = T17DialogBoxManager.GetDialog(false);
		if (dialog != null)
		{
			dialog.Initialize("Text.Menu.QuitTitle", "Text.Menu.QuitBody", "Text.Button.Quit", "Text.Button.Cancel", string.Empty);
			dialog.OnConfirm = (T17DialogBox.DialogEvent)Delegate.Combine(dialog.OnConfirm, new T17DialogBox.DialogEvent(DoExit));
			dialog.Show();
		}
	}

	private void DoExit()
	{
		Application.Quit();
	}
}
