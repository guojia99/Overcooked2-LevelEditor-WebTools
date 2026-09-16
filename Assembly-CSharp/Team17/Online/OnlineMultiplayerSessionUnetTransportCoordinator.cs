using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using UnityEngine.Networking;

namespace Team17.Online
{
	public class OnlineMultiplayerSessionUnetTransportCoordinator
	{
		public delegate void ConnectionCallback(int connectionId);

		public delegate void DisconnectionCallback(int connectionId);

		public delegate void DataCallback(int connectionId, byte[] data, int dataSiszeInBytes);

		private enum Status
		{
			eIdle = 0,
			eOpen = 1,
			eClosing = 2
		}

		public static readonly int s_InvalidConnectionId;

		private readonly long m_shutdownDelayTimeInMilliseconds = 1000L;

		private readonly ushort m_unetPacketOverhead = 100;

		private Status m_status;

		private int m_hostId;

		private List<int> m_connectionIds = new List<int>();

		private ConnectionCallback m_connectionCallback;

		private DisconnectionCallback m_disconnectionCallback;

		private DataCallback m_dataCallback;

		private DataCallback m_voipDataCallback;

		private Stopwatch m_shutdownStopwatch = new Stopwatch();

		private byte m_reliableChannelId;

		private byte m_unreliableChannelId;

		private byte m_voipChannelId;

		public static uint DisconnectionTimeInSeconds
		{
			get
			{
				return 7u;
			}
		}

		public bool IsIdle
		{
			get
			{
				return Status.eIdle == m_status;
			}
		}

		public bool Open(ushort gamePort, uint numConnections, bool usePlatformProtocols, ConnectionCallback connectionCallback, DisconnectionCallback disconnectionCallback, DataCallback dataCallback, DataCallback voipDataCallback)
		{
			if (m_status != Status.eIdle)
			{
				NetworkTransport.RemoveHost(m_hostId);
				NetworkTransport.Shutdown();
				m_connectionIds.Clear();
				m_shutdownStopwatch.Stop();
				m_connectionCallback = null;
				m_disconnectionCallback = null;
				m_dataCallback = null;
				m_hostId = 0;
				m_reliableChannelId = 0;
				m_unreliableChannelId = 0;
				m_voipChannelId = 0;
				m_status = Status.eIdle;
			}
			if (connectionCallback != null && disconnectionCallback != null && dataCallback != null && voipDataCallback != null)
			{
				try
				{
					m_connectionCallback = connectionCallback;
					m_disconnectionCallback = disconnectionCallback;
					m_dataCallback = dataCallback;
					m_voipDataCallback = voipDataCallback;
					NetworkTransport.Init();
					ushort num = (ushort)OnlineMultiplayerConfig.MaxTransportMessageSize;
					num += m_unetPacketOverhead;
					ConnectionConfig connectionConfig = new ConnectionConfig();
					connectionConfig.UsePlatformSpecificProtocols = usePlatformProtocols;
					connectionConfig.DisconnectTimeout = DisconnectionTimeInSeconds * 1000;
					connectionConfig.PacketSize = num;
					connectionConfig.SendDelay = 1u;
					m_reliableChannelId = connectionConfig.AddChannel(QosType.ReliableSequenced);
					m_unreliableChannelId = connectionConfig.AddChannel(QosType.Unreliable);
					m_voipChannelId = connectionConfig.AddChannel(QosType.Unreliable);
					HostTopology topology = new HostTopology(connectionConfig, (int)numConnections);
					m_hostId = NetworkTransport.AddHost(topology, gamePort);
					m_status = Status.eOpen;
					return true;
				}
				catch (Exception)
				{
				}
			}
			return false;
		}

		public void Close()
		{
			if (m_status == Status.eOpen)
			{
				for (int i = 0; i < m_connectionIds.Count; i++)
				{
					byte error;
					NetworkTransport.Disconnect(m_hostId, m_connectionIds[i], out error);
				}
				m_status = Status.eClosing;
				m_shutdownStopwatch.Reset();
				m_shutdownStopwatch.Start();
			}
		}

		public int OpenConnection(EndPoint endpoint)
		{
			if (m_status == Status.eOpen && endpoint != null)
			{
				byte error;
				int result = NetworkTransport.ConnectEndPoint(m_hostId, endpoint, 0, out error);
				if (error == 0)
				{
					return result;
				}
			}
			return s_InvalidConnectionId;
		}

		public void CloseConnection(int connectionId)
		{
			if (m_status == Status.eOpen && s_InvalidConnectionId != connectionId && m_connectionIds.Exists((int x) => x == connectionId))
			{
				byte error;
				NetworkTransport.Disconnect(m_hostId, connectionId, out error);
				m_connectionIds.Remove(connectionId);
			}
		}

		public bool SendData(int unetConnectionId, byte[] data, int dataSize, bool sendReliably)
		{
			if (m_status == Status.eOpen && s_InvalidConnectionId != unetConnectionId)
			{
				byte channelId = ((!sendReliably) ? m_unreliableChannelId : m_reliableChannelId);
				byte error;
				return NetworkTransport.Send(m_hostId, unetConnectionId, channelId, data, dataSize, out error);
			}
			return false;
		}

		public bool SendVoipData(int unetConnectionId, byte[] data, int dataSize)
		{
			byte error;
			if (m_status == Status.eOpen && s_InvalidConnectionId != unetConnectionId)
			{
				return NetworkTransport.Send(m_hostId, unetConnectionId, m_voipChannelId, data, dataSize, out error);
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
				if (receiveBuffer == null || m_status != Status.eOpen || num++ >= maxIterations)
				{
					continue;
				}
				int hostId;
				int connectionId;
				int channelId;
				int receivedSize;
				byte error;
				NetworkEventType networkEventType = NetworkTransport.Receive(out hostId, out connectionId, out channelId, receiveBuffer, receiveBuffer.Length, out receivedSize, out error);
				if (error != 0)
				{
					continue;
				}
				if (channelId == m_voipChannelId)
				{
					OnVoipDataEvent(connectionId, receiveBuffer, receivedSize);
					continue;
				}
				switch (networkEventType)
				{
				case NetworkEventType.ConnectEvent:
					if (hostId == m_hostId)
					{
						OnConnectEvent(connectionId);
					}
					flag = false;
					break;
				case NetworkEventType.DisconnectEvent:
					if (hostId == m_hostId)
					{
						OnDisconnectEvent(connectionId);
					}
					flag = false;
					break;
				case NetworkEventType.DataEvent:
					if (hostId == m_hostId)
					{
						OnDataEvent(connectionId, receiveBuffer, receivedSize);
					}
					flag = false;
					break;
				}
			}
			while (!flag);
			if (m_status == Status.eClosing && m_shutdownStopwatch.ElapsedMilliseconds >= m_shutdownDelayTimeInMilliseconds)
			{
				NetworkTransport.RemoveHost(m_hostId);
				NetworkTransport.Shutdown();
				m_connectionIds.Clear();
				m_shutdownStopwatch.Stop();
				m_connectionCallback = null;
				m_disconnectionCallback = null;
				m_dataCallback = null;
				m_hostId = 0;
				m_reliableChannelId = 0;
				m_unreliableChannelId = 0;
				m_voipChannelId = 0;
				m_status = Status.eIdle;
			}
		}

		private void OnConnectEvent(int connectionId)
		{
			if (m_connectionCallback != null)
			{
				if (!m_connectionIds.Exists((int x) => x == connectionId))
				{
					m_connectionIds.Add(connectionId);
				}
				try
				{
					m_connectionCallback(connectionId);
				}
				catch (Exception)
				{
				}
			}
		}

		private void OnDisconnectEvent(int connectionId)
		{
			if (m_disconnectionCallback != null)
			{
				try
				{
					m_disconnectionCallback(connectionId);
				}
				catch (Exception)
				{
				}
				if (m_connectionIds.Exists((int x) => x == connectionId))
				{
					m_connectionIds.Remove(connectionId);
				}
			}
		}

		private void OnDataEvent(int connectionId, byte[] data, int dataSizeInBytes)
		{
			if (m_dataCallback != null)
			{
				try
				{
					m_dataCallback(connectionId, data, dataSizeInBytes);
				}
				catch (Exception)
				{
				}
			}
		}

		private void OnVoipDataEvent(int connectionId, byte[] data, int dataSizeInBytes)
		{
			if (m_voipDataCallback != null)
			{
				try
				{
					m_voipDataCallback(connectionId, data, dataSizeInBytes);
				}
				catch (Exception)
				{
				}
			}
		}
	}
}
