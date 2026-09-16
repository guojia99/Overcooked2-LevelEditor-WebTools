using BitStream;
using Steamworks;
using Team17.Online.Multiplayer.Messaging;

namespace Team17.Online
{
	public class SteamOnlineUserPlatformId : Serialisable
	{
		public CSteamID m_steamId = default(CSteamID);

		public void Serialise(BitStreamWriter writer)
		{
			writer.Write(m_steamId.m_SteamID, 64);
		}

		public bool Deserialise(BitStreamReader reader)
		{
			m_steamId = new CSteamID((ulong)reader.ReadUInt64(64));
			return true;
		}
	}
}
