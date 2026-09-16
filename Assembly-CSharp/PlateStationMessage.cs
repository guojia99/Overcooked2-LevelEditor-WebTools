using BitStream;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class PlateStationMessage : Serialisable
{
	public GameObject m_delivered;

	public bool m_success;

	private EntityMessageHeader m_deliveredHeader = new EntityMessageHeader();

	public void Serialise(BitStreamWriter writer)
	{
		EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(m_delivered);
		entry.m_Header.Serialise(writer);
		writer.Write(m_success);
	}

	public bool Deserialise(BitStreamReader reader)
	{
		bool flag = m_deliveredHeader.Deserialise(reader);
		if (flag)
		{
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(m_deliveredHeader.m_uEntityID);
			m_delivered = entry.m_GameObject;
			m_success = reader.ReadBit();
		}
		return flag;
	}
}
