using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class FireHazardSpawnerMessage : Serialisable
{
	public EntitySerialisationEntry m_parentEntry = new EntitySerialisationEntry();

	public EntitySerialisationEntry m_spawnedEntry = new EntitySerialisationEntry();

	public void Serialise(BitStreamWriter writer)
	{
		m_parentEntry.m_Header.Serialise(writer);
		m_spawnedEntry.m_Header.Serialise(writer);
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_parentEntry.m_Header.Deserialise(reader);
		m_spawnedEntry.m_Header.Deserialise(reader);
		return true;
	}
}
