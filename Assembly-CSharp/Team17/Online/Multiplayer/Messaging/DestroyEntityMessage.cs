using BitStream;

namespace Team17.Online.Multiplayer.Messaging
{
	public class DestroyEntityMessage : Serialisable
	{
		public EntityMessageHeader m_Header = new EntityMessageHeader();

		public void Initialise(EntityMessageHeader header)
		{
			m_Header = header;
		}

		public void Serialise(BitStreamWriter writer)
		{
			m_Header.Serialise(writer);
		}

		public bool Deserialise(BitStreamReader reader)
		{
			return m_Header.Deserialise(reader);
		}
	}
}
