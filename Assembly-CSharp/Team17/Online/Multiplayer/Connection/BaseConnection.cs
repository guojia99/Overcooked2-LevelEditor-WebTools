using System;
using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

namespace Team17.Online.Multiplayer.Connection
{
	public abstract class BaseConnection : NetworkConnection
	{
		public const ushort MaxSequenceValue = ushort.MaxValue;

		public const ushort MaxSequenceJump = 32767;

		public const int UninitialisedSequence = -1;

		protected float m_fLastUnreliablePacketReceivedTime;

		protected float m_fMaxUnreliableReceiveWait;

		protected float m_fLastReliablePacketReceivedTime;

		protected float m_fMaxReliableReceiveWait;

		private float m_fNetworkStatsSettleTime = 5f;

		protected ConnectionStats m_Stats;

		protected TechHeaderWriter m_TechHeaderWriter = new TechHeaderWriter();

		protected byte[] m_LargeMessageBuffer = new byte[1024];

		protected ushort m_OutgoingReliableSequenceNumber;

		protected ushort m_OutgoingUnreliableSequenceNumber;

		protected int m_IncomingReliableSequenceNumber = -1;

		protected int m_IncomingUnreliableSequenceNumber = -1;

		protected NetworkPeer m_LocalPeer;

		protected MessageTransmitter m_Transmitter;

		protected bool m_LatencyPaused;

		protected void Initialise()
		{
			m_OutgoingReliableSequenceNumber = 0;
			m_OutgoingUnreliableSequenceNumber = 0;
			m_IncomingReliableSequenceNumber = -1;
			m_IncomingUnreliableSequenceNumber = -1;
			m_TechHeaderWriter.Initialise();
			m_Transmitter = CreateTransmitter();
			m_fLastUnreliablePacketReceivedTime = Time.realtimeSinceStartup + m_fNetworkStatsSettleTime;
			m_fMaxUnreliableReceiveWait = 0f;
			m_fLastReliablePacketReceivedTime = Time.realtimeSinceStartup + m_fNetworkStatsSettleTime;
			m_fMaxReliableReceiveWait = 0f;
		}

		public abstract IOnlineMultiplayerSessionUserId GetRemoteSessionUserId();

		protected abstract MessageBatcherBuffer GetMessageBatcher(bool bReliable);

		public virtual bool SendMessage(byte[] data, int size, bool bReliable)
		{
			MessageBatcherBuffer messageBatcher = GetMessageBatcher(bReliable);
			if (messageBatcher.IsSizeSupported(size))
			{
				return messageBatcher.AddData(data, size);
			}
			if (size < 1023)
			{
				TransmitLargeMessage(data, size, bReliable);
				return true;
			}
			return TransmitMultiPartMessage(data, size);
		}

		public virtual void HandleReceivedBytes(byte[] data, int size)
		{
			m_LocalPeer.HandleReceivedBytesFromConnection(this, data, size);
		}

		public abstract void Dispatch();

		public abstract void Disconnect();

		public abstract ConnectionStats GetConnectionStats(bool bReliable);

		public virtual void SetLatencyTestPaused(bool paused)
		{
			m_LatencyPaused = paused;
		}

		public virtual bool GetLatencyTestPaused()
		{
			return m_LatencyPaused;
		}

		protected virtual MessageTransmitter CreateTransmitter()
		{
			return new MessageTransmitter();
		}

		protected bool TransmitLargeMessage(byte[] data, int size, bool bReliable)
		{
			if (bReliable)
			{
				m_LargeMessageBuffer[0] = m_TechHeaderWriter.SerialiseHeader(true, TechMessageType.ReliableGameMessage);
				FastList<byte> fastList = m_TechHeaderWriter.SerialiseSequence(m_OutgoingReliableSequenceNumber);
				for (int i = 0; i < fastList.Count; i++)
				{
					m_LargeMessageBuffer[1 + i] = fastList._items[i];
				}
				m_OutgoingReliableSequenceNumber++;
			}
			else
			{
				m_LargeMessageBuffer[0] = m_TechHeaderWriter.SerialiseHeader(true, TechMessageType.UnreliableGameMessage);
				FastList<byte> fastList2 = m_TechHeaderWriter.SerialiseSequence(m_OutgoingUnreliableSequenceNumber);
				for (int j = 0; j < fastList2.Count; j++)
				{
					m_LargeMessageBuffer[1 + j] = fastList2._items[j];
				}
				m_OutgoingUnreliableSequenceNumber++;
			}
			Array.Copy(data, 0, m_LargeMessageBuffer, 3, size);
			return m_Transmitter.Transmit(m_LargeMessageBuffer, size + 3, bReliable);
		}

		protected bool TransmitMultiPartMessage(byte[] data, int size)
		{
			bool flag = true;
			int num = 1020;
			byte b = 2;
			b += (byte)((size - num) / 1021);
			int num2 = size;
			int num3 = 3;
			int num4 = 0;
			m_LargeMessageBuffer[0] = m_TechHeaderWriter.SerialiseHeader(true, TechMessageType.ReliableMultiPart);
			FastList<byte> fastList = m_TechHeaderWriter.SerialiseSequence(m_OutgoingReliableSequenceNumber);
			for (int i = 0; i < fastList.Count; i++)
			{
				m_LargeMessageBuffer[1 + i] = fastList._items[i];
			}
			m_LargeMessageBuffer[3] = b;
			m_OutgoingReliableSequenceNumber++;
			int num5 = 1024 - num3;
			int num6 = num2;
			if (num6 > num5)
			{
				num6 = num5;
				num2 -= num5;
			}
			else
			{
				num2 = 0;
			}
			Array.Copy(data, num4, m_LargeMessageBuffer, num3, num6);
			num4 += num6;
			int num7 = 0;
			flag |= m_Transmitter.Transmit(m_LargeMessageBuffer, num3 + num6, true);
			num7++;
			num3 = 2;
			while (flag && num2 > 0)
			{
				m_LargeMessageBuffer[0] = m_TechHeaderWriter.SerialiseHeader(true, TechMessageType.ReliableMultiPart);
				fastList = m_TechHeaderWriter.SerialiseSequence(m_OutgoingReliableSequenceNumber);
				for (int j = 0; j < fastList.Count; j++)
				{
					m_LargeMessageBuffer[1 + j] = fastList._items[j];
				}
				m_OutgoingReliableSequenceNumber++;
				num5 = 1024 - num3;
				num6 = num2;
				if (num6 > num5)
				{
					num6 = num5;
					num2 -= num5;
				}
				else
				{
					num2 = 0;
				}
				Array.Copy(data, num4, m_LargeMessageBuffer, num3, num6);
				num4 += num6;
				flag |= m_Transmitter.Transmit(m_LargeMessageBuffer, num3 + num6, true);
				num7++;
			}
			return flag;
		}

		public virtual bool CheckReceivedSequenceNumber(bool bReliable, uint sequenceNumber)
		{
			bool result = true;
			if (bReliable)
			{
				int num = m_IncomingReliableSequenceNumber + 1;
				if (num > 65535)
				{
					num = 0;
				}
				if (m_IncomingReliableSequenceNumber == -1 || num == sequenceNumber)
				{
					m_IncomingReliableSequenceNumber = (int)sequenceNumber;
				}
				else
				{
					result = false;
					Disconnect();
				}
				float realtimeSinceStartup = Time.realtimeSinceStartup;
				m_fMaxReliableReceiveWait = Mathf.Max(realtimeSinceStartup - m_fLastReliablePacketReceivedTime, m_fMaxReliableReceiveWait);
				m_fLastReliablePacketReceivedTime = realtimeSinceStartup;
			}
			else
			{
				TrackReceivedUnreliableMessage(m_IncomingUnreliableSequenceNumber, (int)sequenceNumber);
				m_IncomingUnreliableSequenceNumber = (int)sequenceNumber;
			}
			return result;
		}

		private void TrackReceivedUnreliableMessage(int iLastSequence, int iCurrentSequence)
		{
			iLastSequence = iCurrentSequence;
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			m_fMaxUnreliableReceiveWait = Mathf.Max(realtimeSinceStartup - m_fLastUnreliablePacketReceivedTime, m_fMaxUnreliableReceiveWait);
			m_fLastUnreliablePacketReceivedTime = realtimeSinceStartup;
		}
	}
}
