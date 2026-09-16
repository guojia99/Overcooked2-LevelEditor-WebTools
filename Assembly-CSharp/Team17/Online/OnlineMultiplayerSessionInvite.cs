namespace Team17.Online
{
	public class OnlineMultiplayerSessionInvite : SteamOnlineMultiplayerSessionInvite
	{
		public override bool WasAcceptedBy(GamepadUser localUser)
		{
			return base.WasAcceptedBy(localUser);
		}
	}
}
