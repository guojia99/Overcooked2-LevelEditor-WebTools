using BitStream;

namespace Team17.Online.Multiplayer.Messaging
{
	public class Message : Serialisable
	{
		public MessageType Type;

		public Serialisable Payload;

		public void Serialise(BitStreamWriter writer)
		{
			writer.Write((byte)Type, 8);
			Payload.Serialise(writer);
		}

		public bool Deserialise(BitStreamReader reader)
		{
			Type = (MessageType)reader.ReadByte(8);
			if (Type >= MessageType.Example && Type < MessageType.COUNT)
			{
				return SerialisationRegistry<MessageType>.Deserialise(out Payload, Type, reader);
			}
			return false;
		}
	}
}
