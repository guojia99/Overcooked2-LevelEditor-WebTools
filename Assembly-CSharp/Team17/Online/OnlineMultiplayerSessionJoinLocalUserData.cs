using System.ComponentModel;

namespace Team17.Online
{
	public class OnlineMultiplayerSessionJoinLocalUserData
	{
		[DefaultValue(null)]
		public OnlineMultiplayerLocalUserId Id { get; set; }

		[DefaultValue(null)]
		public byte[] GameData { get; set; }

		[DefaultValue(0L)]
		public uint GameDataSize { get; set; }
	}
}
