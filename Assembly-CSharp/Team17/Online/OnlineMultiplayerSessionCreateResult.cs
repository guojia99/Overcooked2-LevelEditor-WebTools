namespace Team17.Online
{
	public enum OnlineMultiplayerSessionCreateResult : byte
	{
		eSuccess = 0,
		eGenericFailure = 1,
		eLostNetwork = 2,
		eApplicationSuspended = 3,
		eGoneOffline = 4,
		eLoggedOut = 5
	}
}
