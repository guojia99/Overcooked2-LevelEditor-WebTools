using BitStream;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class CannonMessage : Serialisable
{
	public enum CannonState
	{
		Launched = 0,
		Load = 1,
		Unload = 2
	}

	public CannonState m_state;

	public float m_angle;

	public GameObject m_loadedObject;

	public const int m_stateBits = 2;

	private EntityMessageHeader m_entityHeader = new EntityMessageHeader();

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write((uint)m_state, 2);
		writer.Write(m_angle);
		bool flag = m_loadedObject != null;
		writer.Write(flag);
		if (flag)
		{
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(m_loadedObject);
			entry.m_Header.Serialise(writer);
		}
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_state = (CannonState)reader.ReadUInt32(2);
		m_angle = reader.ReadFloat32();
		if (reader.ReadBit())
		{
			m_entityHeader.Deserialise(reader);
			m_loadedObject = EntitySerialisationRegistry.GetEntry(m_entityHeader.m_uEntityID).m_GameObject;
		}
		else
		{
			m_loadedObject = null;
		}
		return true;
	}
}
