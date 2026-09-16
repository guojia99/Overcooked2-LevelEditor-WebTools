using BitStream;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class RespawnColliderMessage : Serialisable
{
	public GameObject m_targetObject;

	public Vector3 m_killPosition = Vector3.zero;

	private EntityMessageHeader m_targetObjectHeader = new EntityMessageHeader();

	public void Initialise(GameObject _object)
	{
		m_targetObject = _object;
		m_killPosition = m_targetObject.transform.position;
	}

	public void Serialise(BitStreamWriter writer)
	{
		EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(m_targetObject);
		entry.m_Header.Serialise(writer);
		writer.Write(ref m_killPosition);
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_targetObjectHeader.Deserialise(reader);
		EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(m_targetObjectHeader.m_uEntityID);
		if (entry != null)
		{
			m_targetObject = entry.m_GameObject;
			reader.ReadVector3(ref m_killPosition);
			return true;
		}
		return false;
	}
}
