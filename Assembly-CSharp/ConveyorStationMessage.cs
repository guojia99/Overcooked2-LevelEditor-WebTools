using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class ConveyorStationMessage : Serialisable
{
	public uint m_receiverEntityID;

	public uint m_itemEntityID;

	public float m_arriveTime;

	private EntityMessageHeader m_receiverHeader = new EntityMessageHeader();

	private EntityMessageHeader m_receiverItem = new EntityMessageHeader();

	public void Serialise(BitStreamWriter writer)
	{
		EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(m_receiverEntityID);
		entry.m_Header.Serialise(writer);
		entry = EntitySerialisationRegistry.GetEntry(m_itemEntityID);
		entry.m_Header.Serialise(writer);
		writer.Write(m_arriveTime);
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_receiverHeader.Deserialise(reader);
		m_receiverEntityID = m_receiverHeader.m_uEntityID;
		m_receiverItem.Deserialise(reader);
		m_itemEntityID = m_receiverItem.m_uEntityID;
		m_arriveTime = reader.ReadFloat32();
		return true;
	}
}
