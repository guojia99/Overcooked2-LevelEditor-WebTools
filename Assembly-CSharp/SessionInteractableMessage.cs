using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class SessionInteractableMessage : Serialisable
{
	public enum MessageType
	{
		InteractionState = 0
	}

	private const int c_msgTypeBits = 2;

	public MessageType m_msgType;

	public uint m_interacterID;

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write((uint)m_msgType, 2);
		if (m_msgType == MessageType.InteractionState)
		{
			writer.Write(m_interacterID, 10);
		}
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_msgType = (MessageType)reader.ReadUInt32(2);
		if (m_msgType == MessageType.InteractionState)
		{
			m_interacterID = reader.ReadUInt32(10);
		}
		return true;
	}
}
