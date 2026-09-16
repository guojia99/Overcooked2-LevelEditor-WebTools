using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

namespace Team17.Online.Multiplayer.Connection
{
	public class RemoteConnection : BaseConnection
	{
		private IOnlineMultiplayerSessionCoordinator m_OnlineMultiplayerSessionCoordinator;

		private IOnlineMultiplayerSessionUserId m_RemoteUserId;

		protected MessageBatcherBuffer m_OutgoingUnreliable = new MessageBatcherBuffer();

		protected MessageBatcherBuffer m_OutgoingReliable = new MessageBatcherBuffer();

		private LatencyMeasure m_ReliableLatencyMeasure = new LatencyMeasure();

		private LatencyMeasure m_UnreliableLatencyMeasure = new LatencyMeasure();

		public override IOnlineMultiplayerSessionUserId GetRemoteSessionUserId()
		{
			return m_RemoteUserId;
		}

		public void Initialise(PeerBase localPeer, IOnlineMultiplayerSessionCoordinator sessionCoordinator, IOnlineMultiplayerSessionUserId remoteUserId)
		{
			Initialise();
			m_OutgoingUnreliable.Initialise(TechMessageType.UnreliableMessageBatch);
			m_OutgoingReliable.Initialise(TechMessageType.ReliableMessageBatch);
			m_LocalPeer = localPeer;
			m_RemoteUserId = remoteUserId;
			m_OnlineMultiplayerSessionCoordinator = sessionCoordinator;
			m_ReliableLatencyMeasure.Initialise(localPeer, this, remoteUserId, true);
			m_UnreliableLatencyMeasure.Initialise(localPeer, this, remoteUserId, false);
			m_Transmitter.Initialise(TransmitMessage);
		}

		private bool TransmitMessage(byte[] data, int size, bool bReliable)
		{
			return m_OnlineMultiplayerSessionCoordinator.SendData(m_RemoteUserId, data, size, bReliable);
		}

		public override bool SendMessage(byte[] data, int size, bool bReliable)
		{
			if (m_OnlineMultiplayerSessionCoordinator == null)
			{
				return false;
			}
			return base.SendMessage(data, size, bReliable);
		}

		public override void Dispatch()
		{
			MessageBatcherBuffer messageBatcher = GetMessageBatcher(true);
			DispatchLatencyPings(messageBatcher.GetCurrentBatcher(), m_ReliableLatencyMeasure);
			MessageBatcherBuffer messageBatcher2 = GetMessageBatcher(false);
			DispatchLatencyPings(messageBatcher2.GetCurrentBatcher(), m_UnreliableLatencyMeasure);
			if (!SendBufferedData(true))
			{
			}
			if (!SendBufferedData(false))
			{
			}
			m_Transmitter.Update();
		}

		private bool DispatchLatencyPings(MessageBatcher batch, LatencyMeasure measure)
		{
			if (batch.MessagesBatched > 0 && batch.m_BytesUsed > 0)
			{
				if (batch.GetAvailableSpace() > measure.GetMessageSize() && measure.ShouldAppendLatency(batch))
				{
					measure.SendLatencyMessage(LatencyMessage.Stage.Ping, Time.realtimeSinceStartup);
				}
			}
			else if (measure.ShouldForceLatency())
			{
				measure.SendLatencyMessage(LatencyMessage.Stage.Ping, Time.realtimeSinceStartup);
			}
			return false;
		}

		public override ConnectionStats GetConnectionStats(bool bReliable)
		{
			m_Stats.m_fLatency = GetLatencyMeasure(bReliable).GetAverageOneWayTripTime();
			if (bReliable)
			{
				m_Stats.m_fMaxTimeBetweenReceives = m_fMaxReliableReceiveWait;
				m_Stats.m_fIncomingSequenceNumber = m_IncomingReliableSequenceNumber;
				m_Stats.m_fOutgoingSequenceNumber = (int)m_OutgoingReliableSequenceNumber;
			}
			else
			{
				m_Stats.m_fMaxTimeBetweenReceives = m_fMaxUnreliableReceiveWait;
				m_Stats.m_fIncomingSequenceNumber = m_IncomingUnreliableSequenceNumber;
				m_Stats.m_fOutgoingSequenceNumber = (int)m_OutgoingUnreliableSequenceNumber;
			}
			return m_Stats;
		}

		private LatencyMeasure GetLatencyMeasure(bool bReliable)
		{
			if (bReliable)
			{
				return m_ReliableLatencyMeasure;
			}
			return m_UnreliableLatencyMeasure;
		}

		private bool SendBufferedData(bool bReliable)
		{
			MessageBatcherBuffer messageBatcher = GetMessageBatcher(bReliable);
			bool flag = true;
			NetworkMessageTracker tracker = m_LocalPeer.GetTracker();
			if (bReliable)
			{
				return flag & messageBatcher.Dispatch(m_Transmitter, tracker, ref m_OutgoingReliableSequenceNumber);
			}
			return flag & messageBatcher.Dispatch(m_Transmitter, tracker, ref m_OutgoingUnreliableSequenceNumber);
		}

		public override void HandleReceivedBytes(byte[] data, int size)
		{
			m_LocalPeer.HandleReceivedBytesFromConnection(this, data, size);
		}

		public override void Disconnect()
		{
			m_LargeMessageBuffer[0] = m_TechHeaderWriter.SerialiseHeader(true, TechMessageType.Disconnect);
			FastList<byte> fastList = m_TechHeaderWriter.SerialiseSequence(m_OutgoingUnreliableSequenceNumber);
			for (int i = 0; i < fastList.Count; i++)
			{
				m_LargeMessageBuffer[1 + i] = fastList._items[i];
			}
			m_OutgoingReliableSequenceNumber++;
			m_Transmitter.Transmit(m_LargeMessageBuffer, 3, true);
			m_LocalPeer.HandleConnectionLost(m_RemoteUserId, this);
		}

		protected override MessageBatcherBuffer GetMessageBatcher(bool bReliable)
		{
			if (bReliable)
			{
				return m_OutgoingReliable;
			}
			return m_OutgoingUnreliable;
		}
	}
}
