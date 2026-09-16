using System;
using System.Collections.Generic;
using Team17.Online;

public class ServerDropInUserMonitor
{
	private enum DropInStatus
	{
		Idle = 0,
		Triggered = 1,
		Busy = 2
	}

	private GenericVoid<IConnectionModeSwitchStatus> OnEngagementPrivilegeCheckStarted;

	private GenericVoid<IConnectionModeSwitchStatus> OnEngagementPrivilegeCheckCompleted;

	private CheckPrivilegesAndDropInAllLocalUsersTask m_Checker = new CheckPrivilegesAndDropInAllLocalUsersTask();

	private IPlayerManager m_PlayerManager;

	private IOnlineMultiplayerSessionCoordinator m_iOnlineMultiplayerSessionCoordinator;

	private DropInStatus m_DropInStatus;

	public void Initialise(GenericVoid<IConnectionModeSwitchStatus> onStarted, GenericVoid<IConnectionModeSwitchStatus> onCompleted)
	{
		m_Checker.Initialise(OnUserCheckCompleted);
		m_PlayerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
		IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
		m_iOnlineMultiplayerSessionCoordinator = onlinePlatformManager.OnlineMultiplayerSessionCoordinator();
		OnEngagementPrivilegeCheckStarted = onStarted;
		OnEngagementPrivilegeCheckCompleted = onCompleted;
		ServerUserSystem.OnUserRemoved = (GenericVoid<User>)Delegate.Combine(ServerUserSystem.OnUserRemoved, new GenericVoid<User>(OnUserRemoved));
	}

	public void Shutdown()
	{
		ServerUserSystem.OnUserRemoved = (GenericVoid<User>)Delegate.Remove(ServerUserSystem.OnUserRemoved, new GenericVoid<User>(OnUserRemoved));
	}

	public IConnectionModeSwitchStatus GetStatus()
	{
		return m_Checker.GetStatus();
	}

	public void Update()
	{
		if (ConnectionModeSwitcher.GetStatus().GetProgress() == eConnectionModeSwitchProgress.Complete && ConnectionModeSwitcher.GetStatus().GetResult() == eConnectionModeSwitchResult.Success)
		{
			m_Checker.Update();
			switch (m_DropInStatus)
			{
			case DropInStatus.Triggered:
				UpdateTriggered();
				break;
			case DropInStatus.Busy:
				UpdateBusy();
				break;
			}
		}
	}

	public void TriggerDropIn()
	{
		if (m_DropInStatus == DropInStatus.Idle && ConnectionModeSwitcher.GetRequestedConnectionState() != NetConnectionState.Offline && ConnectionStatus.IsHost())
		{
			m_DropInStatus = DropInStatus.Triggered;
		}
	}

	private void SetDropInStatus(DropInStatus status)
	{
		m_DropInStatus = status;
	}

	private void UpdateTriggered()
	{
		if (m_DropInStatus == DropInStatus.Busy || m_Checker.GetStatus().GetProgress() == eConnectionModeSwitchProgress.InProgress)
		{
			return;
		}
		m_Checker.Start(null);
		switch (m_Checker.GetStatus().GetProgress())
		{
		case eConnectionModeSwitchProgress.InProgress:
			if (OnEngagementPrivilegeCheckStarted != null)
			{
				OnEngagementPrivilegeCheckStarted(GetStatus());
			}
			SetDropInStatus(DropInStatus.Busy);
			break;
		case eConnectionModeSwitchProgress.Complete:
			SetDropInStatus(DropInStatus.Idle);
			break;
		}
	}

	private void UpdateBusy()
	{
		if (m_Checker.GetStatus().GetProgress() == eConnectionModeSwitchProgress.Complete)
		{
			if (OnEngagementPrivilegeCheckCompleted != null)
			{
				OnEngagementPrivilegeCheckCompleted(GetStatus());
			}
			SetDropInStatus(DropInStatus.Idle);
		}
	}

	private void OnUserCheckCompleted(GamepadUser user, EngagementSlot slot, IConnectionModeSwitchStatus status)
	{
		if (status.GetResult() == eConnectionModeSwitchResult.Success)
		{
			FastList<User> users = ServerUserSystem.m_Users;
			User.MachineID s_LocalMachineId = ServerUserSystem.s_LocalMachineId;
			User user2 = UserSystemUtils.FindUser(users, null, s_LocalMachineId, slot);
			if (user2 == null)
			{
				bool bLocal = true;
				s_LocalMachineId = ServerUserSystem.s_LocalMachineId;
				ServerUserSystem.AddUser(bLocal, s_LocalMachineId, null, 0u, 0u, slot, PadSide.Both, TeamID.None, User.PartyPersistance.Remain);
			}
		}
		else
		{
			m_PlayerManager.DisengagePad(slot);
			PrivilegeCheckCache.RemoveAllowedUser(user);
			TriggerDropIn();
		}
	}

	private void OnUserRemoved(User user)
	{
		if (user.IsLocal)
		{
			OnlineMultiplayerLocalUserId allowedUser = PrivilegeCheckCache.GetAllowedUser(user.GamepadUser);
			if (LocalDroppedInUserCache.HasBeenDroppedIn(allowedUser))
			{
				m_iOnlineMultiplayerSessionCoordinator.RemoveNonPrimaryLocalUser(allowedUser);
			}
			LocalDroppedInUserCache.RemoveDroppedInUser(allowedUser);
			PrivilegeCheckCache.RemoveAllowedUser(user.GamepadUser);
		}
	}
}
