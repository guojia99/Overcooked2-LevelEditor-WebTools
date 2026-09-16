namespace Team17.Online
{
	public class OnlineMultiplayerSessionPlayTogetherHosting : SteamOnlineMultiplayerSessionInvite
	{
		public override bool WasAcceptedBy(GamepadUser localUser)
		{
			return base.WasAcceptedBy(localUser);
		}
	}
}
