using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class TriggerDisableMessage : Serialisable
{
	public bool m_enabled;

	public void Initialise(bool _enabled)
	{
		m_enabled = _enabled;
	}

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write(m_enabled);
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_enabled = reader.ReadBit();
		return true;
	}
}
