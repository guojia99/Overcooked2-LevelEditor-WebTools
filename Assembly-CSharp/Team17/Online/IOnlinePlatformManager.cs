namespace Team17.Online
{
	public interface IOnlinePlatformManager
	{
		string Name();

		bool PluginsReady();

		IOnlineMultiplayerNotificationCoordinator OnlineMultiplayerNotificationCoordinator();

		IOnlineAvatarImageCoordinator OnlineAvatarImageCoordinator();

		IOnlineFriendsCoordinator OnlineFriendsCoordinator();

		IOnlineMultiplayerConnectionModeCoordinator OnlineMultiplayerConnectionModeCoordinator();

		IOnlineMultiplayerSessionPropertyCoordinator OnlineMultiplayerSessionPropertyCoordinator();

		IOnlineMultiplayerSessionCoordinator OnlineMultiplayerSessionCoordinator();

		IOnlineMultiplayerSessionEnumerateCoordinator OnlineMultiplayerSessionEnumerateCoordinator();

		IOnlineMultiplayerPrivilegeChecksCoordinator OnlineMultiplayerPrivilegeChecksCoordinator();

		IOnlineMultiplayerGameInviteCoordinator OnlineMultiplayerGameInviteCoordinator();

		IOnlineMultiplayerTransportStats OnlineMultiplayerTransportStats();
	}
}
