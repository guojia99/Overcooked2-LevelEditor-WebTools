using System.Collections.Generic;
using BitStream;
using Team17.Online.Multiplayer.Connection;
using Team17.Online.Multiplayer.Messaging;

namespace Team17.Online.Multiplayer
{
	public class Client : PeerBase
	{
		private FastList<byte> m_WriteBuffer = new FastList<byte>(1024);

		private ClientUserSystem m_ClientUserSystem = new ClientUserSystem();

		private BitStreamWriter m_Writer;

		private NetworkConnection m_ServerConnection;

		private IOnlineMultiplayerSessionCoordinator m_iOnlineMultiplayerSessionCoordinator;

		private ClientTime m_Time = new ClientTime();

		public void Initialise(bool bOnline)
		{
			if (bOnline)
			{
				IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
				m_iOnlineMultiplayerSessionCoordinator = onlinePlatformManager.OnlineMultiplayerSessionCoordinator();
				if (m_iOnlineMultiplayerSessionCoordinator != null)
				{
					m_iOnlineMultiplayerSessionCoordinator.RegisterDataReceivedCallback(OnlineMultiplayerSessionDataReceivedCallback);
				}
			}
			m_ClientUserSystem.Initialise();
			m_Writer = new BitStreamWriter(m_WriteBuffer);
			m_Time.Initialise();
		}

		public void Reset()
		{
			if (m_ServerConnection != null)
			{
				m_ServerConnection.Disconnect();
				m_ServerConnection = null;
			}
			if (m_iOnlineMultiplayerSessionCoordinator != null)
			{
				m_iOnlineMultiplayerSessionCoordinator.UnRegisterDataReceivedCallback(OnlineMultiplayerSessionDataReceivedCallback);
			}
			m_Time.Shutdown();
		}

		public ClientUserSystem GetUserSystem()
		{
			return m_ClientUserSystem;
		}

		public override ConnectionStats GetConnectionStats(bool bReliable)
		{
			if (m_ServerConnection != null)
			{
				return m_ServerConnection.GetConnectionStats(bReliable);
			}
			return default(ConnectionStats);
		}

		public void SendMessageToServer(MessageType type, Serialisable message, bool bReliable = true)
		{
			if (m_ServerConnection != null)
			{
				m_WriteBuffer.Clear();
				m_Writer.Reset(m_WriteBuffer);
				m_CurrentMessage.Type = type;
				m_CurrentMessage.Payload = message;
				m_CurrentMessage.Serialise(m_Writer);
				if (m_Tracker != null)
				{
					m_Tracker.TrackSentGlobalEvent(type);
				}
				if (!m_ServerConnection.SendMessage(m_WriteBuffer._items, m_WriteBuffer.Count, bReliable))
				{
					m_ServerConnection.Disconnect();
				}
			}
		}

		public void Update()
		{
			ClientTime.Update();
		}

		public override void Dispatch()
		{
			if (m_ServerConnection != null)
			{
				m_ServerConnection.Dispatch();
			}
		}

		public void HandleOutgoingServerConnectionAccepted(NetworkConnection serverConnection)
		{
			m_ServerConnection = serverConnection;
		}

		public void OnlineMultiplayerSessionDataReceivedCallback(IOnlineMultiplayerSessionUserId fromUserId, byte[] receivedData, int receivedDataSize)
		{
			m_ServerConnection.HandleReceivedBytes(receivedData, receivedDataSize);
		}

		public override void HandleConnectionLost(IOnlineMultiplayerSessionUserId sessionUserId, NetworkConnection connection)
		{
			m_ServerConnection = null;
			LeaveSession(m_iOnlineMultiplayerSessionCoordinator);
		}

		public override void HandleLocalLoopbackConnectionLost(NetworkConnection connection)
		{
			m_ServerConnection = null;
		}

		public override void HandleDisconnectMessage(NetworkConnection connection)
		{
			DisconnectionHandler.HandleKickMessage();
		}

		public override void SetLatencyTestPaused(bool paused)
		{
			if (m_ServerConnection != null)
			{
				m_ServerConnection.SetLatencyTestPaused(paused);
			}
		}
	}
}
