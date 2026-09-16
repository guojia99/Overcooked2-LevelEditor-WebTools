using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class TriggerZoneMessage : Serialisable
{
	public bool m_occupied;

	public void Initialise(bool _occupied)
	{
		m_occupied = _occupied;
	}

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write(m_occupied);
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_occupied = reader.ReadBit();
		return true;
	}
}
