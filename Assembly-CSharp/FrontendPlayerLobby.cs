#define ANALYTICS
using System;
using System.Collections.Generic;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class FrontendPlayerLobby : FrontendMenuBehaviour
{
	private enum DisplayProgressInfo
	{
		None = 0,
		ConnectionModeSwitch = 1,
		DropInPlayer = 2
	}

	public List<FrontendPlayerSlot> m_PlayerSlots = new List<FrontendPlayerSlot>();

	public FrontendChef[] m_PlayerChefs;

	[SerializeField]
	public ParticleSystem m_playerJoinPFXPrefab;

	[SerializeField]
	public GameOneShotAudioTag m_playerJoinAudioTag = GameOneShotAudioTag.COUNT;

	public GameObject m_OptionsParent;

	public T17Text m_chalkboardInactivePrompt;

	public GameObject m_chalkboardGuestPrompt;

	public GameObject m_LeaveKitchenButton;

	public GameObject m_ControllerOptions;

	public T17Button m_ControllerLeftSideButton;

	public T17Image m_ControllerLeftSideImage;

	public T17Button m_ControllerFullButton;

	public T17Image m_ControllerFullImage;

	public T17Button m_ControllerRightSideButton;

	public T17Image m_ControllerRightSideImage;

	public GameObject m_InviteButton;

	public GameObject m_SplitPadButton;

	public GameObject m_GamertagButton;

	public GameObject m_KickButton;

	public GameObject m_RemoveButton;

	public GameObject m_ChangeProfileButton;

	public GameObject m_HostingIndicator;

	[SerializeField]
	private GameObject m_ModeButtonGroup;

	[SerializeField]
	private T17Button m_ModeButton;

	[SerializeField]
	private GameObject m_FriendsButton;

	[SerializeField]
	private GameObject m_SearchButton;

	[SerializeField]
	private GameObject m_LeftModeArrow;

	[SerializeField]
	private GameObject m_RightModeArrow;

	[SerializeField]
	private T17Text m_ModeText;

	[SerializeField]
	private FrontendSwitchSearch m_switchSearchMenu;

	[SerializeField]
	private FrontendSwitchFriends m_switchFriendsMenu;

	[SerializeField]
	private FrontendPlayWithFriendsMenu m_switchPlayWithFriendsMenu;

	[SerializeField]
	private GameObject m_mouseBlock;

	private static readonly string s_ModeTooltip = "Text.Menu.ModeTooltip";

	private static readonly string s_OnlineModeTooltip = "Text.Menu.ModeOnlineTooltip";

	private static readonly string s_OfflineModeTooltip = "Text.Menu.ModeOfflineTooltip";

	private static readonly string s_WirelessModeTooltip = "Text.Menu.ModeWirelessTooltip";

	private static readonly string s_offlineTooltip = "Text.Menu.OfflineTooltip";

	private bool m_isSelectingMode;

	private ILogicalButton m_cycleModeLeftButton;

	private ILogicalButton m_cycleModeRightButton;

	private OnlineMultiplayerConnectionMode m_currentOnlineMode;

	private OnlineMultiplayerConnectionMode m_selectedOnlineMode;

	[SerializeField]
	private string m_hostTooltip = "Text.Menu.DefaultTooltip";

	[SerializeField]
	private string m_clientTooltip = "Text.Menu.DefaultTooltipClient";

	[SerializeField]
	private GameObject m_InviteFocusItem;

	[SerializeField]
	private FrontendChefMenu m_chefMenu;

	private FrontendPlayerSlot m_CurrentExpandedSlot;

	private bool m_currentSlotHasUser;

	private T17Text m_GamertagButtonText;

	private IPlayerManager m_IPlayerManager;

	private T17DialogBox m_progressBox;

	private DisplayProgressInfo m_displayProgressInformation = DisplayProgressInfo.ConnectionModeSwitch;

	[AssignResource("Frontend_ControllerTypeSprites", Editorbility.NonEditable)]
	public ControllerTypeSprites m_ControllerSprites;

	private KitchenSendInviteTask m_sendInviteTask;

	private KitchenSwitchConnectionModeTask m_switchConnectionModeTask;

	private NetworkErrorDialog m_NetworkErrorDialog;

	private bool m_bIsHeadChef = true;

	[SerializeField]
	private FrontendRootMenu m_rootMenu;

	[SerializeField]
	private string m_HeadChefPrompt = "Text.Menu.AddLocalPlayer";

	private int m_lastPlayerCountForPrompt = -1;

	private FrontendChef.AnimationSet[] m_animSets = new FrontendChef.AnimationSet[4]
	{
		FrontendChef.AnimationSet.One,
		FrontendChef.AnimationSet.Two,
		FrontendChef.AnimationSet.Three,
		FrontendChef.AnimationSet.Four
	};

	public HatMeshVisibility.VisState m_ChefHat = HatMeshVisibility.VisState.Fancy;

	private bool m_bIsFocussed;

	private bool m_isPlayerSlotMenuOpen;

	public bool m_slotMenuClosedThisFrame;

	private T17DialogBox m_leaveSessionDialogBox;

	public bool IsFocused
	{
		get
		{
			return m_bIsFocussed;
		}
	}

	public bool IsPlayerSlotMenuOpen
	{
		get
		{
			return m_isPlayerSlotMenuOpen;
		}
	}

	public bool IsKitchenConnectionTaskRunning()
	{
		return (m_switchConnectionModeTask != null && m_switchConnectionModeTask.isRunning) || (null != m_switchPlayWithFriendsMenu && m_switchPlayWithFriendsMenu.IsBusy());
	}

	public void SetupNetworking()
	{
		if (m_NetworkErrorDialog == null)
		{
			m_NetworkErrorDialog = new NetworkErrorDialog();
			m_NetworkErrorDialog.Enable(OnNetworkErrorDismissed);
			InviteMonitor.InviteJoinComplete = (GenericVoid)Delegate.Combine(InviteMonitor.InviteJoinComplete, new GenericVoid(OnInviteJoinComplete));
		}
	}

	protected override void SingleTimeInitialize()
	{
		base.SingleTimeInitialize();
	}

	protected override void Start()
	{
		base.Start();
		m_IPlayerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
		if (m_PlayerSlots == null || m_PlayerSlots.Count == 0)
		{
			m_PlayerSlots = new List<FrontendPlayerSlot>(GetComponentsInChildren<FrontendPlayerSlot>(true));
		}
		for (int i = 0; i < m_PlayerSlots.Count; i++)
		{
			if (!(m_PlayerSlots[i] != null) || !(m_PlayerSlots[i].m_SlotButton != null))
			{
				continue;
			}
			int index = i;
			FrontendPlayerSlot slot = m_PlayerSlots[i];
			slot.m_SlotButton.onClick.AddListener(delegate
			{
				OpenOptionsMenuForPlayerslot(index);
			});
			T17Button slotButton = slot.m_SlotButton;
			slotButton.OnButtonSelect = (T17Button.T17ButtonDelegate)Delegate.Combine(slotButton.OnButtonSelect, (T17Button.T17ButtonDelegate)delegate
			{
				CloseOptionsMenu();
			});
			T17Button slotButton2 = slot.m_SlotButton;
			slotButton2.OnButtonMove = (T17Button.T17ButtonMoveDelegate)Delegate.Combine(slotButton2.OnButtonMove, (T17Button.T17ButtonMoveDelegate)delegate(Selectable _from, Selectable _to, MoveDirection _direction)
			{
				if (_to != null && _to.gameObject.IsInHierarchyOf(m_OptionsParent))
				{
					m_CurrentExpandedSlot = slot;
					UpdateButtonNavigation();
				}
			});
		}
		if (m_GamertagButton != null)
		{
			m_GamertagButtonText = m_GamertagButton.GetComponentInChildren<T17Text>();
		}
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Combine(ClientUserSystem.usersChanged, new GenericVoid(OnUsersChangedEvent));
		ClientUserSystem.userAdded = (GenericVoid<uint, UserData>)Delegate.Combine(ClientUserSystem.userAdded, new GenericVoid<uint, UserData>(OnUserAdded));
		m_IPlayerManager.EngagementChangeCallback += OnEngagementChanged;
		m_sendInviteTask = new KitchenSendInviteTask();
		m_sendInviteTask.onComplete += OnSendInviteTaskComplete;
		m_switchConnectionModeTask = new KitchenSwitchConnectionModeTask();
		m_switchConnectionModeTask.onComplete += OnSwitchConnectionModeComplete;
		KitchenSwitchConnectionModeTask switchConnectionModeTask = m_switchConnectionModeTask;
		switchConnectionModeTask.onResults = (KitchenSwitchConnectionModeTask.OnResults)Delegate.Combine(switchConnectionModeTask.onResults, new KitchenSwitchConnectionModeTask.OnResults(OnResults));
		ServerUserSystem.OnEngagementPrivilegeCheckStarted = (GenericVoid<IConnectionModeSwitchStatus>)Delegate.Combine(ServerUserSystem.OnEngagementPrivilegeCheckStarted, new GenericVoid<IConnectionModeSwitchStatus>(EngagementPrivilegeCheckStarted));
		ServerUserSystem.OnEngagementPrivilegeCheckCompleted = (GenericVoid<IConnectionModeSwitchStatus>)Delegate.Combine(ServerUserSystem.OnEngagementPrivilegeCheckCompleted, new GenericVoid<IConnectionModeSwitchStatus>(EngagementPrivilegeCheckComplete));
		ShowIdleMenu();
		if (ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession())
		{
			for (int num = 0; num < ServerUserSystem.m_Users.Count; num++)
			{
				User user = ServerUserSystem.m_Users._items[num];
				user.Colour = (uint)num;
			}
		}
		m_animSets.ShuffleContents();
		NetworkUtils.SelectRandomAvatar();
		RecalculateChefAvatars(true);
		UpdatePromptPlayerCount();
		SetMouseBlockActive(false);
		DisconnectionHandler.SessionConnectionLostEvent = (GenericVoid)Delegate.Combine(DisconnectionHandler.SessionConnectionLostEvent, new GenericVoid(OnSessionConnectionLost));
		DisconnectionHandler.ConnectionModeErrorEvent = (GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult>>)Delegate.Combine(DisconnectionHandler.ConnectionModeErrorEvent, new GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult>>(OnConnectionModeError));
		DisconnectionHandler.KickedFromSessionEvent = (GenericVoid)Delegate.Combine(DisconnectionHandler.KickedFromSessionEvent, new GenericVoid(OnKickedFromSession));
		DisconnectionHandler.LocalDisconnectionEvent = (GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult>>)Delegate.Combine(DisconnectionHandler.LocalDisconnectionEvent, new GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult>>(OnLocalDisconnection));
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Remove(ClientUserSystem.usersChanged, new GenericVoid(OnUsersChangedEvent));
		ClientUserSystem.userAdded = (GenericVoid<uint, UserData>)Delegate.Remove(ClientUserSystem.userAdded, new GenericVoid<uint, UserData>(OnUserAdded));
		if (m_IPlayerManager != null)
		{
			m_IPlayerManager.EngagementChangeCallback -= OnEngagementChanged;
		}
		if (m_sendInviteTask != null)
		{
			m_sendInviteTask.CleanUp();
		}
		if (m_switchConnectionModeTask != null)
		{
			m_switchConnectionModeTask.CleanUp();
			m_switchConnectionModeTask.onComplete -= OnSwitchConnectionModeComplete;
			KitchenSwitchConnectionModeTask switchConnectionModeTask = m_switchConnectionModeTask;
			switchConnectionModeTask.onResults = (KitchenSwitchConnectionModeTask.OnResults)Delegate.Remove(switchConnectionModeTask.onResults, new KitchenSwitchConnectionModeTask.OnResults(OnResults));
		}
		ServerUserSystem.OnEngagementPrivilegeCheckStarted = (GenericVoid<IConnectionModeSwitchStatus>)Delegate.Remove(ServerUserSystem.OnEngagementPrivilegeCheckStarted, new GenericVoid<IConnectionModeSwitchStatus>(EngagementPrivilegeCheckStarted));
		ServerUserSystem.OnEngagementPrivilegeCheckCompleted = (GenericVoid<IConnectionModeSwitchStatus>)Delegate.Remove(ServerUserSystem.OnEngagementPrivilegeCheckCompleted, new GenericVoid<IConnectionModeSwitchStatus>(EngagementPrivilegeCheckComplete));
		ConnectionModeSwitcher.InvalidateCallback(OnLeaveKitchenComplete);
		ConnectionModeSwitcher.InvalidateCallback(OnLeaveOnlineModeForPadSplitComplete);
		if (m_NetworkErrorDialog != null)
		{
			InviteMonitor.InviteJoinComplete = (GenericVoid)Delegate.Remove(InviteMonitor.InviteJoinComplete, new GenericVoid(OnInviteJoinComplete));
			m_NetworkErrorDialog.Disable();
			m_NetworkErrorDialog = null;
		}
		DisconnectionHandler.SessionConnectionLostEvent = (GenericVoid)Delegate.Remove(DisconnectionHandler.SessionConnectionLostEvent, new GenericVoid(OnSessionConnectionLost));
		DisconnectionHandler.ConnectionModeErrorEvent = (GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult>>)Delegate.Remove(DisconnectionHandler.ConnectionModeErrorEvent, new GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult>>(OnConnectionModeError));
		DisconnectionHandler.KickedFromSessionEvent = (GenericVoid)Delegate.Remove(DisconnectionHandler.KickedFromSessionEvent, new GenericVoid(OnKickedFromSession));
		DisconnectionHandler.LocalDisconnectionEvent = (GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult>>)Delegate.Remove(DisconnectionHandler.LocalDisconnectionEvent, new GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult>>(OnLocalDisconnection));
	}

	private void OnSessionConnectionLost()
	{
		DisableLeaveKitchenButton();
	}

	private void OnConnectionModeError(OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult> _error)
	{
		DisableLeaveKitchenButton();
	}

	private void OnKickedFromSession()
	{
		DisableLeaveKitchenButton();
	}

	private void OnLocalDisconnection(OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult> _error)
	{
		DisableLeaveKitchenButton();
	}

	public override bool Show(GamepadUser currentGamer, BaseMenuBehaviour parent, GameObject invoker, bool hideInvoker = true)
	{
		bool result = base.Show(currentGamer, parent, invoker, hideInvoker);
		PlayerInputLookup.ResetToDefaultInputConfig();
		UpdateTooltipDefaults();
		ShowIdleMenu();
		return result;
	}

	public void RefreshSearch()
	{
		AdhocSearch();
	}

	private void OnUserAdded(uint _idx, UserData _userData)
	{
		TriggerUserJoinEffect((int)_idx);
	}

	private void TriggerUserJoinEffect(int _userIdx)
	{
		FrontendChef frontendChef = m_PlayerChefs[_userIdx];
		if (frontendChef != null)
		{
			if (m_playerJoinPFXPrefab != null)
			{
				m_playerJoinPFXPrefab.InstantiatePFX(frontendChef.transform);
			}
			if (m_playerJoinAudioTag != GameOneShotAudioTag.COUNT)
			{
				GameUtils.TriggerAudio(m_playerJoinAudioTag, frontendChef.gameObject.layer);
			}
		}
	}

	private void OnUsersChangedEvent()
	{
		RecalculateChefAvatars();
		if (m_CurrentExpandedSlot != null)
		{
			int engagementSlot = (int)m_CurrentExpandedSlot.m_EngagementSlot;
			bool flag = GetClientUserForCurrentSlot() != null;
			if (m_currentSlotHasUser != flag || engagementSlot > ClientUserSystem.m_Users.Count)
			{
				CloseOptionsMenu();
			}
		}
		if (m_CachedEventSystem != null && m_CachedEventSystem.currentSelectedGameObject == null)
		{
			GameObject lastRequestedSelectedGameobject = m_CachedEventSystem.GetLastRequestedSelectedGameobject();
			for (int i = ClientUserSystem.m_Users.Count; i < m_PlayerSlots.Count; i++)
			{
				FrontendPlayerSlot frontendPlayerSlot = m_PlayerSlots[i];
				if (frontendPlayerSlot.m_SlotButton.gameObject == lastRequestedSelectedGameobject && ClientUserSystem.m_Users.Count < m_PlayerSlots.Count)
				{
					GameObject selectedGameObject = m_PlayerSlots[ClientUserSystem.m_Users.Count].m_SlotButton.gameObject;
					m_CachedEventSystem.SetSelectedGameObject(selectedGameObject);
				}
			}
		}
		bool bIsHeadChef = m_bIsHeadChef;
		bool flag2 = false;
		if (m_chalkboardInactivePrompt != null)
		{
			flag2 = m_chalkboardInactivePrompt.gameObject.activeInHierarchy;
		}
		m_bIsHeadChef = !ConnectionStatus.IsInSession() || ConnectionStatus.IsHost();
		if (ClientUserSystem.m_Users.Count != 4)
		{
			if (m_bIsHeadChef && !bIsHeadChef && flag2)
			{
				if (m_chalkboardInactivePrompt != null)
				{
					m_chalkboardInactivePrompt.gameObject.SetActive(true);
				}
			}
			else if (!m_bIsHeadChef && bIsHeadChef && flag2 && m_chalkboardInactivePrompt != null)
			{
				m_chalkboardInactivePrompt.gameObject.SetActive(false);
			}
		}
		UpdatePromptPlayerCount();
		UpdateCurrentConnectionMode();
		UpdateTooltipDefaults();
	}

	private void RecalculateChefAvatars(bool _force = false)
	{
		if (m_PlayerChefs == null)
		{
			return;
		}
		for (int i = 0; i < m_PlayerChefs.Length; i++)
		{
			FrontendChef frontendChef = m_PlayerChefs[i];
			if (!(null != frontendChef))
			{
				continue;
			}
			if (i >= 0 && i < ClientUserSystem.m_Users.Count)
			{
				User user = ClientUserSystem.m_Users._items[i];
				GameSession.SelectedChefData selectedChefData = user.SelectedChefData;
				if (selectedChefData != null)
				{
					m_PlayerChefs[i].SetChefData(selectedChefData, _force);
				}
				m_PlayerChefs[i].SetChefHat(m_ChefHat);
				m_PlayerChefs[i].SetAnimationSet(m_animSets[i]);
				m_PlayerChefs[i].gameObject.SetActive(true);
			}
			else
			{
				m_PlayerChefs[i].gameObject.SetActive(false);
			}
		}
	}

	private void OnEngagementChanged(EngagementSlot _slot, GamepadUser _prevUser, GamepadUser _newUser)
	{
	}

	public override bool Hide(bool restoreInvokerState = true, bool isTabSwitch = false)
	{
		if (!base.Hide(restoreInvokerState, isTabSwitch))
		{
			return false;
		}
		HideAllOptionsButtons();
		return true;
	}

	protected override void Update()
	{
		base.Update();
		if (m_sendInviteTask.isRunning)
		{
			m_sendInviteTask.Update();
		}
		if (m_switchConnectionModeTask.isRunning)
		{
			m_switchConnectionModeTask.Update();
		}
		if (m_progressBox != null)
		{
			string strMessage = string.Empty;
			switch (m_displayProgressInformation)
			{
			case DisplayProgressInfo.ConnectionModeSwitch:
				strMessage = ConnectionModeSwitcher.GetStatus().GetLocalisedProgressDescription();
				break;
			case DisplayProgressInfo.DropInPlayer:
				strMessage = ServerUserSystem.GetEngagementPrivilegeCheckStatus().GetLocalisedProgressDescription();
				break;
			}
			m_progressBox.SetMessage(strMessage, false);
		}
	}

	private void LateUpdate()
	{
		if (m_slotMenuClosedThisFrame)
		{
			m_isPlayerSlotMenuOpen = false;
			m_slotMenuClosedThisFrame = false;
		}
	}

	private void ShowIdleMenu()
	{
		HideAllOptionsButtons();
		if (ConnectionStatus.IsInSession() && !ConnectionStatus.IsHost())
		{
			m_OptionsParent.SetActive(true);
			EnableLeaveKitchenButton();
		}
		UpdateButtonNavigation();
		UpdateTooltipDefaults();
	}

	private void OpenOptionsMenuForPlayerslot(int playerslotIndex)
	{
		if (T17FrontendFlow.Instance != null)
		{
			T17FrontendFlow.Instance.FocusOnMultiplayerKitchen();
		}
		FrontendPlayerSlot frontendPlayerSlot = m_PlayerSlots[playerslotIndex];
		if (!(m_OptionsParent != null))
		{
			return;
		}
		if (m_OptionsParent.activeSelf && m_CurrentExpandedSlot == frontendPlayerSlot)
		{
			HideAllOptionsButtons();
			ShowIdleMenu();
			return;
		}
		HideAllOptionsButtons();
		HidePrompts();
		m_CurrentExpandedSlot = frontendPlayerSlot;
		if (!ConnectionStatus.IsInSession() || ConnectionStatus.IsHost())
		{
			ConfigureMenuForHeadChef();
		}
		else
		{
			ConfigureMenuForSecondaryPlayer();
		}
		UpdateButtonNavigation();
		if (m_CurrentExpandedSlot != null)
		{
			m_CurrentExpandedSlot.SelectSlot();
		}
		m_currentSlotHasUser = GetClientUserForCurrentSlot() != null;
		m_isPlayerSlotMenuOpen = true;
		m_slotMenuClosedThisFrame = false;
	}

	private FrontendPlayerSlot GetSlotToReturnTo()
	{
		if (m_CurrentExpandedSlot != null && m_CurrentExpandedSlot.m_SlotButton != null)
		{
			int engagementSlot = (int)m_CurrentExpandedSlot.m_EngagementSlot;
			if (engagementSlot <= ClientUserSystem.m_Users.Count)
			{
				return m_CurrentExpandedSlot;
			}
			if (ClientUserSystem.m_Users.Count <= m_PlayerSlots.Count)
			{
				return m_PlayerSlots[ClientUserSystem.m_Users.Count];
			}
		}
		return null;
	}

	public void CloseOptionsMenu()
	{
		FrontendPlayerSlot slotToReturnTo = GetSlotToReturnTo();
		if (slotToReturnTo != null)
		{
			m_CachedEventSystem.SetSelectedGameObject(slotToReturnTo.m_SlotButton.gameObject);
		}
		ShowIdleMenu();
		ShowCorrectPrompt();
	}

	private void ConfigureMenuForHeadChef()
	{
		User clientUserForCurrentSlot = GetClientUserForCurrentSlot();
		GamepadUser user = m_IPlayerManager.GetUser(EngagementSlot.One);
		m_OptionsParent.SetActive(true);
		if (clientUserForCurrentSlot == null)
		{
			EnableInviteButton();
			if (CanAddSplitPadGuest())
			{
				EnableSplitPadButton();
			}
			FocusKitchenMenuButton(m_InviteButton);
		}
		else if (clientUserForCurrentSlot.GamepadUser != null && clientUserForCurrentSlot.GamepadUser == user && clientUserForCurrentSlot.Split != User.SplitStatus.SplitPadGuest)
		{
			EnableGamertagButton(clientUserForCurrentSlot.DisplayName);
			EnableControllerOptionsButton(clientUserForCurrentSlot);
			EnableChangeProfileButton();
			FocusKitchenMenuButton(m_GamertagButton);
		}
		else if (!clientUserForCurrentSlot.IsLocal)
		{
			EnableGamertagButton(clientUserForCurrentSlot.DisplayName);
			EnableKickButton();
			FocusKitchenMenuButton(m_GamertagButton);
		}
		else if (clientUserForCurrentSlot.Split == User.SplitStatus.SplitPadGuest)
		{
			EnableRemoveButton();
			FocusKitchenMenuButton(m_RemoveButton);
		}
		else
		{
			EnableControllerOptionsButton(clientUserForCurrentSlot);
			EnableRemoveButton();
			FocusKitchenMenuButton(m_ControllerLeftSideButton.gameObject);
		}
	}

	private void ConfigureMenuForSecondaryPlayer()
	{
		User clientUserForCurrentSlot = GetClientUserForCurrentSlot();
		m_OptionsParent.SetActive(true);
		if (clientUserForCurrentSlot != null)
		{
			if (clientUserForCurrentSlot.IsLocal)
			{
				EnableGamertagButton(clientUserForCurrentSlot.DisplayName);
				EnableControllerOptionsButton(clientUserForCurrentSlot);
				EnableLeaveKitchenButton();
			}
			else
			{
				EnableGamertagButton(clientUserForCurrentSlot.DisplayName);
			}
			UpdateButtonNavigation();
			FocusKitchenMenuButton(m_GamertagButton);
		}
	}

	private void ConfigureMenuForSwitch()
	{
		m_OptionsParent.SetActive(true);
		UpdateCurrentConnectionMode();
		EnableModeButton();
		if (m_currentOnlineMode == OnlineMultiplayerConnectionMode.eAdhoc)
		{
			EnableFriendsButton(false);
		}
		else
		{
			EnableFriendsButton(true);
		}
		if (m_HostingIndicator != null)
		{
			bool active = m_currentOnlineMode == OnlineMultiplayerConnectionMode.eAdhoc && ConnectionStatus.IsHost();
			m_HostingIndicator.SetActive(active);
		}
	}

	private void EnableControllerOptionsButton(User slotUser)
	{
		if (!(m_ControllerOptions == null) && !(m_ControllerLeftSideButton == null) && !(m_ControllerRightSideButton == null) && !(m_ControllerFullButton == null) && slotUser != null)
		{
			m_ControllerOptions.SetActive(true);
			m_ControllerLeftSideButton.gameObject.SetActive(true);
			if (m_ControllerLeftSideImage != null)
			{
				m_ControllerLeftSideImage.sprite = m_ControllerSprites.GetEngagementImage(PadSide.Left, slotUser.GamepadUser.ControlType);
			}
			m_ControllerRightSideButton.gameObject.SetActive(true);
			if (m_ControllerRightSideImage != null)
			{
				m_ControllerRightSideImage.sprite = m_ControllerSprites.GetEngagementImage(PadSide.Right, slotUser.GamepadUser.ControlType);
			}
			m_ControllerFullButton.gameObject.SetActive(slotUser.Split == User.SplitStatus.NotSplit);
			if (m_ControllerFullImage != null)
			{
				m_ControllerFullImage.sprite = m_ControllerSprites.GetEngagementImage(PadSide.Both, slotUser.GamepadUser.ControlType);
			}
		}
	}

	private void EnableGamertagButton(string gamerTag)
	{
		if (m_GamertagButton != null)
		{
			m_GamertagButton.SetActive(true);
			if (m_GamertagButtonText != null)
			{
				m_GamertagButtonText.text = gamerTag;
			}
		}
	}

	private void EnableInviteButton()
	{
		if (m_InviteButton != null)
		{
			m_InviteButton.SetActive(true);
		}
	}

	private void EnableSplitPadButton()
	{
		if (m_SplitPadButton != null)
		{
			m_SplitPadButton.SetActive(true);
		}
	}

	private void EnableChangeProfileButton()
	{
	}

	private void EnableModeButton()
	{
		m_selectedOnlineMode = m_currentOnlineMode;
		if (m_ModeButtonGroup != null)
		{
			m_ModeButtonGroup.SetActive(true);
			UpdateSwitchOnlineModeText();
		}
	}

	private void EnableFriendsButton(bool bEnabled)
	{
		if (m_FriendsButton != null)
		{
			m_FriendsButton.SetActive(bEnabled);
			RectTransform rectTransform = (RectTransform)m_OptionsParent.transform;
			if (rectTransform != null)
			{
				LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
			}
		}
	}

	private void EnableSearchButton(bool bEnabled)
	{
		if (m_SearchButton != null)
		{
			m_SearchButton.SetActive(bEnabled);
		}
	}

	private void EnableKickButton()
	{
		if (m_KickButton != null)
		{
			m_KickButton.SetActive(true);
		}
	}

	private void EnableRemoveButton()
	{
		if (m_RemoveButton != null)
		{
			m_RemoveButton.SetActive(true);
		}
	}

	private void EnableLeaveKitchenButton()
	{
		if (m_LeaveKitchenButton != null)
		{
			m_LeaveKitchenButton.SetActive(true);
		}
	}

	private void DisableLeaveKitchenButton()
	{
		if (m_LeaveKitchenButton != null)
		{
			m_LeaveKitchenButton.SetActive(false);
		}
	}

	private void HideAllOptionsButtons()
	{
		if (m_CurrentExpandedSlot != null)
		{
			m_CurrentExpandedSlot.DeselectSlot();
		}
		m_CurrentExpandedSlot = null;
		m_slotMenuClosedThisFrame = true;
		DisableLeaveKitchenButton();
		if (m_ControllerOptions != null)
		{
			m_ControllerOptions.SetActive(false);
		}
		if (m_ControllerLeftSideButton != null)
		{
			m_ControllerLeftSideButton.gameObject.SetActive(false);
		}
		if (m_ControllerFullButton != null)
		{
			m_ControllerFullButton.gameObject.SetActive(false);
		}
		if (m_ControllerRightSideButton != null)
		{
			m_ControllerRightSideButton.gameObject.SetActive(false);
		}
		if (m_InviteButton != null)
		{
			m_InviteButton.SetActive(false);
		}
		if (m_SplitPadButton != null)
		{
			m_SplitPadButton.SetActive(false);
		}
		if (m_GamertagButton != null)
		{
			m_GamertagButton.SetActive(false);
		}
		if (m_KickButton != null)
		{
			m_KickButton.SetActive(false);
		}
		if (m_RemoveButton != null)
		{
			m_RemoveButton.SetActive(false);
		}
		if (m_ChangeProfileButton != null)
		{
			m_ChangeProfileButton.SetActive(false);
		}
		if (m_ModeButtonGroup != null)
		{
			m_ModeButtonGroup.SetActive(false);
		}
		if (m_FriendsButton != null)
		{
			m_FriendsButton.SetActive(false);
		}
		if (m_SearchButton != null)
		{
			m_SearchButton.SetActive(false);
		}
		ShowCorrectPrompt();
	}

	public void FocusMenu()
	{
		m_bIsFocussed = true;
		SetMouseBlockActive(false);
		if (!(m_CachedEventSystem != null))
		{
			return;
		}
		FastList<User> users = ClientUserSystem.m_Users;
		if (!ConnectionStatus.IsHost() && ConnectionStatus.IsInSession())
		{
			for (int i = 0; i < users.Count; i++)
			{
				if (users._items[i].IsLocal && users._items[i].Engagement == EngagementSlot.One)
				{
					m_CachedEventSystem.SetSelectedGameObject(m_PlayerSlots[i].m_SlotButton.gameObject);
					break;
				}
			}
		}
		else if (users.Count != 4)
		{
			m_CachedEventSystem.SetSelectedGameObject(m_PlayerSlots[users.Count].m_SlotButton.gameObject);
		}
		else
		{
			m_CachedEventSystem.SetSelectedGameObject(m_BorderSelectables.selectOnUp.gameObject);
		}
	}

	public void ShowPlayWithFriendsMenu()
	{
		if (m_switchPlayWithFriendsMenu != null)
		{
			m_rootMenu.CollapseCurrentTab();
			m_rootMenu.OpenFrontendMenu(m_switchPlayWithFriendsMenu);
		}
	}

	private void UpdateTooltipDefaults(bool _setToDefault = true)
	{
		T17TooltipManager t17TooltipManager = T17TooltipManager.Instance;
		if (t17TooltipManager == null)
		{
			t17TooltipManager = UnityEngine.Object.FindObjectOfType<T17TooltipManager>();
		}
		if (t17TooltipManager != null)
		{
			if (!ConnectionStatus.IsInSession() || ConnectionStatus.IsHost())
			{
				t17TooltipManager.SetDefaultTooltip(m_hostTooltip);
			}
			else
			{
				t17TooltipManager.SetDefaultTooltip(m_clientTooltip, new LocToken[1]
				{
					new LocToken("[Player]", ClientUserSystem.m_Users._items[0].DisplayName)
				});
			}
			if (_setToDefault)
			{
				t17TooltipManager.Show(string.Empty, true);
			}
		}
	}

	public void UnFocusMenu()
	{
		m_bIsFocussed = false;
		SetMouseBlockActive(false);
		ShowIdleMenu();
	}

	public void SetMouseBlockActive(bool _active)
	{
		if (m_mouseBlock != null)
		{
			m_mouseBlock.SetActive(_active);
		}
	}

	private void FocusKitchenMenuButton(GameObject button)
	{
		if (button != null)
		{
			m_CachedEventSystem.SetSelectedGameObject(button);
		}
	}

	public void OnLeaveKitchen()
	{
		if (ConnectionStatus.IsInSession())
		{
			ShowProgressSpinnerDialog();
			ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Offline, null, OnLeaveKitchenComplete);
		}
	}

	private void OnLeaveKitchenComplete(IConnectionModeSwitchStatus status)
	{
		HideProgressSpinnerDialog();
		CloseOptionsMenu();
		T17FrontendFlow.Instance.OnSessionLeft();
	}

	public void OnUseLeftHalfOnly()
	{
		User clientUserForCurrentSlot = GetClientUserForCurrentSlot();
		if (clientUserForCurrentSlot != null && clientUserForCurrentSlot.PadSide != PadSide.Left)
		{
			ClientMessenger.ControllerSettings(PadSide.Left, clientUserForCurrentSlot);
			if (clientUserForCurrentSlot.GamepadUser != null)
			{
				clientUserForCurrentSlot.GamepadUser.Side = PadSide.Left;
			}
			User splitPadPartner = GetSplitPadPartner(clientUserForCurrentSlot);
			if (splitPadPartner != null && splitPadPartner.Split == User.SplitStatus.SplitPadGuest)
			{
				ClientMessenger.ControllerSettings(PadSide.Right, splitPadPartner);
			}
		}
	}

	public void OnUseFullPad()
	{
		User clientUserForCurrentSlot = GetClientUserForCurrentSlot();
		if (clientUserForCurrentSlot != null && clientUserForCurrentSlot.PadSide != PadSide.Both && clientUserForCurrentSlot.Split == User.SplitStatus.NotSplit)
		{
			ClientMessenger.ControllerSettings(PadSide.Both, clientUserForCurrentSlot);
			if (clientUserForCurrentSlot.GamepadUser != null)
			{
				clientUserForCurrentSlot.GamepadUser.Side = PadSide.Both;
			}
		}
	}

	public void OnUseRightHalfOnly()
	{
		User clientUserForCurrentSlot = GetClientUserForCurrentSlot();
		if (clientUserForCurrentSlot != null && clientUserForCurrentSlot.PadSide != PadSide.Right)
		{
			ClientMessenger.ControllerSettings(PadSide.Right, clientUserForCurrentSlot);
			if (clientUserForCurrentSlot.GamepadUser != null)
			{
				clientUserForCurrentSlot.GamepadUser.Side = PadSide.Right;
			}
			User splitPadPartner = GetSplitPadPartner(clientUserForCurrentSlot);
			if (splitPadPartner != null && splitPadPartner.Split == User.SplitStatus.SplitPadGuest)
			{
				ClientMessenger.ControllerSettings(PadSide.Left, splitPadPartner);
			}
		}
	}

	public void OnSplitPad()
	{
		User firstUnsplitUser = GetFirstUnsplitUser();
		if (firstUnsplitUser == null)
		{
			return;
		}
		if (ConnectionStatus.IsInSession())
		{
			if (UserSystemUtils.AnyRemoteUsers())
			{
				NetworkDialogHelper.ShowGoingOfflineDialog(LeaveOnlineModeThenSplitPad);
			}
			else
			{
				LeaveOnlineModeThenSplitPad();
			}
			return;
		}
		ServerUserSystem.SplitUser(firstUnsplitUser.Engagement);
		Analytics.LogEvent("Split Pad User", "Players", ServerUserSystem.m_Users.Count);
		if (m_CurrentExpandedSlot != null)
		{
			m_CachedEventSystem.SetSelectedGameObject(m_CurrentExpandedSlot.m_SlotButton.gameObject);
		}
		CloseOptionsMenu();
	}

	private void LeaveOnlineModeThenSplitPad()
	{
		ShowProgressSpinnerDialog();
		ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Offline, null, OnLeaveOnlineModeForPadSplitComplete);
	}

	private void OnLeaveOnlineModeForPadSplitComplete(IConnectionModeSwitchStatus status)
	{
		HideProgressSpinnerDialog();
		if (status.GetResult() == eConnectionModeSwitchResult.Success)
		{
			OnSplitPad();
		}
	}

	public void OnInvite()
	{
		if (!m_sendInviteTask.isRunning)
		{
			m_sendInviteTask.Start();
		}
	}

	private void OnSendInviteTaskComplete(KitchenTaskResult result)
	{
		if (result != KitchenTaskResult.Success)
		{
			IConnectionModeSwitchStatus status = ConnectionModeSwitcher.GetStatus();
			if (!status.DisplayPlatformDialog())
			{
				T17DialogBox dialog = T17DialogBoxManager.GetDialog(false);
				if (dialog != null)
				{
					string text = status.GetLocalisedResultDescription();
					if (string.IsNullOrEmpty(text))
					{
						text = Localization.Get("Text.Kitchen.Invite.Failed");
					}
					dialog.Initialize("Text.Warning", text, "Text.Button.Continue", null, null, T17DialogBox.Symbols.Warning, true, false);
					dialog.Show();
				}
			}
			ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Offline);
		}
		if (T17FrontendFlow.Instance != null)
		{
			T17FrontendFlow.Instance.FocusOnMainMenu();
		}
	}

	public void OnResults(SearchTask.SearchResultData results)
	{
		if (m_switchSearchMenu != null)
		{
			m_switchSearchMenu.SetResults(results);
		}
	}

	public void SwitchConnectionMode(OnlineMultiplayerConnectionMode mode)
	{
		if (!m_switchConnectionModeTask.isRunning)
		{
			m_selectedOnlineMode = mode;
			switch (mode)
			{
			case OnlineMultiplayerConnectionMode.eNone:
				m_switchConnectionModeTask.connectionMode = KitchenSwitchConnectionModeTask.Mode.Offline;
				break;
			case OnlineMultiplayerConnectionMode.eInternet:
				m_switchConnectionModeTask.connectionMode = KitchenSwitchConnectionModeTask.Mode.Internet;
				break;
			case OnlineMultiplayerConnectionMode.eAdhoc:
				m_switchConnectionModeTask.connectionMode = KitchenSwitchConnectionModeTask.Mode.Wireless;
				break;
			}
			m_switchConnectionModeTask.Start();
		}
	}

	private void OnSwitchConnectionModeComplete(KitchenTaskResult result)
	{
		if (m_CachedEventSystem != null && m_ModeButton != null)
		{
			m_CachedEventSystem.SetSelectedGameObject(m_ModeButton.gameObject);
		}
		switch (result)
		{
		case KitchenTaskResult.Success:
			m_currentOnlineMode = m_selectedOnlineMode;
			ConfigureMenuForSwitch();
			if (m_switchConnectionModeTask.connectionMode == KitchenSwitchConnectionModeTask.Mode.Wireless && !m_switchConnectionModeTask.m_hosting)
			{
				m_rootMenu.OpenFrontendMenu(m_switchSearchMenu);
			}
			else if (m_switchConnectionModeTask.connectionMode == KitchenSwitchConnectionModeTask.Mode.Offline)
			{
				T17FrontendFlow.Instance.OnSessionLeft();
			}
			break;
		case KitchenTaskResult.Failure:
		{
			IConnectionModeSwitchStatus status = ConnectionModeSwitcher.GetStatus();
			bool flag = status.DisplayPlatformDialog();
			if (!flag && m_switchConnectionModeTask.connectionMode == KitchenSwitchConnectionModeTask.Mode.JoinRoom)
			{
				CompositeStatus compositeStatus = status as CompositeStatus;
				if (compositeStatus != null)
				{
					JoinSessionStatus joinSessionStatus = compositeStatus.m_TaskSubStatus as JoinSessionStatus;
					if (joinSessionStatus != null && joinSessionStatus.sessionJoinResult != null)
					{
						OnlineMultiplayerSessionJoinResult returnCode = joinSessionStatus.sessionJoinResult.m_returnCode;
						flag = ((returnCode != OnlineMultiplayerSessionJoinResult.eFull && returnCode != OnlineMultiplayerSessionJoinResult.eNotEnoughRoomForAllLocalUsers) ? true : false);
					}
					else
					{
						flag = true;
					}
				}
				else
				{
					flag = true;
				}
			}
			if (!flag)
			{
				T17DialogBox dialog = T17DialogBoxManager.GetDialog(false);
				if (dialog != null)
				{
					string text = status.GetLocalisedResultDescription();
					if (string.IsNullOrEmpty(text))
					{
						text = Localization.Get("Online.ConnectionMode.ConnectionMode.Result.eGeneric");
					}
					dialog.Initialize("Text.Warning", text, "Text.Button.Continue", null, null, T17DialogBox.Symbols.Warning, true, false);
					dialog.Show();
				}
			}
			if (m_switchConnectionModeTask.connectionMode == KitchenSwitchConnectionModeTask.Mode.JoinRoom)
			{
				IPlayerManager playerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
				OfflineOptions offlineOptions = new OfflineOptions
				{
					hostUser = playerManager.GetUser(EngagementSlot.One),
					eAdditionalAction = OfflineOptions.AdditionalAction.None,
					connectionMode = OnlineMultiplayerConnectionMode.eNone
				};
				ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Offline, offlineOptions, OnAdhocJoinFailToOfflineComplete);
			}
			m_selectedOnlineMode = m_currentOnlineMode;
			UpdateSwitchOnlineModeText();
			break;
		}
		case KitchenTaskResult.Cancelled:
			m_selectedOnlineMode = m_currentOnlineMode;
			UpdateSwitchOnlineModeText();
			break;
		}
	}

	public void OnOpenGamertag()
	{
		User clientUserForCurrentSlot = GetClientUserForCurrentSlot();
		if (clientUserForCurrentSlot != null)
		{
			if (clientUserForCurrentSlot.IsLocal)
			{
				m_IPlayerManager.ShowGamerCard(clientUserForCurrentSlot.GamepadUser);
			}
			else
			{
				m_IPlayerManager.ShowGamerCard(clientUserForCurrentSlot.PlatformID);
			}
		}
	}

	public void OnRemovePlayer()
	{
		if (!(m_CurrentExpandedSlot != null))
		{
			return;
		}
		User serverUserForCurrentSlot = GetServerUserForCurrentSlot();
		if (serverUserForCurrentSlot == null)
		{
			return;
		}
		if (serverUserForCurrentSlot.Split == User.SplitStatus.SplitPadGuest)
		{
			ServerUserSystem.RemoveUser(serverUserForCurrentSlot);
		}
		else if (!serverUserForCurrentSlot.IsLocal)
		{
			FastList<User> users = ServerUserSystem.m_Users;
			User.MachineID machine = serverUserForCurrentSlot.Machine;
			User[] array = UserSystemUtils.FindUsers(users, null, machine);
			if (array != null && array.Length > 0)
			{
				for (int i = 0; i < array.Length; i++)
				{
					ServerUserSystem.RemoveUser(array[i], i == array.Length - 1);
				}
			}
		}
		else
		{
			m_IPlayerManager.DisengagePad(serverUserForCurrentSlot.Engagement);
		}
		CloseOptionsMenu();
	}

	public void OnChangeProfile()
	{
	}

	public void OnMode()
	{
		if (m_isSelectingMode)
		{
			if (m_selectedOnlineMode != m_currentOnlineMode || m_currentOnlineMode == OnlineMultiplayerConnectionMode.eAdhoc)
			{
				SwitchConnectionMode(m_selectedOnlineMode);
			}
			m_isSelectingMode = false;
		}
		else
		{
			m_selectedOnlineMode = m_currentOnlineMode;
			m_isSelectingMode = true;
		}
		EnableSwitchOnlineModeSelectionArrows(m_isSelectingMode);
		UpdateModeSelectionTooltip();
	}

	public void OnCancelOnlineModeSelection()
	{
		if (m_isSelectingMode)
		{
			m_selectedOnlineMode = m_currentOnlineMode;
			UpdateSwitchOnlineModeText();
			EnableSwitchOnlineModeSelectionArrows(false);
			m_isSelectingMode = false;
			UpdateModeSelectionTooltip();
		}
	}

	public void OnFriends()
	{
		if (m_currentOnlineMode != OnlineMultiplayerConnectionMode.eInternet)
		{
			m_switchConnectionModeTask.onComplete += OpenFriendsAfterConnectionModeComplete;
			SwitchConnectionMode(OnlineMultiplayerConnectionMode.eInternet);
		}
		else if (m_switchFriendsMenu != null && m_rootMenu != null)
		{
			m_switchFriendsMenu.Hide();
			m_rootMenu.OpenFrontendMenu(m_switchFriendsMenu);
		}
	}

	private void OpenFriendsAfterConnectionModeComplete(KitchenTaskResult result)
	{
		if (result == KitchenTaskResult.Success && m_switchFriendsMenu != null && m_rootMenu != null)
		{
			m_switchFriendsMenu.Hide();
			m_rootMenu.OpenFrontendMenu(m_switchFriendsMenu);
		}
		m_switchConnectionModeTask.onComplete -= OpenFriendsAfterConnectionModeComplete;
	}

	public void OnSearch()
	{
	}

	public void OnSearchCancelled()
	{
		if (m_currentOnlineMode != OnlineMultiplayerConnectionMode.eNone)
		{
			m_switchConnectionModeTask.onComplete += OpenPlayWithFriendsAfterConnectionModeComplete;
			SwitchConnectionMode(OnlineMultiplayerConnectionMode.eNone);
		}
		else
		{
			ShowPlayWithFriendsMenu();
		}
	}

	private void OpenPlayWithFriendsAfterConnectionModeComplete(KitchenTaskResult result)
	{
		if (result == KitchenTaskResult.Success)
		{
			ShowPlayWithFriendsMenu();
		}
		m_switchConnectionModeTask.onComplete -= OpenPlayWithFriendsAfterConnectionModeComplete;
	}

	private bool CanAddSplitPadGuest()
	{
		if (ServerUserSystem.m_Users.Count >= 4)
		{
			return false;
		}
		User firstUnsplitUser = GetFirstUnsplitUser();
		return firstUnsplitUser != null;
	}

	private GameInputConfig BuildInputConfig()
	{
		List<PlayerGameInput> list = new List<PlayerGameInput>();
		for (int i = 0; i < 4; i++)
		{
			PlayerGameInput engagedPlayerGameInput = LobbyUIController.GetEngagedPlayerGameInput(m_IPlayerManager, i);
			if (engagedPlayerGameInput != null)
			{
				list.Add(engagedPlayerGameInput);
			}
		}
		GameInputConfig.ConfigEntry[] array = new GameInputConfig.ConfigEntry[list.Count];
		for (int j = 0; j < list.Count; j++)
		{
			PlayerInputLookup.Player player = (PlayerInputLookup.Player)j;
			array[j] = new GameInputConfig.ConfigEntry(player, list[j].Pad, list[j].Side, ClientUserSystem.s_LocalMachineId, list[j].AmbiControlsMapping);
		}
		return new GameInputConfig(array);
	}

	private User GetClientUserForCurrentSlot()
	{
		User result = null;
		if (m_CurrentExpandedSlot != null)
		{
			int engagementSlot = (int)m_CurrentExpandedSlot.m_EngagementSlot;
			FastList<User> users = ClientUserSystem.m_Users;
			if (engagementSlot < users.Count)
			{
				result = users._items[engagementSlot];
			}
		}
		return result;
	}

	private User GetServerUserForCurrentSlot()
	{
		User result = null;
		if (m_CurrentExpandedSlot != null)
		{
			int engagementSlot = (int)m_CurrentExpandedSlot.m_EngagementSlot;
			FastList<User> users = ServerUserSystem.m_Users;
			if (engagementSlot < users.Count)
			{
				result = users._items[engagementSlot];
			}
		}
		return result;
	}

	private User GetFirstUnsplitUser()
	{
		for (int i = 0; i < ServerUserSystem.m_Users.Count; i++)
		{
			if (ServerUserSystem.m_Users._items[i].Split == User.SplitStatus.NotSplit)
			{
				return ServerUserSystem.m_Users._items[i];
			}
		}
		return null;
	}

	private User GetSplitPadPartner(User me)
	{
		User result = null;
		if (me != null && (me.Split == User.SplitStatus.SplitPadHost || me.Split == User.SplitStatus.SplitPadGuest))
		{
			FastList<User> users = ServerUserSystem.m_Users;
			User.MachineID machine = me.Machine;
			User[] array = UserSystemUtils.FindUsers(users, null, machine, me.Engagement);
			for (int i = 0; i < array.Length; i++)
			{
				if (array[i].Split != me.Split)
				{
					result = array[i];
					break;
				}
			}
		}
		return result;
	}

	private void UpdateButtonNavigation()
	{
		T17Button[] array = m_OptionsParent.RequestComponentsRecursive<T17Button>();
		array = array.AllRemoved_Predicate((T17Button x) => !x.isActiveAndEnabled || !x.IsInteractable() || (m_ControllerFullButton != null && m_ControllerFullButton.gameObject.activeInHierarchy && x == m_ControllerLeftSideButton) || x == m_ControllerRightSideButton);
		for (int num = 0; num < m_PlayerSlots.Count; num++)
		{
			T17Button slotButton = m_PlayerSlots[num].m_SlotButton;
			Navigation navigation = slotButton.navigation;
			navigation.selectOnDown = ((array.Length <= 0) ? null : array[0]);
			slotButton.navigation = navigation;
		}
		if (array.Length <= 0)
		{
			return;
		}
		for (int num2 = 0; num2 < array.Length; num2++)
		{
			T17Button t17Button = array[num2];
			Navigation navigation2 = t17Button.navigation;
			navigation2.selectOnUp = ((num2 <= 0) ? null : array[num2 - 1]);
			navigation2.selectOnDown = ((num2 >= array.Length - 1) ? null : array[num2 + 1]);
			t17Button.navigation = navigation2;
		}
		T17Button t17Button2 = array[0];
		Navigation navigation3 = t17Button2.navigation;
		FrontendPlayerSlot slotToReturnTo = GetSlotToReturnTo();
		if (slotToReturnTo != null)
		{
			navigation3.selectOnUp = slotToReturnTo.m_SlotButton;
		}
		t17Button2.navigation = navigation3;
		if (m_ControllerFullButton != null)
		{
			if (m_ControllerLeftSideButton != null)
			{
				Navigation navigation4 = m_ControllerLeftSideButton.navigation;
				navigation4.selectOnUp = m_ControllerFullButton.navigation.selectOnUp;
				navigation4.selectOnDown = m_ControllerFullButton.navigation.selectOnDown;
				m_ControllerLeftSideButton.navigation = navigation4;
			}
			if (m_ControllerRightSideButton != null)
			{
				Navigation navigation5 = m_ControllerRightSideButton.navigation;
				navigation5.selectOnUp = m_ControllerFullButton.navigation.selectOnUp;
				navigation5.selectOnDown = m_ControllerFullButton.navigation.selectOnDown;
				m_ControllerRightSideButton.navigation = navigation5;
			}
		}
		else if (m_ControllerLeftSideButton != null && m_ControllerRightSideButton != null)
		{
			Navigation navigation6 = m_ControllerRightSideButton.navigation;
			navigation6.selectOnUp = m_ControllerLeftSideButton.navigation.selectOnUp;
			navigation6.selectOnDown = m_ControllerLeftSideButton.navigation.selectOnDown;
			m_ControllerRightSideButton.navigation = navigation6;
		}
	}

	private void ShowProgressSpinnerDialog()
	{
		if (m_progressBox == null)
		{
			m_progressBox = T17DialogBoxManager.GetDialog(false);
			if (m_progressBox != null)
			{
				m_progressBox.Initialize("Text.PleaseWait", string.Empty, null, null, null, T17DialogBox.Symbols.Spinner);
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
		m_displayProgressInformation = DisplayProgressInfo.ConnectionModeSwitch;
	}

	private void SelectHeadChefSlot(bool bImmediate = false)
	{
		if (!(m_PlayerSlots[0] != null) || !(m_PlayerSlots[0].m_SlotButton != null))
		{
			return;
		}
		if (bImmediate)
		{
			EventSystem cachedEventSystem = m_CachedEventSystem;
			if (cachedEventSystem != null)
			{
				cachedEventSystem.SetSelectedGameObject(m_PlayerSlots[0].m_SlotButton.gameObject);
			}
		}
		else
		{
			m_CachedEventSystem.SetSelectedGameObject(m_PlayerSlots[0].m_SlotButton.gameObject);
		}
	}

	private void OnNetworkErrorDismissed()
	{
		if (m_bIsFocussed)
		{
			SelectHeadChefSlot();
		}
	}

	private void OnInviteJoinComplete()
	{
		PadSide currentPadSide = m_PlayerSlots[0].CurrentPadSide;
		FastList<User> users = ClientUserSystem.m_Users;
		User.MachineID s_LocalMachineId = ClientUserSystem.s_LocalMachineId;
		User user = UserSystemUtils.FindUser(users, null, s_LocalMachineId, EngagementSlot.One);
		ClientMessenger.ControllerSettings(currentPadSide, user);
		ShowIdleMenu();
		if (m_bIsFocussed)
		{
			FocusOnInviteAcceptedItem();
		}
		for (int i = 0; i < ClientUserSystem.m_Users.Count; i++)
		{
			TriggerUserJoinEffect(i);
		}
		SelectSaveDialog[] array = T17FrontendFlow.Instance.gameObject.RequestComponentsRecursive<SelectSaveDialog>();
		if (array == null || array.Length <= 0)
		{
			return;
		}
		for (int j = 0; j < array.Length; j++)
		{
			if (array[j].isActiveAndEnabled)
			{
				array[j].Close();
			}
		}
	}

	private void EngagementPrivilegeCheckStarted(IConnectionModeSwitchStatus status)
	{
		m_displayProgressInformation = DisplayProgressInfo.DropInPlayer;
		ShowProgressSpinnerDialog();
	}

	private void EngagementPrivilegeCheckComplete(IConnectionModeSwitchStatus status)
	{
		HideProgressSpinnerDialog();
		if (status.GetResult() != eConnectionModeSwitchResult.Success && !status.DisplayPlatformDialog())
		{
			T17DialogBox dialog = T17DialogBoxManager.GetDialog(false);
			if (dialog != null)
			{
				dialog.Initialize("Text.Warning", status.GetLocalisedResultDescription(), "Text.Button.Continue", null, null, T17DialogBox.Symbols.Warning, true, false);
				dialog.Show();
			}
		}
	}

	public void UpdateCurrentConnectionMode()
	{
		m_currentOnlineMode = ConnectionStatus.CurrentConnectionMode();
		m_selectedOnlineMode = m_currentOnlineMode;
		UpdateSwitchOnlineModeText();
		for (int i = 0; i < m_PlayerSlots.Count; i++)
		{
			if (m_PlayerSlots[i] != null)
			{
				m_PlayerSlots[i].OnConnectionModeUpdated();
			}
		}
	}

	private void UpdateSwitchOnlineModeSelection()
	{
		OnlineMultiplayerConnectionMode onlineMultiplayerConnectionMode = m_selectedOnlineMode;
		if (m_cycleModeLeftButton != null && m_cycleModeLeftButton.JustPressed())
		{
			onlineMultiplayerConnectionMode = ((m_selectedOnlineMode != OnlineMultiplayerConnectionMode.eNone) ? (m_selectedOnlineMode - 1) : OnlineMultiplayerConnectionMode.eAdhoc);
		}
		if (m_cycleModeRightButton != null && m_cycleModeRightButton.JustPressed())
		{
			onlineMultiplayerConnectionMode = ((m_selectedOnlineMode != OnlineMultiplayerConnectionMode.eAdhoc) ? (m_selectedOnlineMode + 1) : OnlineMultiplayerConnectionMode.eNone);
		}
		if (m_selectedOnlineMode != onlineMultiplayerConnectionMode)
		{
			m_selectedOnlineMode = onlineMultiplayerConnectionMode;
			UpdateSwitchOnlineModeText();
			UpdateModeSelectionTooltip();
		}
	}

	private void UpdateSwitchOnlineModeText()
	{
		if (m_ModeText != null)
		{
			m_ModeText.SetNonLocalizedText(Localization.Get("MainMenu.Kitchen.Mode." + m_selectedOnlineMode));
		}
	}

	public void LeaveSession()
	{
		if (ConnectionStatus.IsInSession())
		{
			m_leaveSessionDialogBox = T17DialogBoxManager.GetDialog(false);
			if (m_leaveSessionDialogBox != null)
			{
				m_leaveSessionDialogBox.Initialize("Text.LeaveSession.Title", "Text.LeaveSession.Body", "Text.Button.Confirm", null, "Text.Button.Cancel");
				T17DialogBox leaveSessionDialogBox = m_leaveSessionDialogBox;
				leaveSessionDialogBox.OnConfirm = (T17DialogBox.DialogEvent)Delegate.Combine(leaveSessionDialogBox.OnConfirm, new T17DialogBox.DialogEvent(LeaveSessionConfirmed));
				m_leaveSessionDialogBox.Show();
			}
		}
	}

	private void LeaveSessionConfirmed()
	{
		if (!m_switchConnectionModeTask.isRunning)
		{
			m_switchConnectionModeTask.connectionMode = KitchenSwitchConnectionModeTask.Mode.Offline;
			m_switchConnectionModeTask.Start();
		}
	}

	private void EnableSwitchOnlineModeSelectionArrows(bool bEnabled)
	{
		if (m_LeftModeArrow != null)
		{
			m_LeftModeArrow.gameObject.SetActive(bEnabled);
		}
		if (m_RightModeArrow != null)
		{
			m_RightModeArrow.gameObject.SetActive(bEnabled);
		}
	}

	private void AdhocSearch()
	{
		GamepadUser user = m_IPlayerManager.GetUser(EngagementSlot.One);
		ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Offline, new OfflineOptions
		{
			hostUser = user,
			searchGameMode = GameMode.OnlineKitchen,
			eAdditionalAction = OfflineOptions.AdditionalAction.PrivilegeCheckAllUsersAndSearchForGames,
			connectionMode = OnlineMultiplayerConnectionMode.eAdhoc
		}, OnSearchComplete);
	}

	private void OnAdhocJoinFailToOfflineComplete(IConnectionModeSwitchStatus status)
	{
		UpdateCurrentConnectionMode();
		UpdateTooltipDefaults();
	}

	private void OnSearchComplete(IConnectionModeSwitchStatus status)
	{
		if (status != null && status.GetResult() == eConnectionModeSwitchResult.Success)
		{
			SearchTask.SearchResultData searchResultData = ConnectionModeSwitcher.GetAgentData() as SearchTask.SearchResultData;
			if (searchResultData != null && searchResultData.m_AvailableSessions != null)
			{
				OnResults(searchResultData);
				m_switchSearchMenu.Hide();
				m_rootMenu.OpenFrontendMenu(m_switchSearchMenu);
			}
		}
		else if (status != null && status.GetResult() == eConnectionModeSwitchResult.Failure)
		{
			m_switchSearchMenu.Hide();
			if (T17FrontendFlow.Instance != null)
			{
				T17FrontendFlow.Instance.FocusOnMainMenu();
			}
		}
		if (m_progressBox != null)
		{
			m_progressBox.Hide();
			m_progressBox = null;
		}
	}

	private void ShowCorrectPrompt()
	{
		if (ClientUserSystem.m_Users.Count == 4)
		{
			return;
		}
		if (m_bIsHeadChef && (!ConnectionStatus.IsInSession() || ConnectionStatus.IsHost()))
		{
			if (m_chalkboardInactivePrompt != null)
			{
				m_chalkboardInactivePrompt.gameObject.SetActive(true);
			}
		}
		else if (m_chalkboardInactivePrompt != null)
		{
			m_chalkboardInactivePrompt.gameObject.SetActive(false);
		}
	}

	private void HidePrompts()
	{
		if (m_chalkboardInactivePrompt != null)
		{
			m_chalkboardInactivePrompt.gameObject.SetActive(false);
		}
	}

	private void UpdatePromptPlayerCount()
	{
		if (!string.IsNullOrEmpty(m_HeadChefPrompt) && m_chalkboardInactivePrompt != null)
		{
			if (!IsPlayerSlotMenuOpen)
			{
				bool active = ClientUserSystem.m_Users.Count != 4 && (!ConnectionStatus.IsInSession() || ConnectionStatus.IsHost());
				m_chalkboardInactivePrompt.gameObject.SetActive(active);
			}
			int num = ClientUserSystem.m_Users.Count + 1;
			if (m_lastPlayerCountForPrompt != num)
			{
				string nonLocalizedText = Localization.Get(m_HeadChefPrompt, new LocToken("PlayerCount", num.ToString()));
				m_chalkboardInactivePrompt.SetNonLocalizedText(nonLocalizedText);
				m_lastPlayerCountForPrompt = num;
			}
		}
	}

	private void UpdateModeSelectionTooltip()
	{
		if (m_isSelectingMode)
		{
			switch (m_selectedOnlineMode)
			{
			case OnlineMultiplayerConnectionMode.eNone:
				m_ModeButton.m_TooltipTag = s_OfflineModeTooltip;
				break;
			case OnlineMultiplayerConnectionMode.eInternet:
				m_ModeButton.m_TooltipTag = s_OnlineModeTooltip;
				break;
			case OnlineMultiplayerConnectionMode.eAdhoc:
				m_ModeButton.m_TooltipTag = s_WirelessModeTooltip;
				break;
			}
		}
		else
		{
			m_ModeButton.m_TooltipTag = s_ModeTooltip;
		}
		if (T17TooltipManager.Instance != null)
		{
			T17TooltipManager.Instance.Show(m_ModeButton.m_TooltipTag);
		}
	}

	private void FocusOnInviteAcceptedItem()
	{
		EventSystem cachedEventSystem = m_CachedEventSystem;
		if (cachedEventSystem != null)
		{
			if (m_InviteFocusItem == null || !m_InviteFocusItem.IsInHierarchyOf(base.gameObject))
			{
				T17FrontendFlow.Instance.FocusOnMainMenu();
			}
			cachedEventSystem.SetSelectedGameObject(m_InviteFocusItem);
		}
	}

	public void JoinAdhocGame(JoinEnumeratedRoomOptions joinOptions, string progressText)
	{
		if (!m_switchConnectionModeTask.isRunning)
		{
			m_switchConnectionModeTask.connectionMode = KitchenSwitchConnectionModeTask.Mode.JoinRoom;
			m_switchConnectionModeTask.joinOptions = joinOptions;
			m_switchConnectionModeTask.joinProgressText = progressText;
			m_switchConnectionModeTask.Start();
		}
	}
}
