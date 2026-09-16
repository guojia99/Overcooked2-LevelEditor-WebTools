using BitStream;

namespace Team17.Online.Multiplayer.Messaging
{
	public interface Serialisable
	{
		void Serialise(BitStreamWriter writer);

		bool Deserialise(BitStreamReader reader);
	}
}
