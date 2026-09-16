using System.Collections.Generic;
using BitStream;

namespace Team17.Online.Multiplayer.Messaging
{
	public class DestroyEntitiesMessage : Serialisable
	{
		public const int k_idCountBitCount = 5;

		public const int k_idCapacity = 16;

		public uint m_rootId;

		public FastList<uint> m_ids = new FastList<uint>(16);

		public void Initialise(uint root, FastList<uint> ids)
		{
			m_rootId = root;
			m_ids.Clear();
			ids.CopyTo(m_ids._items);
		}

		public void Serialise(BitStreamWriter writer)
		{
			writer.Write(m_rootId, 10);
			writer.Write((uint)m_ids.Count, 5);
			for (int i = 0; i < m_ids.Count; i++)
			{
				writer.Write(m_ids._items[i], 10);
			}
		}

		public bool Deserialise(BitStreamReader reader)
		{
			m_rootId = reader.ReadUInt32(10);
			uint num = reader.ReadUInt32(5);
			m_ids.Clear();
			for (int i = 0; i < num; i++)
			{
				uint item = reader.ReadUInt32(10);
				m_ids.Add(item);
			}
			return true;
		}
	}
}
