using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class TriggerColourCycleMessage : Serialisable
{
	public int m_colourIndex;

	private const int indexBitSize = 4;

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write((uint)m_colourIndex, 4);
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_colourIndex = (int)reader.ReadUInt32(4);
		return true;
	}
}
