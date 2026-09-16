using BitStream;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ThrowableItemMessage : Serialisable
{
	public GameObject m_thrower;

	public bool m_inFlight;

	private EntityMessageHeader m_entityHeader = new EntityMessageHeader();

	public void Initialise(bool _inFlight, GameObject _thrower)
	{
		m_inFlight = _inFlight;
		m_thrower = _thrower;
	}

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write(m_inFlight);
		if (m_inFlight)
		{
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(m_thrower);
			entry.m_Header.Serialise(writer);
		}
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_inFlight = reader.ReadBit();
		if (m_inFlight)
		{
			m_entityHeader.Deserialise(reader);
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(m_entityHeader.m_uEntityID);
			if (entry == null)
			{
				return false;
			}
			m_thrower = entry.m_GameObject;
		}
		return true;
	}
}
