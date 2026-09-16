namespace Team17.Online
{
	public enum OnlineMultiplayerPrivilegeCheckResult : byte
	{
		eSuccess = 0,
		eNoNetwork = 1,
		ePatchRequired = 2,
		eSystemSoftwareUpdateRequired = 3,
		eNotSignedInToPlatform = 4,
		eNoOnlineAccount = 5,
		eUnderAge = 6,
		eNoMultiplayerPrivilege = 7,
		eApplicationSuspended = 8,
		eGenericFailure = 9
	}
}
