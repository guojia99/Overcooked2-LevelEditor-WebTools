using BitStream;

namespace Team17.Online.Multiplayer.Messaging
{
	public class ResumeEntitySyncMessage : Serialisable
	{
		public EntityMessageHeader m_header = new EntityMessageHeader();

		public void Initialise(EntityMessageHeader header)
		{
			m_header = header;
		}

		public void Serialise(BitStreamWriter writer)
		{
			m_header.Serialise(writer);
		}

		public bool Deserialise(BitStreamReader reader)
		{
			return m_header.Deserialise(reader);
		}
	}
}
