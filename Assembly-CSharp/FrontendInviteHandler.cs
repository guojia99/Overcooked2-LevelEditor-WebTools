using System;
using System.Collections.Generic;
using Team17.Online;

public class FrontendInviteHandler : InviteHandler
{
	private enum InviteProcessingState
	{
		eIdle = 0,
		eGoingOffline = 1,
		eDisengageNonRequiredUsers = 2,
		eStartJoinProcess = 3,
		eJoining = 4
	}

	private enum PlayTogetherHostingState
	{
		eIdle = 0,
		eGoingOffline = 1,
		eDisengageNonRequiredUsers = 2,
		eStartHosting = 3,
		eHosting = 4
	}

	private bool m_bBusy;

	private T17DialogBox m_progressBox;

	private AcceptInviteData m_InviteData = new AcceptInviteData();

	private bool m_InviteRequiresAllActiveLocalUsers;

	private InviteProcessingState m_inviteProcessingState;

	private PlayTogetherHostingState m_playTogetherHostingState;

	private OnlineMultiplayerSessionPlayTogetherHosting m_PlayTogetherData;

	private T17DialogBox m_dialogBox;

	private IConnectionModeSwitchStatus m_FailedStatus;

	public void Start()
	{
		m_PlayTogetherData = InviteMonitor.GetPlayTogetherHost();
		if (m_PlayTogetherData != null)
		{
			HandlePlayTogetherHost(m_PlayTogetherData);
		}
		else
		{
			HandleAcceptedInvite(InviteMonitor.GetAcceptedInvite());
		}
		PlayerManager playerManager = GameUtils.RequireManager<PlayerManager>();
		playerManager.EngagementChangeCallback += OnEngagementChanged;
	}

	private void LoadIISScreen(IConnectionModeSwitchStatus status)
	{
		ServerUserSystem.UnlockEngagement();
		UserSystemUtils.RemoveAllSplitPadGuestUsers();
		IPlayerManager playerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
		for (int i = 0; i < 4; i++)
		{
			GamepadUser user = playerManager.GetUser((EngagementSlot)i);
			if (null != user)
			{
				playerManager.DisengagePad((EngagementSlot)i);
			}
		}
	}

	private void OnEngagementChanged(EngagementSlot _e, GamepadUser _prev, GamepadUser _new)
	{
		if (_e == EngagementSlot.One && _new != null && m_InviteData != null)
		{
			HandleAcceptedInvite(m_InviteData);
		}
	}

	public void Stop()
	{
		if (m_bBusy)
		{
			m_bBusy = false;
			m_inviteProcessingState = InviteProcessingState.eIdle;
			m_playTogetherHostingState = PlayTogetherHostingState.eIdle;
			m_PlayTogetherData = null;
			m_InviteData = null;
			ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Offline);
		}
		if (null != m_dialogBox && m_dialogBox.IsActive)
		{
			m_dialogBox.Hide();
			m_dialogBox = null;
		}
		if (null != m_progressBox && m_progressBox.IsActive)
		{
			m_progressBox.Hide();
			m_progressBox = null;
		}
		PlayerManager playerManager = GameUtils.RequireManager<PlayerManager>();
		playerManager.EngagementChangeCallback -= OnEngagementChanged;
	}

	public void Update()
	{
		switch (m_inviteProcessingState)
		{
		case InviteProcessingState.eDisengageNonRequiredUsers:
			m_inviteProcessingState = InviteProcessingState.eStartJoinProcess;
			UserSystemUtils.DisengageNonRequiredUsersForOnline(m_InviteRequiresAllActiveLocalUsers);
			break;
		case InviteProcessingState.eStartJoinProcess:
			if (ServerUserSystem.m_Users.Count < OnlineMultiplayerConfig.MaxPlayers)
			{
				m_inviteProcessingState = InviteProcessingState.eJoining;
				ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.AcceptInvite, m_InviteData, OnAcceptInviteConnectionStateComplete);
				m_InviteData = null;
				break;
			}
			m_InviteData = null;
			m_bBusy = false;
			m_inviteProcessingState = InviteProcessingState.eIdle;
			if (null != m_progressBox)
			{
				m_progressBox.Hide();
				m_progressBox = null;
			}
			NetworkDialogHelper.ShowTooManyLocalUsersForJoining();
			break;
		}
		switch (m_playTogetherHostingState)
		{
		case PlayTogetherHostingState.eDisengageNonRequiredUsers:
			m_playTogetherHostingState = PlayTogetherHostingState.eStartHosting;
			UserSystemUtils.DisengageNonRequiredUsersForOnline(true);
			break;
		case PlayTogetherHostingState.eStartHosting:
		{
			m_playTogetherHostingState = PlayTogetherHostingState.eHosting;
			ServerOptions serverOptions = new ServerOptions
			{
				gameMode = GameMode.OnlineKitchen,
				visibility = OnlineMultiplayerSessionVisibility.ePrivate,
				hostUser = GameUtils.RequireManagerInterface<IPlayerManager>().GetUser(EngagementSlot.One),
				connectionMode = OnlineMultiplayerConnectionMode.eInternet,
				playTogetherHost = m_PlayTogetherData
			};
			m_PlayTogetherData = null;
			ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Server, serverOptions, OnPlayTogetherHostComplete);
			break;
		}
		}
		if (m_progressBox != null)
		{
			string localisedProgressDescription = ConnectionModeSwitcher.GetStatus().GetLocalisedProgressDescription();
			m_progressBox.SetMessage(localisedProgressDescription, false);
		}
	}

	public void HandleAcceptedInvite(AcceptInviteData invite)
	{
		if (!(null == m_dialogBox))
		{
			return;
		}
		m_InviteData = invite;
		m_inviteProcessingState = InviteProcessingState.eIdle;
		GamepadUser user = GameUtils.RequireManager<PlayerManager>().GetUser(EngagementSlot.One);
		if (!(null != user) || m_InviteData == null)
		{
			return;
		}
		if (m_InviteData.Invite.WasAcceptedBy(user))
		{
			m_InviteData.User = user;
			switch (m_InviteData.JoinLocalUsersChoice)
			{
			case AcceptInviteData.LocalUsersChoice.eNotChosenYet:
				if (UserSystemUtils.LocalUserCount(ClientUserSystem.m_Users, false) > 1)
				{
					m_dialogBox = T17DialogBoxManager.GetDialog(false);
					if (null != m_dialogBox)
					{
						FastList<User> users = ClientUserSystem.m_Users;
						User.MachineID s_LocalMachineId = ClientUserSystem.s_LocalMachineId;
						User user2 = UserSystemUtils.FindUser(users, null, s_LocalMachineId, EngagementSlot.One);
						string message = Localization.Get("Text.Menu.MultiLocalUserInviteQuestion", new LocToken("[NAME]", (user2 == null) ? string.Empty : user2.DisplayName));
						m_dialogBox.Initialize("StartScreen.AreYouSure", message, "Text.Button.InvitedUserOnly", "Text.Button.AllLocalUsers", "Text.Button.Cancel", T17DialogBox.Symbols.Warning, true, false);
						T17DialogBox dialogBox = m_dialogBox;
						dialogBox.OnConfirm = (T17DialogBox.DialogEvent)Delegate.Combine(dialogBox.OnConfirm, new T17DialogBox.DialogEvent(OnInviteStartPrimaryUserOnly));
						T17DialogBox dialogBox2 = m_dialogBox;
						dialogBox2.OnDecline = (T17DialogBox.DialogEvent)Delegate.Combine(dialogBox2.OnDecline, new T17DialogBox.DialogEvent(OnInviteStartAllLocalUsers));
						T17DialogBox dialogBox3 = m_dialogBox;
						dialogBox3.OnCancel = (T17DialogBox.DialogEvent)Delegate.Combine(dialogBox3.OnCancel, new T17DialogBox.DialogEvent(OnInviteCancelled));
						m_dialogBox.Show();
					}
					else
					{
						OnInviteCancelled();
					}
				}
				else if (UserSystemUtils.AnySplitPadUsers() || UserSystemUtils.AnyRemoteUsers())
				{
					m_dialogBox = T17DialogBoxManager.GetDialog(false);
					if (null != m_dialogBox)
					{
						FastList<User> users = ClientUserSystem.m_Users;
						User.MachineID s_LocalMachineId = ClientUserSystem.s_LocalMachineId;
						User user3 = UserSystemUtils.FindUser(users, null, s_LocalMachineId, EngagementSlot.One);
						string message2 = Localization.Get("Text.Menu.InviteRestriction", new LocToken("[NAME]", (user3 == null) ? string.Empty : user3.DisplayName));
						m_dialogBox.Initialize("StartScreen.AreYouSure", message2, "Text.Button.Confirm", "Text.Button.Cancel", null, T17DialogBox.Symbols.Warning, true, false);
						T17DialogBox dialogBox4 = m_dialogBox;
						dialogBox4.OnConfirm = (T17DialogBox.DialogEvent)Delegate.Combine(dialogBox4.OnConfirm, new T17DialogBox.DialogEvent(OnInviteStartPrimaryUserOnly));
						T17DialogBox dialogBox5 = m_dialogBox;
						dialogBox5.OnDecline = (T17DialogBox.DialogEvent)Delegate.Combine(dialogBox5.OnDecline, new T17DialogBox.DialogEvent(OnInviteCancelled));
						m_dialogBox.Show();
					}
					else
					{
						OnInviteCancelled();
					}
				}
				else
				{
					OnInviteStartPrimaryUserOnly();
				}
				break;
			case AcceptInviteData.LocalUsersChoice.ePrimary:
				OnInviteStartPrimaryUserOnly();
				break;
			case AcceptInviteData.LocalUsersChoice.eAll:
				OnInviteStartAllLocalUsers();
				break;
			default:
				OnInviteCancelled();
				break;
			}
		}
		else if (m_InviteData.FromIIS)
		{
			InviteMonitor.ClearInvite();
		}
		else
		{
			m_InviteData.FromIIS = true;
			m_InviteData.User = null;
			if (InviteMonitor.InviteAccepted != null)
			{
				InviteMonitor.InviteAccepted();
			}
			ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Offline, null, LoadIISScreen);
		}
	}

	public void HandlePlayTogetherHost(OnlineMultiplayerSessionPlayTogetherHosting host)
	{
		m_PlayTogetherData = host;
		m_bBusy = true;
		m_dialogBox = null;
		InviteMonitor.ClearPlayTogetherHost();
		if (null == m_progressBox)
		{
			m_progressBox = T17DialogBoxManager.GetDialog(false);
			if (null != m_progressBox)
			{
				m_progressBox.Initialize("Text.PleaseWait", string.Empty, null, null, null, T17DialogBox.Symbols.Spinner);
				m_progressBox.Show();
			}
		}
		m_playTogetherHostingState = PlayTogetherHostingState.eGoingOffline;
		ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Offline, null, OnOfflineToPlayTogetherHost);
	}

	public bool IsBusy()
	{
		if (m_bBusy || null != m_dialogBox || null != m_progressBox)
		{
			return true;
		}
		return false;
	}

	public bool IsAwaitingUserInput()
	{
		return null != m_dialogBox;
	}

	private void OnOfflineToPlayTogetherHost(IConnectionModeSwitchStatus status)
	{
		if (m_playTogetherHostingState != PlayTogetherHostingState.eGoingOffline)
		{
			return;
		}
		if (status.GetResult() == eConnectionModeSwitchResult.Success)
		{
			m_playTogetherHostingState = PlayTogetherHostingState.eDisengageNonRequiredUsers;
			return;
		}
		m_bBusy = false;
		m_PlayTogetherData = null;
		m_playTogetherHostingState = PlayTogetherHostingState.eIdle;
		if (null != m_progressBox)
		{
			m_progressBox.Hide();
			m_progressBox = null;
		}
		NetworkErrorDialog.ShowDialog(status);
	}

	private void OnInviteStartPrimaryUserOnly()
	{
		StartJoin(false);
	}

	private void OnInviteStartAllLocalUsers()
	{
		StartJoin(true);
	}

	private void OnInviteCancelled()
	{
		InviteMonitor.ClearInvite();
		InviteMonitor.ClearPlayTogetherHost();
		m_InviteData = null;
		m_dialogBox = null;
		m_inviteProcessingState = InviteProcessingState.eIdle;
	}

	private void StartJoin(bool allowAllLocalUsers)
	{
		m_bBusy = true;
		m_dialogBox = null;
		InviteMonitor.ClearInvite();
		if (InviteMonitor.InviteAccepted != null)
		{
			InviteMonitor.InviteAccepted();
		}
		m_InviteRequiresAllActiveLocalUsers = allowAllLocalUsers;
		if (null == m_progressBox)
		{
			m_progressBox = T17DialogBoxManager.GetDialog(false);
			if (null != m_progressBox)
			{
				m_progressBox.Initialize("Text.PleaseWait", ConnectionModeSwitcher.GetStatus().GetLocalisedProgressDescription(), null, null, null, T17DialogBox.Symbols.Spinner, true, false);
				m_progressBox.Show();
			}
		}
		m_inviteProcessingState = InviteProcessingState.eGoingOffline;
		ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Offline, null, OnOfflineToJoinInvite);
	}

	private void OnOfflineToJoinInvite(IConnectionModeSwitchStatus status)
	{
		if (m_inviteProcessingState != InviteProcessingState.eGoingOffline)
		{
			return;
		}
		if (status.GetResult() == eConnectionModeSwitchResult.Success)
		{
			m_inviteProcessingState = InviteProcessingState.eDisengageNonRequiredUsers;
			return;
		}
		m_InviteData = null;
		m_bBusy = false;
		m_inviteProcessingState = InviteProcessingState.eIdle;
		if (null != m_progressBox)
		{
			m_progressBox.Hide();
			m_progressBox = null;
		}
		NetworkErrorDialog.ShowDialog(status);
	}

	private void OnAcceptInviteConnectionStateComplete(IConnectionModeSwitchStatus status)
	{
		m_inviteProcessingState = InviteProcessingState.eIdle;
		if (status.GetResult() == eConnectionModeSwitchResult.Success)
		{
			m_bBusy = false;
			if (null != m_progressBox)
			{
				m_progressBox.Hide();
				m_progressBox = null;
			}
			if (InviteMonitor.InviteJoinComplete != null)
			{
				InviteMonitor.InviteJoinComplete();
			}
			GameUtils.SendDiagnosticEvent("AcceptInvite:Success");
			return;
		}
		CompositeStatus compositeStatus = status as CompositeStatus;
		ConnectionModeStatus connectionModeStatus = null;
		if (compositeStatus != null)
		{
			connectionModeStatus = compositeStatus.m_TaskSubStatus as ConnectionModeStatus;
		}
		m_FailedStatus = status.Clone();
		if (connectionModeStatus != null)
		{
			switch (connectionModeStatus.m_Result.m_returnCode)
			{
			case OnlineMultiplayerConnectionModeConnectResult.eCancelledByUser:
				GameUtils.SendDiagnosticEvent("AcceptInvite:Failure:eCancelledByUser");
				break;
			case OnlineMultiplayerConnectionModeConnectResult.eGenericFailure:
				GameUtils.SendDiagnosticEvent("AcceptInvite:Failure:eGenericFailure");
				break;
			default:
				GameUtils.SendDiagnosticEvent("AcceptInvite:Failure:Unknown");
				break;
			}
		}
		else
		{
			GameUtils.SendDiagnosticEvent("AcceptInvite:Failure:NotSpecified");
		}
		if (connectionModeStatus != null && connectionModeStatus.m_Result.m_returnCode == OnlineMultiplayerConnectionModeConnectResult.eCancelledByUser)
		{
			ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Offline, null, JoinCancelled_OnOfflineComplete);
		}
		else
		{
			ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Offline, null, JoinFailed_OnOfflineComplete);
		}
	}

	private void OnPlayTogetherHostComplete(IConnectionModeSwitchStatus status)
	{
		m_playTogetherHostingState = PlayTogetherHostingState.eIdle;
		if (status.GetResult() == eConnectionModeSwitchResult.Success)
		{
			m_bBusy = false;
			if (null != m_progressBox)
			{
				m_progressBox.Hide();
				m_progressBox = null;
			}
		}
		else
		{
			m_FailedStatus = status.Clone();
			ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Offline, null, JoinFailed_OnOfflineComplete);
		}
	}

	private void JoinCancelled_OnOfflineComplete(IConnectionModeSwitchStatus status)
	{
		m_bBusy = false;
		if (m_progressBox != null)
		{
			m_progressBox.Hide();
			m_progressBox = null;
		}
	}

	private void JoinFailed_OnOfflineComplete(IConnectionModeSwitchStatus status)
	{
		JoinCancelled_OnOfflineComplete(status);
		ShowError(m_FailedStatus);
	}

	private void ShowError(IConnectionModeSwitchStatus status)
	{
		if (!status.DisplayPlatformDialog())
		{
			T17DialogBox dialog = T17DialogBoxManager.GetDialog(false);
			if (null != dialog)
			{
				dialog.Initialize("Text.Warning", status.GetLocalisedResultDescription(), "Text.Button.Confirm", null, null, T17DialogBox.Symbols.Warning, true, false);
				dialog.Show();
			}
		}
	}
}
