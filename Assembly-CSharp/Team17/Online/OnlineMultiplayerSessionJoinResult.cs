namespace Team17.Online
{
	public enum OnlineMultiplayerSessionJoinResult : byte
	{
		eSuccess = 0,
		eClosed = 1,
		eFull = 2,
		eNoLongerExists = 3,
		eNoHostConnection = 4,
		eLostNetwork = 5,
		eApplicationSuspended = 6,
		eGoneOffline = 7,
		eLoggedOut = 8,
		eCodeVersionMismatch = 9,
		eGenericFailure = 10,
		eNotEnoughRoomForAllLocalUsers = 11
	}
}
