using BitStream;

namespace Team17.Online.Multiplayer.Messaging
{
	public class UserAddedMessage : Serialisable
	{
		private int kIdxBitCount = GameUtils.GetRequiredBitCount(4);

		public uint UserIndex { get; private set; }

		public UserData User { get; private set; }

		public UserAddedMessage()
		{
			User = new UserData();
		}

		public void Initialise(uint _idx, User user)
		{
			UserIndex = _idx;
			User.Initialise(user);
		}

		public void Serialise(BitStreamWriter writer)
		{
			writer.Write(UserIndex, kIdxBitCount);
			User.Serialise(writer);
		}

		public bool Deserialise(BitStreamReader reader)
		{
			UserIndex = reader.ReadUInt32(kIdxBitCount);
			User.Deserialise(reader);
			return true;
		}
	}
}
