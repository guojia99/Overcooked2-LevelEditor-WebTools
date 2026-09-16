using System;
using System.Collections.Generic;
using Team17.Online;

public class GameplayInviteHandler : InviteHandler
{
	private AcceptInviteData m_Invite;

	private T17DialogBox m_dialogBox;

	private IPlayerManager m_playerManager;

	private bool m_busy;

	public void Start()
	{
		m_playerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
		if (InviteMonitor.GetAcceptedInvite() != null)
		{
			HandleAcceptedInvite(InviteMonitor.GetAcceptedInvite());
		}
	}

	public void Stop()
	{
		if (null != m_dialogBox && m_dialogBox.IsActive)
		{
			m_dialogBox.Hide();
			m_Invite = null;
		}
		m_dialogBox = null;
		m_busy = false;
	}

	public void Update()
	{
	}

	public void HandleAcceptedInvite(AcceptInviteData invite)
	{
		if (!(m_dialogBox == null))
		{
			return;
		}
		m_Invite = invite;
		m_busy = true;
		GamepadUser user = m_playerManager.GetUser(EngagementSlot.One);
		if (invite.Invite.WasAcceptedBy(user))
		{
			invite.User = user;
			if (UserSystemUtils.LocalUserCount(ClientUserSystem.m_Users, false) > 1)
			{
				m_dialogBox = T17DialogBoxManager.GetDialog(false);
				if (m_dialogBox != null)
				{
					FastList<User> users = ClientUserSystem.m_Users;
					User.MachineID s_LocalMachineId = ClientUserSystem.s_LocalMachineId;
					User user2 = UserSystemUtils.FindUser(users, null, s_LocalMachineId, EngagementSlot.One);
					string message = Localization.Get("Text.Menu.MultiLocalUserInviteQuestion", new LocToken("[NAME]", (user2 == null) ? string.Empty : user2.DisplayName));
					m_dialogBox.Initialize("StartScreen.AreYouSure", message, "Text.Button.InvitedUserOnly", "Text.Button.AllLocalUsers", "Text.Button.Cancel", T17DialogBox.Symbols.Warning, true, false);
					T17DialogBox dialogBox = m_dialogBox;
					dialogBox.OnConfirm = (T17DialogBox.DialogEvent)Delegate.Combine(dialogBox.OnConfirm, new T17DialogBox.DialogEvent(OnConfirmedPrimaryUserOnly));
					T17DialogBox dialogBox2 = m_dialogBox;
					dialogBox2.OnDecline = (T17DialogBox.DialogEvent)Delegate.Combine(dialogBox2.OnDecline, new T17DialogBox.DialogEvent(OnConfirmedAllLocalUsers));
					T17DialogBox dialogBox3 = m_dialogBox;
					dialogBox3.OnCancel = (T17DialogBox.DialogEvent)Delegate.Combine(dialogBox3.OnCancel, new T17DialogBox.DialogEvent(OnDeclined));
					m_dialogBox.Show();
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
					dialogBox4.OnConfirm = (T17DialogBox.DialogEvent)Delegate.Combine(dialogBox4.OnConfirm, new T17DialogBox.DialogEvent(OnConfirmedPrimaryUserOnly));
					T17DialogBox dialogBox5 = m_dialogBox;
					dialogBox5.OnDecline = (T17DialogBox.DialogEvent)Delegate.Combine(dialogBox5.OnDecline, new T17DialogBox.DialogEvent(OnDeclined));
					m_dialogBox.Show();
				}
				else
				{
					OnDeclined();
				}
			}
			else
			{
				OnConfirmedPrimaryUserOnly();
			}
		}
		else
		{
			OnConfirmed();
		}
	}

	public void HandlePlayTogetherHost(OnlineMultiplayerSessionPlayTogetherHosting host)
	{
		ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Offline, null, OnOfflineComplete);
	}

	public bool IsBusy()
	{
		return m_busy;
	}

	public bool IsAwaitingUserInput()
	{
		return null != m_dialogBox;
	}

	private void OnOfflineComplete(IConnectionModeSwitchStatus status)
	{
		m_dialogBox = null;
		ServerUserSystem.UnlockEngagement();
		if (m_playerManager == null)
		{
			m_playerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
		}
		if (m_Invite != null && !m_Invite.Invite.WasAcceptedBy(m_playerManager.GetUser(EngagementSlot.One)))
		{
			UserSystemUtils.RemoveAllSplitPadGuestUsers();
			for (int i = 0; i < 4; i++)
			{
				GamepadUser user = m_playerManager.GetUser((EngagementSlot)i);
				if (null != user)
				{
					m_playerManager.DisengagePad((EngagementSlot)i);
				}
			}
			m_Invite.FromIIS = true;
		}
		if (m_Invite == null || !m_Invite.FromIIS)
		{
			ServerGameSetup.Mode = GameMode.OnlineKitchen;
			ServerMessenger.LoadLevel("StartScreen", GameState.MainMenu, true);
		}
		m_busy = false;
	}

	private void OnConfirmedPrimaryUserOnly()
	{
		if (m_busy)
		{
			m_Invite.JoinLocalUsersChoice = AcceptInviteData.LocalUsersChoice.ePrimary;
			OnConfirmed();
		}
	}

	private void OnConfirmedAllLocalUsers()
	{
		if (m_busy)
		{
			m_Invite.JoinLocalUsersChoice = AcceptInviteData.LocalUsersChoice.eAll;
			OnConfirmed();
		}
	}

	private void OnConfirmed()
	{
		if (m_busy)
		{
			m_dialogBox = null;
			ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Offline, null, OnOfflineComplete);
		}
	}

	private void OnDeclined()
	{
		InviteMonitor.ClearInvite();
		m_Invite = null;
		m_dialogBox = null;
		m_busy = false;
	}
}
