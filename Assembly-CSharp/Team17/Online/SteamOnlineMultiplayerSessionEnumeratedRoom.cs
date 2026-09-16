using Steamworks;

namespace Team17.Online
{
	public abstract class SteamOnlineMultiplayerSessionEnumeratedRoom
	{
		public CSteamID m_steamLobbyId;

		public virtual string GetHostName()
		{
			return string.Empty;
		}
	}
}
