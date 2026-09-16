using System;
using System.Collections.Generic;
using Steamworks;

namespace Team17.Online
{
	public class SteamOnlineMultiplayerSessionTransportCoordinator
	{
		public delegate void DisconnectionCallback(CSteamID fromSteamId);

		public delegate void DataCallback(CSteamID fromSteamId, byte[] data, int dataSizeInBytes);

		private enum Status : byte
		{
			eIdle = 0,
			eOpen = 1
		}

		private enum Channels : byte
		{
			eData = 1,
			eVoip = 2
		}

		private static Callback<P2PSessionRequest_t> s_steamTransportConnectionRequest;

		private static Callback<P2PSessionConnectFail_t> s_steamTransportConnectionFail;

		private bool m_isInitialized;

		private Status m_status;

		private DisconnectionCallback m_disconnectionCallback;

		private DataCallback m_dataCallback;

		private DataCallback m_voipDataCallback;

		private CSteamID m_lobbyId = default(CSteamID);

		private List<CSteamID> m_connectionIds = new List<CSteamID>();

		private CSteamID receiveFromSteamId = default(CSteamID);

		public static uint DisconnectionTimeInSeconds
		{
			get
			{
				return 5u;
			}
		}

		public void Initialize()
		{
			if (!m_isInitialized)
			{
				s_steamTransportConnectionFail = Callback<P2PSessionConnectFail_t>.Create(OnSteamConnectionFail);
				s_steamTransportConnectionRequest = Callback<P2PSessionRequest_t>.Create(OnSteamConnectionRequest);
				m_isInitialized = true;
			}
		}

		public bool Open(CSteamID lobbyId, DisconnectionCallback disconnectionCallback, DataCallback dataCallback, DataCallback voipDataCallback)
		{
			if (m_isInitialized && m_status == Status.eIdle)
			{
				if (lobbyId.IsValid() && lobbyId.IsLobby() && disconnectionCallback != null && dataCallback != null && voipDataCallback != null)
				{
					try
					{
						if (SteamPlayerManager.Initialized)
						{
							m_disconnectionCallback = disconnectionCallback;
							m_dataCallback = dataCallback;
							m_voipDataCallback = voipDataCallback;
							m_lobbyId = lobbyId;
							m_status = Status.eOpen;
							return true;
						}
					}
					catch (Exception)
					{
					}
				}
				Close();
			}
			return false;
		}

		public void Close()
		{
			if (m_status == Status.eOpen)
			{
				try
				{
					if (SteamPlayerManager.Initialized)
					{
						for (int i = 0; i < m_connectionIds.Count; i++)
						{
							SteamNetworking.CloseP2PSessionWithUser(m_connectionIds[i]);
						}
					}
				}
				catch (Exception)
				{
				}
			}
			m_disconnectionCallback = null;
			m_dataCallback = null;
			m_voipDataCallback = null;
			m_lobbyId.Clear();
			m_connectionIds.Clear();
			m_status = Status.eIdle;
		}

		public void CloseConnection(CSteamID steamId)
		{
			if (m_status == Status.eOpen && steamId.IsValid() && !steamId.IsLobby())
			{
				if (m_connectionIds.Exists((CSteamID x) => x == steamId))
				{
					m_connectionIds.Remove(steamId);
				}
				try
				{
					SteamNetworking.CloseP2PSessionWithUser(steamId);
				}
				catch (Exception)
				{
				}
			}
		}

		public bool SendData(CSteamID toSteamId, byte[] data, int dataSize, bool sendReliably)
		{
			if (m_status == Status.eOpen && toSteamId.IsValid() && !toSteamId.IsLobby())
			{
				try
				{
					EP2PSend eP2PSendType = (sendReliably ? EP2PSend.k_EP2PSendReliable : EP2PSend.k_EP2PSendUnreliable);
					return SteamNetworking.SendP2PPacket(toSteamId, data, (uint)dataSize, eP2PSendType, 1);
				}
				catch (Exception)
				{
				}
			}
			return false;
		}

		public bool SendVoipData(CSteamID toSteamId, byte[] data, int dataSize)
		{
			if (m_status == Status.eOpen && toSteamId.IsValid() && !toSteamId.IsLobby())
			{
				try
				{
					return SteamNetworking.SendP2PPacket(toSteamId, data, (uint)dataSize, EP2PSend.k_EP2PSendUnreliableNoDelay, 2);
				}
				catch (Exception)
				{
				}
			}
			return false;
		}

		public void Update(byte[] receiveBuffer, uint maxIterations)
		{
			uint num = 0u;
			bool flag = false;
			do
			{
				flag = true;
				try
				{
					uint pcubMsgSize;
					if (receiveBuffer == null || num++ >= maxIterations || !SteamNetworking.IsP2PPacketAvailable(out pcubMsgSize, 1))
					{
						continue;
					}
					receiveFromSteamId.Clear();
					if (pcubMsgSize == 0 || pcubMsgSize > receiveBuffer.Length)
					{
						continue;
					}
					uint pcubMsgSize2;
					if (SteamNetworking.ReadP2PPacket(receiveBuffer, (uint)receiveBuffer.Length, out pcubMsgSize2, out receiveFromSteamId, 1))
					{
						if (pcubMsgSize2 != 0)
						{
							if (m_status == Status.eOpen && receiveFromSteamId.IsValid())
							{
								OnDataEvent(receiveFromSteamId, receiveBuffer, (int)pcubMsgSize2);
							}
							flag = false;
						}
					}
					else if (SteamNetworking.ReadP2PPacket(receiveBuffer, (uint)receiveBuffer.Length, out pcubMsgSize2, out receiveFromSteamId, 2) && pcubMsgSize2 != 0)
					{
						if (m_status == Status.eOpen && receiveFromSteamId.IsValid())
						{
							OnVoipDataEvent(receiveFromSteamId, receiveBuffer, (int)pcubMsgSize2);
						}
						flag = false;
					}
				}
				catch (Exception)
				{
				}
			}
			while (!flag);
		}

		private void OnDisconnectEvent(CSteamID fromSteamId)
		{
			if (m_disconnectionCallback != null)
			{
				try
				{
					m_disconnectionCallback(fromSteamId);
				}
				catch (Exception)
				{
				}
				if (m_connectionIds.Exists((CSteamID x) => x == fromSteamId))
				{
					m_connectionIds.Remove(fromSteamId);
				}
			}
		}

		private void OnDataEvent(CSteamID fromSteamId, byte[] data, int dataSizeInBytes)
		{
			if (m_dataCallback != null)
			{
				try
				{
					m_dataCallback(fromSteamId, data, dataSizeInBytes);
				}
				catch (Exception)
				{
				}
			}
		}

		private void OnVoipDataEvent(CSteamID fromSteamId, byte[] data, int dataSizeInBytes)
		{
			if (m_voipDataCallback != null)
			{
				try
				{
					m_voipDataCallback(fromSteamId, data, dataSizeInBytes);
				}
				catch (Exception)
				{
				}
			}
		}

		private void OnSteamConnectionRequest(P2PSessionRequest_t param)
		{
			try
			{
				if (m_status != Status.eOpen)
				{
					return;
				}
				int numLobbyMembers = SteamMatchmaking.GetNumLobbyMembers(m_lobbyId);
				for (int i = 0; i < numLobbyMembers; i++)
				{
					if (param.m_steamIDRemote == SteamMatchmaking.GetLobbyMemberByIndex(m_lobbyId, i))
					{
						SteamNetworking.AcceptP2PSessionWithUser(param.m_steamIDRemote);
						if (!m_connectionIds.Exists((CSteamID x) => x == param.m_steamIDRemote))
						{
							m_connectionIds.Add(param.m_steamIDRemote);
						}
						break;
					}
				}
			}
			catch (Exception)
			{
			}
		}

		private void OnSteamConnectionFail(P2PSessionConnectFail_t param)
		{
			try
			{
				if (m_status == Status.eOpen)
				{
					OnDisconnectEvent(param.m_steamIDRemote);
				}
			}
			catch (Exception)
			{
			}
		}
	}
}
