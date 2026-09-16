using System;
using BitStream;
using Team17.Online.Multiplayer.Connection;
using Team17.Online.Multiplayer.Messaging;

namespace Team17.Online.Multiplayer
{
	public abstract class PeerBase : NetworkPeer
	{
		private int iRunningCount;

		protected Message m_CurrentMessage = new Message();

		private OnlineMultiplayerSessionTransportMessageHeader m_TechHeader = new OnlineMultiplayerSessionTransportMessageHeader();

		protected NetworkMessageTracker m_Tracker;

		private byte[] m_MultiPartMessageData = new byte[262144];

		private int m_MultiPartBufferWritePosition = -1;

		private int m_RemainingMultiPartMessages = -1;

		public event GenericVoid<IOnlineMultiplayerSessionUserId, MessageType, Serialisable, uint, bool> OnMessageReceived;

		public event GenericVoid OnLeftSession;

		public void HandleReceivedBytesFromConnection(NetworkConnection connection, byte[] data, int size)
		{
			if (this.OnMessageReceived == null)
			{
				return;
			}
			BitStreamReader bitStreamReader = new BitStreamReader(data);
			m_TechHeader.Deserialize(bitStreamReader);
			if (!m_TechHeader.IsGameMessage)
			{
				return;
			}
			if (m_TechHeader.MessageTypeId == 5)
			{
				HandleDisconnectMessage(connection);
				return;
			}
			bool bReliable = m_TechHeader.MessageTypeId == 0 || m_TechHeader.MessageTypeId == 2 || m_TechHeader.MessageTypeId == 4;
			ushort num = bitStreamReader.ReadUInt16(16);
			if (connection.CheckReceivedSequenceNumber(bReliable, num))
			{
				if (m_TechHeader.MessageTypeId == 0 || m_TechHeader.MessageTypeId == 1)
				{
					HandleReceivedBatchMessage(bitStreamReader, connection, bReliable, size, num);
				}
				else if (m_TechHeader.MessageTypeId == 4)
				{
					HandleReceivedMultiPart(bitStreamReader, data, connection, bReliable, size, num);
				}
				else
				{
					HandleReceivedGameMessage(bitStreamReader, connection, bReliable, size, num);
				}
			}
		}

		private void HandleReceivedMultiPart(BitStreamReader reader, byte[] data, NetworkConnection connection, bool bReliable, int gameMessageSize, uint sequence)
		{
			if (m_RemainingMultiPartMessages == -1)
			{
				m_RemainingMultiPartMessages = reader.ReadByte(8);
				m_MultiPartBufferWritePosition = 0;
			}
			int num = reader.CurrentIndex + 1;
			int num2 = data.Length - num;
			Array.Copy(data, num, m_MultiPartMessageData, m_MultiPartBufferWritePosition, num2);
			m_MultiPartBufferWritePosition += num2;
			m_RemainingMultiPartMessages--;
			if (m_RemainingMultiPartMessages == 0)
			{
				BitStreamReader reader2 = new BitStreamReader(m_MultiPartMessageData);
				HandleReceivedGameMessage(reader2, connection, bReliable, m_MultiPartBufferWritePosition, sequence);
				m_MultiPartBufferWritePosition = -1;
				m_RemainingMultiPartMessages = -1;
			}
		}

		private void HandleReceivedGameMessage(BitStreamReader reader, NetworkConnection connection, bool bReliable, int gameMessageSize, uint sequence)
		{
			if (!m_CurrentMessage.Deserialise(reader))
			{
				return;
			}
			if (m_Tracker != null)
			{
				m_Tracker.TrackReceivedGlobalEvent(m_CurrentMessage.Type);
			}
			try
			{
				this.OnMessageReceived(connection.GetRemoteSessionUserId(), m_CurrentMessage.Type, m_CurrentMessage.Payload, sequence, bReliable);
			}
			catch (Exception e)
			{
				ExceptionManager exceptionManager = GameUtils.RequestManager<ExceptionManager>();
				if (null != exceptionManager)
				{
					exceptionManager.LogACaughtException(e, "Exception caught when processing received message. " + NetworkUtils.GetNetworkMessageDescription(m_CurrentMessage));
				}
			}
			iRunningCount++;
		}

		private void HandleReceivedBatchMessage(BitStreamReader reader, NetworkConnection connection, bool bReliable, int size, uint sequence)
		{
			iRunningCount = 0;
			while (reader.CurrentIndex + 1 < size)
			{
				reader.AdvanceToNextByteBoundary();
				int currentIndex = reader.CurrentIndex;
				int num = (int)reader.ReadUInt32(8);
				int num2 = currentIndex + num;
				HandleReceivedGameMessage(reader, connection, bReliable, num, sequence);
				if (reader.CurrentIndex != num2)
				{
					reader.SkipToByteIndex(num2 + 1);
				}
			}
			if (m_Tracker != null && connection.GetRemoteSessionUserId() != null)
			{
				m_Tracker.TrackReceivedMessageBatch((!bReliable) ? NetworkMessageTracker.MessageBatchType.Unreliable : NetworkMessageTracker.MessageBatchType.Reliable, iRunningCount);
			}
		}

		public abstract void Dispatch();

		public abstract ConnectionStats GetConnectionStats(bool bReliable);

		public void HandleManuallyDeserialisedMessage(IOnlineMultiplayerSessionUserId sessionUserId, MessageType type, Serialisable message)
		{
			if (this.OnMessageReceived != null)
			{
				this.OnMessageReceived(sessionUserId, type, message, uint.MaxValue, true);
			}
		}

		public void SetTracker(NetworkMessageTracker tracker)
		{
			m_Tracker = tracker;
		}

		public NetworkMessageTracker GetTracker()
		{
			return m_Tracker;
		}

		public void LeaveSession(IOnlineMultiplayerSessionCoordinator m_iOnlineMultiplayerSessionCoordinator)
		{
			m_iOnlineMultiplayerSessionCoordinator.Leave();
			if (this.OnLeftSession != null)
			{
				this.OnLeftSession();
			}
		}

		public abstract void HandleConnectionLost(IOnlineMultiplayerSessionUserId sessionUserId, NetworkConnection connection);

		public abstract void HandleLocalLoopbackConnectionLost(NetworkConnection connection);

		public abstract void HandleDisconnectMessage(NetworkConnection connection);

		public abstract void SetLatencyTestPaused(bool paused);
	}
}
