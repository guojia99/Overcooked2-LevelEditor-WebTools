using BitStream;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class AttachStationMessage : Serialisable
{
	public GameObject m_item;

	private EntityMessageHeader m_itemHeader = new EntityMessageHeader();

	public void Serialise(BitStreamWriter writer)
	{
		EntitySerialisationEntry entitySerialisationEntry = null;
		if (m_item != null)
		{
			entitySerialisationEntry = EntitySerialisationRegistry.GetEntry(m_item);
		}
		bool flag = null != entitySerialisationEntry;
		writer.Write(flag);
		if (flag)
		{
			entitySerialisationEntry.m_Header.Serialise(writer);
		}
	}

	public bool Deserialise(BitStreamReader reader)
	{
		if (reader.ReadBit())
		{
			m_itemHeader.Deserialise(reader);
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(m_itemHeader.m_uEntityID);
			if (entry != null)
			{
				m_item = entry.m_GameObject;
			}
			else
			{
				m_item = null;
			}
		}
		else
		{
			m_item = null;
		}
		return true;
	}
}
