using System.Collections.Generic;
using BitStream;

namespace Team17.Online.Multiplayer.Messaging
{
	public class EntitySynchronisationMessage : Serialisable
	{
		public EntityMessageHeader m_Header = new EntityMessageHeader();

		public FastList<Serialisable> m_Payloads = new FastList<Serialisable>(16);

		public void Initialise(EntityMessageHeader header, FastList<Serialisable> payloads)
		{
			m_Header = header;
			m_Payloads = payloads;
		}

		public void Serialise(BitStreamWriter writer)
		{
			m_Header.Serialise(writer);
			for (int i = 0; i < m_Payloads.Count; i++)
			{
				if (m_Payloads._items[i] != null)
				{
					writer.Write(true);
					m_Payloads._items[i].Serialise(writer);
				}
				else
				{
					writer.Write(false);
				}
			}
		}

		public bool Deserialise(BitStreamReader reader)
		{
			m_Payloads.Clear();
			if (m_Header.Deserialise(reader))
			{
				EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(m_Header.m_uEntityID);
				if (entry != null)
				{
					for (int i = 0; i < entry.m_ClientSynchronisedComponents.Count; i++)
					{
						if (reader.ReadBit())
						{
							ClientSynchroniser clientSynchroniser = entry.m_ClientSynchronisedComponents._items[i];
							Serialisable message;
							SerialisationRegistry<EntityType>.Deserialise(out message, clientSynchroniser.GetEntityType(), reader);
							m_Payloads.Add(message);
						}
						else
						{
							m_Payloads.Add(null);
						}
					}
					return true;
				}
			}
			return false;
		}
	}
}
