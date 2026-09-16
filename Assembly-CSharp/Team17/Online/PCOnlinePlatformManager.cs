namespace Team17.Online
{
	public abstract class PCOnlinePlatformManager : Manager, IOnlinePlatformManager
	{
		public string Name()
		{
			return "EDITOR";
		}

		public bool PluginsReady()
		{
			return false;
		}

		public IOnlineMultiplayerNotificationCoordinator OnlineMultiplayerNotificationCoordinator()
		{
			return null;
		}

		public IOnlineFriendsCoordinator OnlineFriendsCoordinator()
		{
			return null;
		}

		public IOnlineAvatarImageCoordinator OnlineAvatarImageCoordinator()
		{
			return null;
		}

		public IOnlineMultiplayerConnectionModeCoordinator OnlineMultiplayerConnectionModeCoordinator()
		{
			return null;
		}

		public IOnlineMultiplayerSessionPropertyCoordinator OnlineMultiplayerSessionPropertyCoordinator()
		{
			return null;
		}

		public IOnlineMultiplayerSessionCoordinator OnlineMultiplayerSessionCoordinator()
		{
			return null;
		}

		public IOnlineMultiplayerPrivilegeChecksCoordinator OnlineMultiplayerPrivilegeChecksCoordinator()
		{
			return null;
		}

		public IOnlineMultiplayerGameInviteCoordinator OnlineMultiplayerGameInviteCoordinator()
		{
			return null;
		}

		public IOnlineMultiplayerSessionEnumerateCoordinator OnlineMultiplayerSessionEnumerateCoordinator()
		{
			return null;
		}

		public IOnlineMultiplayerTransportStats OnlineMultiplayerTransportStats()
		{
			return null;
		}

		protected void Awake()
		{
		}

		protected void Start()
		{
		}

		protected void OnDestroy()
		{
		}

		protected void Update()
		{
		}
	}
}
