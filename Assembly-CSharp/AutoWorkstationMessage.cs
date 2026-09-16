using System;
using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class AutoWorkstationMessage : Serialisable
{
	private const int kBitsPerItemCount = 4;

	public EntitySerialisationEntry[] m_items = new EntitySerialisationEntry[0];

	public bool m_working;

	public EntityMessageHeader m_itemHeader = new EntityMessageHeader();

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write(m_working);
		if (!m_working)
		{
			return;
		}
		writer.Write((uint)m_items.Length, 4);
		for (int i = 0; i < m_items.Length; i++)
		{
			if (m_items[i] != null)
			{
				m_items[i].m_Header.Serialise(writer);
			}
			else
			{
				m_itemHeader.Serialise(writer);
			}
		}
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_working = reader.ReadBit();
		if (m_working)
		{
			int num = (int)reader.ReadUInt32(4);
			Array.Resize(ref m_items, num);
			for (int i = 0; i < num; i++)
			{
				if (m_itemHeader.Deserialise(reader))
				{
					if (m_itemHeader.m_uEntityID != 0)
					{
						m_items[i] = EntitySerialisationRegistry.GetEntry(m_itemHeader.m_uEntityID);
					}
					else
					{
						m_items[i] = null;
					}
				}
			}
		}
		return true;
	}
}
