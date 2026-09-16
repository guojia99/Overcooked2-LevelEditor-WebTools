using System.Collections.Generic;
using Team17.Online.Multiplayer.Connection;
using Team17.Online.Multiplayer.Messaging;

public class MessageBatcherBuffer
{
	private TechMessageType m_Type;

	private int m_CurrentIndex = -1;

	private MessageBatcher m_CurrentBatcher;

	private FastList<MessageBatcher> m_Batchers = new FastList<MessageBatcher>(4);

	public void Initialise(TechMessageType type)
	{
		m_Type = type;
		for (int i = 0; i < m_Batchers.Capacity; i++)
		{
			AddBatcher();
		}
		Reset();
	}

	public void Reset()
	{
		if (m_Batchers.Count == 0)
		{
			m_CurrentBatcher = AddBatcher();
		}
		else
		{
			m_CurrentBatcher = m_Batchers._items[0];
			for (int i = 0; i < m_Batchers.Count; i++)
			{
				m_Batchers._items[i].Reset(m_Type);
			}
		}
		m_CurrentIndex = 0;
	}

	public bool IsSizeSupported(int size)
	{
		return m_CurrentBatcher.IsSizeSupported(size);
	}

	public bool Dispatch(MessageTransmitter transmitter, NetworkMessageTracker tracker, ref ushort sequence)
	{
		bool flag = true;
		bool flag2 = m_Type == TechMessageType.ReliableMessageBatch || m_Type == TechMessageType.ReliableGameMessage;
		for (int i = 0; i < m_Batchers.Count; i++)
		{
			MessageBatcher messageBatcher = m_Batchers._items[i];
			if (messageBatcher.MessagesBatched <= 0)
			{
				break;
			}
			messageBatcher.SetSequenceNumber(sequence);
			sequence++;
			flag &= transmitter.Transmit(messageBatcher.m_PendingSendBuffer, messageBatcher.m_BytesUsed, flag2);
			if (tracker != null)
			{
				if (flag2)
				{
					tracker.TrackSentMessageBatch(NetworkMessageTracker.MessageBatchType.Reliable, messageBatcher.MessagesBatched);
				}
				else
				{
					tracker.TrackSentMessageBatch(NetworkMessageTracker.MessageBatchType.Unreliable, messageBatcher.MessagesBatched);
				}
			}
		}
		Reset();
		return flag;
	}

	public MessageBatcher GetCurrentBatcher()
	{
		return m_CurrentBatcher;
	}

	public bool AddData(byte[] data, int size)
	{
		if (m_CurrentBatcher.AddData(data, size))
		{
			return true;
		}
		m_CurrentIndex++;
		if (m_CurrentIndex < m_Batchers.Count)
		{
			m_CurrentBatcher = m_Batchers._items[m_CurrentIndex];
			if (m_CurrentBatcher == null)
			{
				m_Batchers._items[m_CurrentIndex] = new MessageBatcher();
				m_CurrentBatcher = m_Batchers._items[m_CurrentIndex];
				m_CurrentBatcher.Initialise(m_Type);
			}
		}
		else
		{
			m_CurrentBatcher = AddBatcher();
		}
		return m_CurrentBatcher.AddData(data, size);
	}

	private MessageBatcher AddBatcher()
	{
		if (m_Batchers.Count == m_Batchers.Capacity)
		{
		}
		MessageBatcher messageBatcher = new MessageBatcher();
		messageBatcher.Initialise(m_Type);
		m_Batchers.Add(messageBatcher);
		return messageBatcher;
	}
}
