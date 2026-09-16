using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class PickupItemSwitcherMessage : Serialisable
{
	public int m_itemIndex;

	private const int indexBitSize = 4;

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write((uint)m_itemIndex, 4);
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_itemIndex = (int)reader.ReadUInt32(4);
		return true;
	}
}
