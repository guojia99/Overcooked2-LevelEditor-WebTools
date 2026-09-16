using BitStream;
using Team17.Online.Multiplayer.Messaging;

namespace OrderController
{
	public struct OrderID : Serialisable
	{
		private const int kBitsPerID = 8;

		public uint m_id;

		public OrderID(uint _id)
		{
			m_id = _id;
		}

		public static bool operator ==(OrderID _id, OrderID _other)
		{
			return _id.m_id == _other.m_id;
		}

		public static bool operator !=(OrderID _id, OrderID _other)
		{
			return _id.m_id != _other.m_id;
		}

		public void Serialise(BitStreamWriter writer)
		{
			writer.Write(m_id, 8);
		}

		public bool Deserialise(BitStreamReader reader)
		{
			m_id = reader.ReadUInt32(8);
			return true;
		}
	}
}
