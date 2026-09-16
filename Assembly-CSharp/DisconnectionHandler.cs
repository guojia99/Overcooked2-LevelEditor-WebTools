using System;
using System.Collections.Generic;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;

internal class DisconnectionHandler
{
	private static IOnlineMultiplayerSessionCoordinator m_iOnlineMultiplayerSessionCoordinator;

	private IOnlineMultiplayerConnectionModeCoordinator m_connectionModeCoordinator;

	private GenericVoid m_OnUsersChanged;

	private bool m_bAnyLocalUsers;

	public static GenericVoid SessionConnectionLostEvent;

	public static GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult>> ConnectionModeErrorEvent;

	public static GenericVoid KickedFromSessionEvent;

	public static GenericVoid<OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult>> LocalDisconnectionEvent;

	private static OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult> m_LastConnectionModeError;

	private static OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult> m_LastSessionConnectionLostError;

	private static OnlineMultiplayerConnectionMode m_requestedOfflineMode;

	public static GenericVoid<EntitySerialisationEntry> OnChefBeingDeleted;

	public void Initialise()
	{
		IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
		m_iOnlineMultiplayerSessionCoordinator = onlinePlatformManager.OnlineMultiplayerSessionCoordinator();
		m_connectionModeCoordinator = onlinePlatformManager.OnlineMultiplayerConnectionModeCoordinator();
		if (m_iOnlineMultiplayerSessionCoordinator != null)
		{
			m_iOnlineMultiplayerSessionCoordinator.RegisterDisconnectionCallback(HandleLocalDisconnection);
			m_iOnlineMultiplayerSessionCoordinator.RegisterRemoteUserDisconnectionCallback(HandleRemoteDisconnection);
		}
		if (m_connectionModeCoordinator != null)
		{
			m_connectionModeCoordinator.RegisterErrorCallback(OnlineMultiplayerConnectionModeErrorCallback);
		}
		m_OnUsersChanged = OnClientUsersChanged;
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Combine(ClientUserSystem.usersChanged, m_OnUsersChanged);
		ServerUserSystem.OnUserRemoved = (GenericVoid<User>)Delegate.Combine(ServerUserSystem.OnUserRemoved, new GenericVoid<User>(OnServerUserRemoved));
	}

	public void Shutdown()
	{
		if (m_iOnlineMultiplayerSessionCoordinator != null)
		{
			m_iOnlineMultiplayerSessionCoordinator.UnRegisterDisconnectionCallback(HandleLocalDisconnection);
			m_iOnlineMultiplayerSessionCoordinator.UnRegisterRemoteUserDisconnectionCallback(HandleRemoteDisconnection);
		}
		if (m_connectionModeCoordinator != null)
		{
			m_connectionModeCoordinator.UnRegisterErrorCallback(OnlineMultiplayerConnectionModeErrorCallback);
		}
		m_iOnlineMultiplayerSessionCoordinator = null;
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Remove(ClientUserSystem.usersChanged, m_OnUsersChanged);
		ServerUserSystem.OnUserRemoved = (GenericVoid<User>)Delegate.Remove(ServerUserSystem.OnUserRemoved, new GenericVoid<User>(OnServerUserRemoved));
	}

	public void HandleSessionConnectionLost()
	{
		if (ConnectionModeSwitcher.GetRequestedConnectionState() != NetConnectionState.Offline && ConnectionModeSwitcher.GetStatus().GetProgress() == eConnectionModeSwitchProgress.Complete)
		{
			m_requestedOfflineMode = ConnectionStatus.CurrentConnectionMode();
			GoOffline(FireSessionConnectionLostEvent, m_requestedOfflineMode);
		}
	}

	private void FireSessionConnectionLostEvent(IConnectionModeSwitchStatus result)
	{
		if (result.GetResult() != eConnectionModeSwitchResult.Success && (!ConnectionStatus.HasConnectionModes() || (ConnectionStatus.HasConnectionModes() && m_requestedOfflineMode != OnlineMultiplayerConnectionMode.eNone)))
		{
			m_requestedOfflineMode = OnlineMultiplayerConnectionMode.eNone;
			GoOffline(FireSessionConnectionLostEvent, m_requestedOfflineMode);
		}
		else if (SessionConnectionLostEvent != null)
		{
			SessionConnectionLostEvent();
		}
	}

	public void OnlineMultiplayerConnectionModeErrorCallback(OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult> result)
	{
		if (ConnectionModeSwitcher.GetRequestedConnectionState() != NetConnectionState.Offline)
		{
			m_LastConnectionModeError = result;
			GoOffline(FireConnectionModeErrorEvent, OnlineMultiplayerConnectionMode.eNone);
		}
	}

	private void FireConnectionModeErrorEvent(IConnectionModeSwitchStatus result)
	{
		if (ConnectionModeErrorEvent != null)
		{
			ConnectionModeErrorEvent(m_LastConnectionModeError);
		}
	}

	public void HandleLocalDisconnection(OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult> result)
	{
		if (ConnectionModeSwitcher.GetRequestedConnectionState() != NetConnectionState.Offline)
		{
			m_LastSessionConnectionLostError = result;
			m_requestedOfflineMode = ConnectionStatus.CurrentConnectionMode();
			GoOffline(FireLocalDisconnectionEvent, m_requestedOfflineMode);
		}
	}

	private void FireLocalDisconnectionEvent(IConnectionModeSwitchStatus result)
	{
		if (result.GetResult() != eConnectionModeSwitchResult.Success && (!ConnectionStatus.HasConnectionModes() || (ConnectionStatus.HasConnectionModes() && m_requestedOfflineMode != OnlineMultiplayerConnectionMode.eNone)))
		{
			m_requestedOfflineMode = OnlineMultiplayerConnectionMode.eNone;
			GoOffline(FireLocalDisconnectionEvent, m_requestedOfflineMode);
		}
		else if (LocalDisconnectionEvent != null)
		{
			LocalDisconnectionEvent(m_LastSessionConnectionLostError);
		}
	}

	private static void GoOffline(GenericVoid<IConnectionModeSwitchStatus> callback, OnlineMultiplayerConnectionMode offlineMode)
	{
		UserSystemUtils.ResetUsersToOffline(ref ServerUserSystem.m_Users);
		UserSystemUtils.ResetUsersToOffline(ref ClientUserSystem.m_Users);
		ServerUserSystem.s_LocalMachineId = User.MachineID.One;
		ClientUserSystem.s_LocalMachineId = User.MachineID.One;
		IPlayerManager playerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
		OfflineOptions offlineOptions = new OfflineOptions
		{
			hostUser = playerManager.GetUser(EngagementSlot.One),
			eAdditionalAction = OfflineOptions.AdditionalAction.None,
			connectionMode = offlineMode
		};
		ConnectionModeSwitcher.RequestConnectionState(NetConnectionState.Offline, offlineOptions, callback);
	}

	public void HandleRemoteDisconnection(IOnlineMultiplayerSessionUserId leavingUserId)
	{
		if (ConnectionModeSwitcher.GetRequestedConnectionState() == NetConnectionState.Offline)
		{
			return;
		}
		User user = UserSystemUtils.FindUser(ServerUserSystem.m_Users, leavingUserId);
		if (user == null)
		{
			return;
		}
		FastList<User> users = ServerUserSystem.m_Users;
		User.MachineID machine = user.Machine;
		User[] array = UserSystemUtils.FindUsers(users, null, machine);
		if (array != null && array.Length > 0)
		{
			for (int i = 0; i < array.Length; i++)
			{
				ServerUserSystem.RemoveUser(array[i], i == array.Length - 1);
			}
		}
	}

	public static void HandleKickMessage()
	{
		m_requestedOfflineMode = ConnectionStatus.CurrentConnectionMode();
		GoOffline(FireKickedFromSessionEvent, m_requestedOfflineMode);
	}

	private static void FireKickedFromSessionEvent(IConnectionModeSwitchStatus result)
	{
		if (result.GetResult() != eConnectionModeSwitchResult.Success && (!ConnectionStatus.HasConnectionModes() || (ConnectionStatus.HasConnectionModes() && m_requestedOfflineMode != OnlineMultiplayerConnectionMode.eNone)))
		{
			m_requestedOfflineMode = OnlineMultiplayerConnectionMode.eNone;
			GoOffline(FireKickedFromSessionEvent, m_requestedOfflineMode);
		}
		else if (GameUtils.RequireManager<PlayerManager>().HasPlayer() && KickedFromSessionEvent != null)
		{
			KickedFromSessionEvent();
		}
	}

	public void OnServerUserRemoved(User removedUser)
	{
		DeleteChef(removedUser.EntityID);
		DeleteChef(removedUser.Entity2ID);
	}

	private void DeleteChef(uint uEntityID)
	{
		EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(uEntityID);
		if (entry == null || !(null != entry.m_GameObject))
		{
			return;
		}
		OnChefBeingDeleted(entry);
		IPlayerCarrier playerCarrier = entry.m_GameObject.RequestInterface<IPlayerCarrier>();
		if (playerCarrier != null)
		{
			for (int i = 0; i < 2; i++)
			{
				if (playerCarrier.InspectCarriedItem((PlayerAttachTarget)i) != null)
				{
					playerCarrier.TakeItem((PlayerAttachTarget)i);
				}
			}
		}
		ServerMessenger.DestroyChef(entry.m_GameObject);
	}

	public void OnClientUsersChanged()
	{
		bool flag = false;
		for (int i = 0; i < ClientUserSystem.m_Users.Count; i++)
		{
			if (ClientUserSystem.m_Users._items[i].IsLocal)
			{
				flag = true;
				break;
			}
		}
		if (m_bAnyLocalUsers && !flag && ConnectionModeSwitcher.GetRequestedConnectionState() != NetConnectionState.Offline)
		{
			HandleKickMessage();
		}
		m_bAnyLocalUsers = flag;
	}
}
