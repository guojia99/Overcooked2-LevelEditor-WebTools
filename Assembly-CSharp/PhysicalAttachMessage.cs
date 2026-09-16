using BitStream;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class PhysicalAttachMessage : Serialisable
{
	public IParentable m_parentable;

	public void Serialise(BitStreamWriter writer)
	{
		uint bits = 0u;
		if (m_parentable != null)
		{
			GameObject gameObject = (m_parentable as MonoBehaviour).gameObject;
			if (gameObject != null)
			{
				EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(gameObject);
				if (entry != null)
				{
					bits = entry.m_Header.m_uEntityID;
				}
			}
		}
		writer.Write(bits, 10);
	}

	public bool Deserialise(BitStreamReader reader)
	{
		uint num = reader.ReadUInt32(10);
		if (num != 0)
		{
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(num);
			if (entry != null)
			{
				m_parentable = entry.m_GameObject.RequireInterface<IParentable>();
			}
		}
		else
		{
			m_parentable = null;
		}
		return true;
	}
}
