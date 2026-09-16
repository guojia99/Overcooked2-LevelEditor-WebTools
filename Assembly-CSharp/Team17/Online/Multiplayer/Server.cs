using System;
using System.Collections.Generic;
using BitStream;
using Team17.Online.Multiplayer.Connection;
using Team17.Online.Multiplayer.Messaging;

namespace Team17.Online.Multiplayer
{
	public class Server : PeerBase
	{
		private IOnlinePlatformManager m_OnlinePlatform;

		private IOnlineMultiplayerSessionCoordinator m_OnlineMultiplayerSessionCoordinator;

		private FastList<byte> m_WriteBuffer = new FastList<byte>(1024);

		private BitStreamWriter m_Writer;

		private Dictionary<IOnlineMultiplayerSessionUserId, NetworkConnection> m_RemoteClientConnections = new Dictionary<IOnlineMultiplayerSessionUserId, NetworkConnection>();

		private LocalLoopbackConnection m_LocalClientConnection;

		private FastList<NetworkConnection> m_AllConnections = new FastList<NetworkConnection>();

		private ServerUserSystem m_ServerUserSystem = new ServerUserSystem();

		private FastList<ConnectionStats> m_ConnectionStatsList = new FastList<ConnectionStats>(4);

		private List<JoinDataProvider.GameUserData> m_remoteGameUserDataStorageCache = new List<JoinDataProvider.GameUserData>(4);

		private bool m_bInitialised;

		private FastList<byte> m_ReplyData = new FastList<byte>((int)OnlineMultiplayerConfig.MaxTransportMessageSize);

		private UsersChangedMessage m_UsersChanged = new UsersChangedMessage();

		private TimeSyncMessage m_TimeSync = new TimeSyncMessage();

		private GameSetupMessage m_GameSetup = new GameSetupMessage();

		public void Initialise(bool bOnline)
		{
			if (bOnline)
			{
				m_OnlinePlatform = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
				m_OnlineMultiplayerSessionCoordinator = m_OnlinePlatform.OnlineMultiplayerSessionCoordinator();
				if (m_OnlineMultiplayerSessionCoordinator != null)
				{
					m_OnlineMultiplayerSessionCoordinator.RegisterDataReceivedCallback(OnlineMultiplayerSessionDataReceivedCallback);
				}
			}
			ServerUserSystem.OnUserRemoved = (GenericVoid<User>)Delegate.Combine(ServerUserSystem.OnUserRemoved, new GenericVoid<User>(OnUserRemoved));
			m_Writer = new BitStreamWriter(m_WriteBuffer);
			while (m_remoteGameUserDataStorageCache.Count != 4)
			{
				m_remoteGameUserDataStorageCache.Add(new JoinDataProvider.GameUserData());
			}
			m_bInitialised = true;
		}

		public void Reset()
		{
			for (int num = m_AllConnections.Count - 1; num >= 0; num--)
			{
				m_AllConnections._items[num].Disconnect();
			}
			m_RemoteClientConnections.Clear();
			m_AllConnections.Clear();
			m_LocalClientConnection = null;
			if (m_OnlineMultiplayerSessionCoordinator != null)
			{
				m_OnlineMultiplayerSessionCoordinator.UnRegisterDataReceivedCallback(OnlineMultiplayerSessionDataReceivedCallback);
			}
			ServerUserSystem.OnUserRemoved = (GenericVoid<User>)Delegate.Remove(ServerUserSystem.OnUserRemoved, new GenericVoid<User>(OnUserRemoved));
			m_bInitialised = false;
		}

		public override ConnectionStats GetConnectionStats(bool bReliable)
		{
			return m_LocalClientConnection.GetConnectionStats(bReliable);
		}

		public FastList<ConnectionStats> GetAllConnectionStats(bool bReliable)
		{
			int num = 0;
			for (int i = 0; i < m_AllConnections.Count; i++)
			{
				if (m_AllConnections._items[i] != m_LocalClientConnection)
				{
					m_ConnectionStatsList._items[num] = m_AllConnections._items[i].GetConnectionStats(bReliable);
					num++;
				}
			}
			return m_ConnectionStatsList;
		}

		public void Update()
		{
			if (m_bInitialised)
			{
				m_ServerUserSystem.Update();
			}
		}

		public override void Dispatch()
		{
			if (m_bInitialised)
			{
				for (int i = 0; i < m_AllConnections.Count; i++)
				{
					m_AllConnections._items[i].Dispatch();
				}
			}
		}

		public ServerUserSystem GetUserSystem()
		{
			return m_ServerUserSystem;
		}

		public void BroadcastMessageToAll(MessageType type, Serialisable message, bool bReliable = true)
		{
			for (int num = m_AllConnections.Count - 1; num >= 0; num--)
			{
				SendMessageToClient(m_AllConnections._items[num], type, message, bReliable);
			}
			if (m_Tracker != null)
			{
				m_Tracker.TrackSentGlobalEvent(type);
			}
		}

		public void SendMessageToClient(IOnlineMultiplayerSessionUserId sessionUser, MessageType type, Serialisable message, bool bReliable = true)
		{
			NetworkConnection value;
			if (sessionUser == null)
			{
				SendMessageToClient(m_LocalClientConnection, type, message, bReliable);
			}
			else if (m_RemoteClientConnections.TryGetValue(sessionUser, out value))
			{
				SendMessageToClient(value, type, message, bReliable);
			}
			if (m_Tracker != null)
			{
				m_Tracker.TrackSentGlobalEvent(type);
			}
		}

		public void EnsureLocalLoopbackClientConnection(LocalLoopbackConnection connection)
		{
			if (m_LocalClientConnection != null)
			{
				m_AllConnections.Remove(m_LocalClientConnection);
			}
			m_LocalClientConnection = connection;
			m_AllConnections.Add(connection);
		}

		public override void HandleConnectionLost(IOnlineMultiplayerSessionUserId sessionUserId, NetworkConnection connection)
		{
			RemoveConnection(sessionUserId, connection);
			User user = UserSystemUtils.FindUser(ServerUserSystem.m_Users, connection.GetRemoteSessionUserId());
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

		public override void HandleLocalLoopbackConnectionLost(NetworkConnection connection)
		{
			if (m_LocalClientConnection != null && m_LocalClientConnection == connection)
			{
				m_LocalClientConnection = null;
			}
		}

		public override void HandleDisconnectMessage(NetworkConnection connection)
		{
			RemoveConnection(connection.GetRemoteSessionUserId(), connection);
			User user = UserSystemUtils.FindUser(ServerUserSystem.m_Users, connection.GetRemoteSessionUserId());
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

		public override void SetLatencyTestPaused(bool paused)
		{
			for (int i = 0; i < m_AllConnections.Count; i++)
			{
				m_AllConnections._items[i].SetLatencyTestPaused(paused);
			}
		}

		public OnlineMultiplayerSessionJoinResult OnlineMultiplayerSessionJoinDecisionCallback(IOnlineMultiplayerSessionUserId primaryRemoteSessionUserId, List<OnlineMultiplayerSessionJoinRemoteUserData> remoteUserData, out byte[] replyData, out int replyDataSize)
		{
			OnlineMultiplayerSessionVisibility onlineMultiplayerSessionVisibility = OnlineMultiplayerSessionVisibility.eClosed;
			replyData = null;
			replyDataSize = 0;
			if (ConnectionModeSwitcher.GetRequestedConnectionState() == NetConnectionState.Server)
			{
				onlineMultiplayerSessionVisibility = ((ServerOptions)ConnectionModeSwitcher.GetAgentData()).visibility;
			}
			else if (ConnectionModeSwitcher.GetRequestedConnectionState() == NetConnectionState.Matchmake)
			{
				onlineMultiplayerSessionVisibility = OnlineMultiplayerSessionVisibility.eMatchmaking;
			}
			if (onlineMultiplayerSessionVisibility == OnlineMultiplayerSessionVisibility.eClosed)
			{
				return OnlineMultiplayerSessionJoinResult.eClosed;
			}
			if (ServerUserSystem.m_Users.Count == 4)
			{
				return OnlineMultiplayerSessionJoinResult.eFull;
			}
			if (ServerUserSystem.m_Users.Count + remoteUserData.Count > 4)
			{
				return OnlineMultiplayerSessionJoinResult.eNotEnoughRoomForAllLocalUsers;
			}
			if (remoteUserData.Count > m_remoteGameUserDataStorageCache.Count)
			{
				return OnlineMultiplayerSessionJoinResult.eNotEnoughRoomForAllLocalUsers;
			}
			BitStreamReader bitStreamReader = new BitStreamReader(remoteUserData[0].GameData);
			for (int i = 0; i < remoteUserData.Count; i++)
			{
				m_remoteGameUserDataStorageCache[i].Clear();
				bitStreamReader.Reset(remoteUserData[i].GameData);
				if (!m_remoteGameUserDataStorageCache[i].Deserialize(bitStreamReader))
				{
					for (int j = 0; j < m_remoteGameUserDataStorageCache.Count; j++)
					{
						m_remoteGameUserDataStorageCache[j].Clear();
					}
					return OnlineMultiplayerSessionJoinResult.eGenericFailure;
				}
			}
			if (m_remoteGameUserDataStorageCache[0].JoinMethod == NetConnectionState.AcceptInvite && onlineMultiplayerSessionVisibility != OnlineMultiplayerSessionVisibility.ePublic && onlineMultiplayerSessionVisibility != OnlineMultiplayerSessionVisibility.ePrivate)
			{
				return OnlineMultiplayerSessionJoinResult.eClosed;
			}
			if (m_remoteGameUserDataStorageCache[0].JoinMethod == NetConnectionState.Matchmake && onlineMultiplayerSessionVisibility != OnlineMultiplayerSessionVisibility.ePublic && onlineMultiplayerSessionVisibility != OnlineMultiplayerSessionVisibility.eMatchmaking)
			{
				return OnlineMultiplayerSessionJoinResult.eClosed;
			}
			User.MachineID availableMachineId = ServerUserSystem.GetAvailableMachineId();
			for (int k = 0; k < remoteUserData.Count; k++)
			{
				User user = m_ServerUserSystem.AddNewRemoteUser(availableMachineId, (k != 0) ? null : primaryRemoteSessionUserId, m_remoteGameUserDataStorageCache[k]);
				m_remoteGameUserDataStorageCache[k].Clear();
			}
			RemoteConnection remoteConnection = new RemoteConnection();
			remoteConnection.Initialise(this, m_OnlineMultiplayerSessionCoordinator, primaryRemoteSessionUserId);
			AddConnection(primaryRemoteSessionUserId, remoteConnection);
			m_ReplyData.Clear();
			BitStreamWriter bitStreamWriter = new BitStreamWriter(m_ReplyData);
			bitStreamWriter.Write((uint)availableMachineId, 3);
			m_TimeSync.Initialise(ClientTime.Time());
			m_TimeSync.Serialise(bitStreamWriter);
			m_UsersChanged.Initialise(ServerUserSystem.m_Users);
			m_UsersChanged.Serialise(bitStreamWriter);
			m_GameSetup.Initialise(ClientGameSetup.Mode);
			m_GameSetup.Serialise(bitStreamWriter);
			replyData = m_ReplyData._items;
			replyDataSize = m_ReplyData.Count;
			return OnlineMultiplayerSessionJoinResult.eSuccess;
		}

		public void OnlineMultiplayerSessionUserJoinedCallback(IOnlineMultiplayerSessionUserId primaryRemoteSessionUserId)
		{
			ServerMessenger.UsersChanged();
		}

		private void OnlineMultiplayerSessionDataReceivedCallback(IOnlineMultiplayerSessionUserId fromUserId, byte[] receivedData, int receivedDataSize)
		{
			if (m_RemoteClientConnections.ContainsKey(fromUserId))
			{
				m_RemoteClientConnections[fromUserId].HandleReceivedBytes(receivedData, receivedDataSize);
			}
		}

		private void SendMessageToClient(NetworkConnection connection, MessageType type, Serialisable message, bool bReliable)
		{
			m_WriteBuffer.Clear();
			m_Writer.Reset(m_WriteBuffer);
			m_CurrentMessage.Type = type;
			m_CurrentMessage.Payload = message;
			m_CurrentMessage.Serialise(m_Writer);
			if (!connection.SendMessage(m_WriteBuffer._items, m_WriteBuffer.Count, bReliable))
			{
				connection.Disconnect();
			}
		}

		private void AddConnection(IOnlineMultiplayerSessionUserId sessionUserId, NetworkConnection connection)
		{
			m_RemoteClientConnections.Add(sessionUserId, connection);
			m_AllConnections.Add(connection);
			m_ConnectionStatsList.Add(default(ConnectionStats));
		}

		private void RemoveConnection(IOnlineMultiplayerSessionUserId sessionUserId, NetworkConnection connection)
		{
			if (m_RemoteClientConnections.ContainsKey(sessionUserId))
			{
				m_RemoteClientConnections.Remove(sessionUserId);
				m_ConnectionStatsList.RemoveAt(m_ConnectionStatsList.Count - 1);
			}
			m_AllConnections.Remove(connection);
		}

		private void OnUserRemoved(User user)
		{
			IOnlineMultiplayerSessionUserId sessionId = user.SessionId;
			if (sessionId != null && m_RemoteClientConnections.ContainsKey(sessionId))
			{
				NetworkConnection networkConnection = m_RemoteClientConnections[sessionId];
				networkConnection.Disconnect();
				RemoveConnection(sessionId, networkConnection);
			}
		}
	}
}
