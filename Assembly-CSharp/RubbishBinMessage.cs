using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class RubbishBinMessage : Serialisable
{
	public uint BinnedItemEntityID;

	public bool m_alive = true;

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write(BinnedItemEntityID, 10);
		writer.Write(m_alive);
	}

	public bool Deserialise(BitStreamReader reader)
	{
		BinnedItemEntityID = reader.ReadUInt32(10);
		m_alive = reader.ReadBit();
		return true;
	}
}
