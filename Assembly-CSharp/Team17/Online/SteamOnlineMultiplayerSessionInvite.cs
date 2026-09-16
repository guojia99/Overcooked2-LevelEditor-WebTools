using Steamworks;

namespace Team17.Online
{
	public abstract class SteamOnlineMultiplayerSessionInvite
	{
		public CSteamID m_steamLobbyId;

		public virtual bool WasAcceptedBy(GamepadUser localUser)
		{
			return (null != localUser) ? true : false;
		}
	}
}
