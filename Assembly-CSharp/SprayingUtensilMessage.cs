using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class SprayingUtensilMessage : Serialisable
{
	public bool m_bSpraying;

	public uint m_Carrier;

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write(m_bSpraying);
		if (m_bSpraying)
		{
			writer.Write(m_Carrier, 10);
		}
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_bSpraying = reader.ReadBit();
		if (m_bSpraying)
		{
			m_Carrier = reader.ReadUInt32(10);
		}
		return true;
	}
}
