using BitStream;

namespace Team17.Online
{
	public class OnlineMultiplayerSessionTransportMessageHeader
	{
		public bool IsGameMessage { get; set; }

		public byte MessageTypeId { get; set; }

		public void Serialize(BitStreamWriter stream)
		{
			stream.Write(IsGameMessage);
			stream.Write(MessageTypeId, 7);
		}

		public void Deserialize(BitStreamReader stream)
		{
			IsGameMessage = stream.ReadBit();
			MessageTypeId = stream.ReadByte(7);
		}
	}
}
