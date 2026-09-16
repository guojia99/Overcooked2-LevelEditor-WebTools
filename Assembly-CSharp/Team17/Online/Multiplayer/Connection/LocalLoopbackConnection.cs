using Team17.Online.Multiplayer.Messaging;

namespace Team17.Online.Multiplayer.Connection
{
	public class LocalLoopbackConnection : BaseConnection
	{
		protected NetworkConnection m_RemoteConnection;

		private int m_iOutgoingMessageBatchers;

		private MessageBatcherBuffer[] m_OutgoingUnreliable = new MessageBatcherBuffer[2];

		private MessageBatcherBuffer[] m_OutgoingReliable = new MessageBatcherBuffer[2];

		public void Initialise(NetworkPeer localPeer, NetworkConnection remoteConnection)
		{
			Initialise();
			for (int i = 0; i < m_OutgoingUnreliable.Length; i++)
			{
				m_OutgoingUnreliable[i] = new MessageBatcherBuffer();
				m_OutgoingUnreliable[i].Initialise(TechMessageType.UnreliableMessageBatch);
			}
			for (int j = 0; j < m_OutgoingReliable.Length; j++)
			{
				m_OutgoingReliable[j] = new MessageBatcherBuffer();
				m_OutgoingReliable[j].Initialise(TechMessageType.ReliableMessageBatch);
			}
			m_LocalPeer = localPeer;
			m_RemoteConnection = remoteConnection;
			m_Transmitter.Initialise(TransmitMessage);
		}

		private bool TransmitMessage(byte[] data, int size, bool bReliable)
		{
			m_RemoteConnection.HandleReceivedBytes(data, size);
			return true;
		}

		public override IOnlineMultiplayerSessionUserId GetRemoteSessionUserId()
		{
			return null;
		}

		public override ConnectionStats GetConnectionStats(bool bReliable)
		{
			return m_Stats;
		}

		public override void Dispatch()
		{
			MessageBatcherBuffer messageBatcher = GetMessageBatcher(true);
			MessageBatcherBuffer messageBatcher2 = GetMessageBatcher(false);
			SwitchBatchBuffers();
			messageBatcher.Dispatch(m_Transmitter, null, ref m_OutgoingReliableSequenceNumber);
			messageBatcher2.Dispatch(m_Transmitter, null, ref m_OutgoingUnreliableSequenceNumber);
			m_Transmitter.Update();
		}

		public override void Disconnect()
		{
			m_LocalPeer.HandleLocalLoopbackConnectionLost(this);
		}

		public override bool SendMessage(byte[] data, int size, bool bReliable)
		{
			if (m_RemoteConnection != null)
			{
				return base.SendMessage(data, size, bReliable);
			}
			return false;
		}

		protected override MessageBatcherBuffer GetMessageBatcher(bool bReliable)
		{
			if (bReliable)
			{
				return m_OutgoingReliable[m_iOutgoingMessageBatchers];
			}
			return m_OutgoingUnreliable[m_iOutgoingMessageBatchers];
		}

		protected void SwitchBatchBuffers()
		{
			if (m_iOutgoingMessageBatchers == 0)
			{
				m_iOutgoingMessageBatchers = 1;
			}
			else
			{
				m_iOutgoingMessageBatchers = 0;
			}
		}
	}
}
