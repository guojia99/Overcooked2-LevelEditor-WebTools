using BitStream;

namespace Team17.Online.Multiplayer.Messaging
{
	public class EntityMessageHeader : Serialisable
	{
		public uint m_uEntityID;

		public void Serialise(BitStreamWriter writer)
		{
			writer.Write(m_uEntityID, 10);
		}

		public bool Deserialise(BitStreamReader reader)
		{
			m_uEntityID = reader.ReadUInt32(10);
			return true;
		}
	}
}
