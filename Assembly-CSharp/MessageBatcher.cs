using System;
using System.Collections.Generic;
using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class MessageBatcher
{
	public const int MAX_GAMEMESSAGE_SIZE = 255;

	public int m_BytesUsed;

	public byte[] m_PendingSendBuffer = new byte[1024];

	public int MessagesBatched;

	private TechHeaderWriter m_TechHeaderWriter = new TechHeaderWriter();

	private FastList<byte> m_SizeBuffer = new FastList<byte>(1);

	private BitStreamWriter m_SizeWriter;

	public void Initialise(TechMessageType type)
	{
		m_SizeWriter = new BitStreamWriter(m_SizeBuffer);
		m_TechHeaderWriter.Initialise();
		Reset(type);
	}

	public void Reset(TechMessageType type)
	{
		m_SizeBuffer.Clear();
		m_SizeWriter.Reset(m_SizeBuffer);
		Array.Clear(m_PendingSendBuffer, 0, m_PendingSendBuffer.Length);
		m_PendingSendBuffer[0] = m_TechHeaderWriter.SerialiseHeader(true, type);
		for (int i = 1; i < 3; i++)
		{
			m_PendingSendBuffer[i] = 0;
		}
		m_BytesUsed = 3;
		MessagesBatched = 0;
	}

	public void SetSequenceNumber(uint sequenceNumber)
	{
		FastList<byte> fastList = m_TechHeaderWriter.SerialiseSequence(sequenceNumber);
		for (int i = 0; i < fastList.Count; i++)
		{
			m_PendingSendBuffer[1 + i] = fastList._items[i];
		}
	}

	public bool IsSizeSupported(int size)
	{
		return size <= 255;
	}

	public bool AddData(byte[] data, int size)
	{
		if (size + 1 > GetAvailableSpace())
		{
			return false;
		}
		if (size + 1 > 255)
		{
			return false;
		}
		m_SizeBuffer.Clear();
		m_SizeWriter.Write((uint)(size + 1), 8);
		m_PendingSendBuffer[m_BytesUsed] = m_SizeBuffer._items[0];
		m_BytesUsed++;
		Array.Copy(data, 0, m_PendingSendBuffer, m_BytesUsed, size);
		m_BytesUsed += size;
		MessagesBatched++;
		return true;
	}

	public int GetAvailableSpace()
	{
		return 1024 - m_BytesUsed;
	}
}
