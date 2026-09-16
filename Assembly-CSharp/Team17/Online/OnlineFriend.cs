namespace Team17.Online
{
	public class OnlineFriend : SteamOnlineFriend
	{
		public enum FriendStatus
		{
			eOffline = 0,
			eOnline = 1,
			eOnlineInSameApplication = 2,
			eOnlineInSameApplicationAndJoinable = 3
		}

		public string m_displayName;

		public FriendStatus m_status;
	}
}
