using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class WashingStationMessage : Serialisable
{
	public enum MessageType
	{
		InteractionState = 0,
		AddPlates = 1,
		CleanedPlate = 2
	}

	private const int c_msgTypeBits = 2;

	private const int c_plateCountBits = 4;

	public MessageType m_msgType;

	public bool m_interacting;

	public float m_progress;

	public int m_plateCount;

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write((uint)m_msgType, 2);
		if (m_msgType == MessageType.InteractionState)
		{
			writer.Write(m_interacting);
			writer.Write(m_progress);
		}
		else
		{
			writer.Write((uint)m_plateCount, 4);
		}
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_msgType = (MessageType)reader.ReadUInt32(2);
		if (m_msgType == MessageType.InteractionState)
		{
			m_interacting = reader.ReadBit();
			m_progress = reader.ReadFloat32();
		}
		else
		{
			m_plateCount = (int)reader.ReadUInt32(4);
		}
		return true;
	}
}
