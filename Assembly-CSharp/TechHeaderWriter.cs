using System.Collections.Generic;
using BitStream;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;

public class TechHeaderWriter
{
	public const int HeaderIndex = 0;

	public const int SequenceIndex = 1;

	public const int SequenceSize = 2;

	public const int Size = 3;

	private OnlineMultiplayerSessionTransportMessageHeader m_TechHeader = new OnlineMultiplayerSessionTransportMessageHeader();

	private FastList<byte> m_TechHeaderBuffer = new FastList<byte>(2);

	private BitStreamWriter m_TechHeaderWriter;

	public void Initialise()
	{
		m_TechHeaderWriter = new BitStreamWriter(m_TechHeaderBuffer);
	}

	public byte SerialiseHeader(bool bIsGameMessage, TechMessageType type)
	{
		m_TechHeaderBuffer.Clear();
		m_TechHeaderWriter.Reset(m_TechHeaderBuffer);
		m_TechHeader.IsGameMessage = bIsGameMessage;
		m_TechHeader.MessageTypeId = (byte)type;
		m_TechHeader.Serialize(m_TechHeaderWriter);
		return m_TechHeaderBuffer._items[0];
	}

	public FastList<byte> SerialiseSequence(uint sequenceNumber)
	{
		m_TechHeaderBuffer.Clear();
		m_TechHeaderWriter.Reset(m_TechHeaderBuffer);
		m_TechHeaderWriter.Write(sequenceNumber, 16);
		return m_TechHeaderBuffer;
	}
}
