namespace Team17.Online
{
	public interface IOnlineMultiplayerPrivilegeChecksCoordinator
	{
		bool IsIdle();

		bool Start(GamepadUser localUser, OnlineMultiplayerPrivilegeCheckCallback callback);

		void Cancel();
	}
}
