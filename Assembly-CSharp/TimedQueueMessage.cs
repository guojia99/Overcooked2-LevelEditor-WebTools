using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class TimedQueueMessage : Serialisable
{
	public enum MsgType
	{
		QueueEvent = 0,
		Cancel = 1
	}

	public const int kBitsPerMsgType = 1;

	public const int kBitsPerIndex = 4;

	public MsgType m_msgType;

	public int m_index;

	public float m_time;

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write((uint)m_msgType, 1);
		if (m_msgType == MsgType.QueueEvent)
		{
			writer.Write((uint)m_index, 4);
			writer.Write(m_time);
		}
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_msgType = (MsgType)reader.ReadUInt32(1);
		if (m_msgType == MsgType.QueueEvent)
		{
			m_index = (int)reader.ReadUInt32(4);
			m_time = reader.ReadFloat32();
		}
		return true;
	}
}
