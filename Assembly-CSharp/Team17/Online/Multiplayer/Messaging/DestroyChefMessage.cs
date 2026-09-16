using BitStream;

namespace Team17.Online.Multiplayer.Messaging
{
	public class DestroyChefMessage : Serialisable
	{
		public DestroyEntityMessage m_Chef = new DestroyEntityMessage();

		public void Initialise(EntityMessageHeader chef)
		{
			m_Chef.Initialise(chef);
		}

		public void Serialise(BitStreamWriter writer)
		{
			m_Chef.Serialise(writer);
		}

		public bool Deserialise(BitStreamReader reader)
		{
			return m_Chef.Deserialise(reader);
		}
	}
}
