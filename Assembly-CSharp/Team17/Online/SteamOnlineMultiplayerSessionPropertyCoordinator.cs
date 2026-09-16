using System.Collections.Generic;

namespace Team17.Online
{
	public abstract class SteamOnlineMultiplayerSessionPropertyCoordinator
	{
		private bool m_isBaseInitialized;

		protected bool Open(List<OnlineMultiplayerSessionProperty> properties)
		{
			if (!m_isBaseInitialized && properties != null)
			{
				m_isBaseInitialized = true;
			}
			return m_isBaseInitialized;
		}

		protected void Close()
		{
			if (m_isBaseInitialized)
			{
				m_isBaseInitialized = false;
			}
		}
	}
}
