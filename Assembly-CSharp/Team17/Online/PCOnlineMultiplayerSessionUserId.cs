using System.IO;

namespace Team17.Online
{
	public abstract class PCOnlineMultiplayerSessionUserId
	{
		public OnlineUserPlatformId PlatformUserId
		{
			get
			{
				return new OnlineUserPlatformId();
			}
		}

		protected void Write(BinaryWriter writer)
		{
		}

		protected void Read(BinaryReader reader)
		{
		}
	}
}
