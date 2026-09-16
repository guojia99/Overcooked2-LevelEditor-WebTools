using UnityEngine;

namespace Team17.Online
{
	public class SteamOnlineMultiplayerNetworkLinkCoordinator
	{
		public enum LinkStatus
		{
			eAvailable = 0,
			eNotAvailable = 1,
			eUnknown = 2
		}

		private bool m_isInitialized;

		public LinkStatus Status
		{
			get
			{
				return GetStatus();
			}
		}

		public void Initialize()
		{
			if (!m_isInitialized)
			{
				m_isInitialized = true;
			}
		}

		public void Shutdown()
		{
			if (m_isInitialized)
			{
				m_isInitialized = false;
			}
		}

		public void Update()
		{
			if (!m_isInitialized)
			{
			}
		}

		private LinkStatus GetStatus()
		{
			LinkStatus result = LinkStatus.eUnknown;
			if (m_isInitialized)
			{
				switch (Application.internetReachability)
				{
				case NetworkReachability.NotReachable:
					result = LinkStatus.eNotAvailable;
					break;
				case NetworkReachability.ReachableViaCarrierDataNetwork:
				case NetworkReachability.ReachableViaLocalAreaNetwork:
					result = LinkStatus.eAvailable;
					break;
				}
			}
			return result;
		}
	}
}
