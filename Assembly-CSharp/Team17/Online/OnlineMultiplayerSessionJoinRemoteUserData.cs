using System.ComponentModel;

namespace Team17.Online
{
	public class OnlineMultiplayerSessionJoinRemoteUserData
	{
		[DefaultValue(null)]
		public byte[] GameData { get; set; }

		[DefaultValue(0L)]
		public uint GameDataSize { get; set; }
	}
}
