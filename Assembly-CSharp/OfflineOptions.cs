using Team17.Online;

public struct OfflineOptions
{
	public enum AdditionalAction
	{
		None = 0,
		PrivilegeCheckAllUsers = 1,
		PrivilegeCheckAllUsersAndSearchForGames = 2
	}

	public GamepadUser hostUser;

	public GameMode searchGameMode;

	public AdditionalAction eAdditionalAction;

	public OnlineMultiplayerConnectionMode? connectionMode;
}
