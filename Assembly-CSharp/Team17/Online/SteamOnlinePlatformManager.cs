using UnityEngine;

namespace Team17.Online
{
	public abstract class SteamOnlinePlatformManager : Manager, IOnlinePlatformManager
	{
		private bool m_steamworksPluginsReady;

		private float m_gameTimeAtStartOfFrame;

		private OnlineMultiplayerTransportStats m_transportStats;

		private SteamOnlineMultiplayerGameInviteCoordinator m_multiplayerGameInviteCoordinator = new SteamOnlineMultiplayerGameInviteCoordinator();

		private SteamOnlineMultiplayerPrivilegeChecksCoordinator m_multiplayerPrivilegeChecksCoordinator = new SteamOnlineMultiplayerPrivilegeChecksCoordinator();

		private OnlineMultiplayerSessionPropertyCoordinator m_multiplayerSessionPropertyCoordinator = new OnlineMultiplayerSessionPropertyCoordinator();

		private SteamOnlineMultiplayerSessionCoordinator m_multiplayerSessionCoordinator = new SteamOnlineMultiplayerSessionCoordinator();

		private SteamOnlineMultiplayerSessionEnumerateCoordinator m_multiplayerSessionEnumerateCoordinator = new SteamOnlineMultiplayerSessionEnumerateCoordinator();

		private SteamOnlineAvatarImageCoordinator m_avatarImageCoordinator = new SteamOnlineAvatarImageCoordinator();

		public string Name()
		{
			return "STEAM";
		}

		public bool PluginsReady()
		{
			return m_steamworksPluginsReady;
		}

		public IOnlineFriendsCoordinator OnlineFriendsCoordinator()
		{
			return null;
		}

		public IOnlineMultiplayerNotificationCoordinator OnlineMultiplayerNotificationCoordinator()
		{
			return null;
		}

		public IOnlineAvatarImageCoordinator OnlineAvatarImageCoordinator()
		{
			return m_avatarImageCoordinator;
		}

		public IOnlineMultiplayerConnectionModeCoordinator OnlineMultiplayerConnectionModeCoordinator()
		{
			return null;
		}

		public IOnlineMultiplayerSessionPropertyCoordinator OnlineMultiplayerSessionPropertyCoordinator()
		{
			return m_multiplayerSessionPropertyCoordinator;
		}

		public IOnlineMultiplayerSessionCoordinator OnlineMultiplayerSessionCoordinator()
		{
			return m_multiplayerSessionCoordinator;
		}

		public IOnlineMultiplayerPrivilegeChecksCoordinator OnlineMultiplayerPrivilegeChecksCoordinator()
		{
			return m_multiplayerPrivilegeChecksCoordinator;
		}

		public IOnlineMultiplayerGameInviteCoordinator OnlineMultiplayerGameInviteCoordinator()
		{
			return m_multiplayerGameInviteCoordinator;
		}

		public IOnlineMultiplayerSessionEnumerateCoordinator OnlineMultiplayerSessionEnumerateCoordinator()
		{
			return m_multiplayerSessionEnumerateCoordinator;
		}

		public IOnlineMultiplayerTransportStats OnlineMultiplayerTransportStats()
		{
			return m_transportStats;
		}

		protected void Awake()
		{
		}

		protected void Start()
		{
			if (SteamPlayerManager.Initialized)
			{
				if (Debug.isDebugBuild)
				{
					m_transportStats = new OnlineMultiplayerTransportStats();
				}
				m_multiplayerSessionPropertyCoordinator.Initialize();
				m_multiplayerGameInviteCoordinator.Initialize();
				m_multiplayerPrivilegeChecksCoordinator.Initialize();
				m_multiplayerSessionEnumerateCoordinator.Initialize(m_multiplayerSessionPropertyCoordinator);
				m_multiplayerSessionCoordinator.Initialize(m_multiplayerSessionPropertyCoordinator, m_transportStats);
				m_avatarImageCoordinator.Initialize();
				m_steamworksPluginsReady = true;
			}
			m_gameTimeAtStartOfFrame = Time.time;
		}

		protected void Update()
		{
			m_gameTimeAtStartOfFrame = Time.time;
			if (m_steamworksPluginsReady)
			{
				m_multiplayerGameInviteCoordinator.Update();
				m_multiplayerPrivilegeChecksCoordinator.Update(m_gameTimeAtStartOfFrame);
				m_multiplayerSessionEnumerateCoordinator.Update(m_gameTimeAtStartOfFrame);
				m_multiplayerSessionCoordinator.Update(m_gameTimeAtStartOfFrame);
				m_avatarImageCoordinator.Update(m_gameTimeAtStartOfFrame);
				if (m_transportStats != null)
				{
					m_transportStats.Update(m_gameTimeAtStartOfFrame);
				}
			}
		}
	}
}
