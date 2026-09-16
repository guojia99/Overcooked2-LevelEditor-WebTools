using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class MixingStationMessage : Serialisable
{
	public bool m_isTurnedOn;

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write(m_isTurnedOn);
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_isTurnedOn = reader.ReadBit();
		return true;
	}
}
