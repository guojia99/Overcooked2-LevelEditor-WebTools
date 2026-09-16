using System.Collections.Generic;
using BitStream;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

namespace Team17.Online.Multiplayer.Connection
{
	public class LatencyMeasure
	{
		public const int LatencyHistorySize = 3;

		public const float SyncFrequencyPerSecond = 10f;

		public const float SyncDelay = 0.1f;

		private BitStreamWriter m_Writer;

		private FastList<byte> m_WriteBuffer = new FastList<byte>(16);

		private Message m_Message = new Message();

		private LatencyMessage m_TimeMessage = new LatencyMessage();

		private float m_fLastSync;

		private float m_fTimeTimeReceived;

		private float m_fAverageOneWayLatency;

		private float m_fRunningLatencyTotal;

		private Queue<float> m_OneWayLatencyHistory = new Queue<float>(3);

		private NetworkMessageTracker m_Tracker;

		private bool m_bReliable;

		private PeerBase m_LocalPeer;

		private NetworkConnection m_Connection;

		private IOnlineMultiplayerSessionUserId m_RemoteUserId;

		public float GetAverageOneWayTripTime()
		{
			return m_fAverageOneWayLatency;
		}

		public int GetMessageSize()
		{
			return 3;
		}

		public void Initialise(PeerBase localPeer, NetworkConnection connection, IOnlineMultiplayerSessionUserId remoteUserId, bool bReliable)
		{
			Mailbox.Server.RegisterForMessageType(MessageType.LatencyMeasure, OnLatencyReceived);
			Mailbox.Client.RegisterForMessageType(MessageType.LatencyMeasure, OnLatencyReceived);
			m_Writer = new BitStreamWriter(m_WriteBuffer);
			m_LocalPeer = localPeer;
			m_Connection = connection;
			m_RemoteUserId = remoteUserId;
			m_Tracker = m_LocalPeer.GetTracker();
			m_bReliable = bReliable;
		}

		public void Shutdown()
		{
			Mailbox.Server.UnregisterForMessageType(MessageType.LatencyMeasure, OnLatencyReceived);
			Mailbox.Client.UnregisterForMessageType(MessageType.LatencyMeasure, OnLatencyReceived);
		}

		public void OnLatencyReceived(IOnlineMultiplayerSessionUserId from, Serialisable message)
		{
			if (m_Connection.GetLatencyTestPaused() || from.UniqueId != m_RemoteUserId.UniqueId)
			{
				return;
			}
			LatencyMessage latencyMessage = (LatencyMessage)message;
			if (latencyMessage.m_bReliable != m_bReliable)
			{
				return;
			}
			if (latencyMessage.m_Stage == LatencyMessage.Stage.Ping)
			{
				SendLatencyMessage(LatencyMessage.Stage.Pong, latencyMessage.m_fTime);
			}
			else if (latencyMessage.m_Stage == LatencyMessage.Stage.Pong)
			{
				m_fTimeTimeReceived = Time.realtimeSinceStartup;
				float num = (m_fTimeTimeReceived - latencyMessage.m_fTime) * 0.5f;
				if (m_OneWayLatencyHistory.Count >= 3)
				{
					m_fRunningLatencyTotal -= m_OneWayLatencyHistory.Dequeue();
				}
				m_OneWayLatencyHistory.Enqueue(num);
				m_fRunningLatencyTotal += num;
				m_fAverageOneWayLatency = m_fRunningLatencyTotal / (float)m_OneWayLatencyHistory.Count;
			}
		}

		public bool ShouldAppendLatency(MessageBatcher batch)
		{
			if (!m_Connection.GetLatencyTestPaused())
			{
				float realtimeSinceStartup = Time.realtimeSinceStartup;
				if (m_fLastSync + 0.1f < realtimeSinceStartup)
				{
					return true;
				}
			}
			return false;
		}

		public bool ShouldForceLatency()
		{
			if (!m_Connection.GetLatencyTestPaused())
			{
				float realtimeSinceStartup = Time.realtimeSinceStartup;
				if (m_fLastSync + 0.2f < realtimeSinceStartup)
				{
					return true;
				}
			}
			return false;
		}

		public void SendLatencyMessage(LatencyMessage.Stage stage, float fTime)
		{
			m_TimeMessage.m_Stage = stage;
			m_TimeMessage.m_bReliable = m_bReliable;
			m_TimeMessage.m_fTime = fTime;
			m_WriteBuffer.Clear();
			m_Writer.Reset(m_WriteBuffer);
			m_Message.Type = MessageType.LatencyMeasure;
			m_Message.Payload = m_TimeMessage;
			m_Message.Serialise(m_Writer);
			m_Connection.SendMessage(m_WriteBuffer._items, m_WriteBuffer.Count, m_bReliable);
			if (m_Tracker != null)
			{
				m_Tracker.TrackSentGlobalEvent(MessageType.LatencyMeasure);
			}
			if (stage == LatencyMessage.Stage.Ping)
			{
				m_fLastSync = fTime;
			}
		}
	}
}
