using BitStream;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class AttachmentCatcherMessage : Serialisable
{
	public GameObject m_object;

	public bool m_hasObject;

	private EntityMessageHeader m_entityHeader = new EntityMessageHeader();

	public void Initialise(GameObject _object)
	{
		m_object = _object;
		m_hasObject = _object != null;
	}

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write(m_hasObject);
		if (m_hasObject)
		{
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(m_object);
			entry.m_Header.Serialise(writer);
		}
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_hasObject = reader.ReadBit();
		if (m_hasObject)
		{
			m_entityHeader.Deserialise(reader);
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(m_entityHeader.m_uEntityID);
			if (entry == null)
			{
				return false;
			}
			m_object = entry.m_GameObject;
		}
		else
		{
			m_object = null;
		}
		return true;
	}
}
