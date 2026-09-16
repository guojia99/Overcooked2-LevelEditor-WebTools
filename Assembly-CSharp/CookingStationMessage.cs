using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class CookingStationMessage : Serialisable
{
	public bool m_isTurnedOn;

	public bool m_isCooking;

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write(m_isTurnedOn);
		writer.Write(m_isCooking);
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_isTurnedOn = reader.ReadBit();
		m_isCooking = reader.ReadBit();
		return true;
	}
}
