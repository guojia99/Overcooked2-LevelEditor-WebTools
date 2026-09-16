using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class TriggerToggleOnAnimatorMessage : Serialisable
{
	public bool m_value;

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write(m_value);
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_value = reader.ReadBit();
		return true;
	}
}
