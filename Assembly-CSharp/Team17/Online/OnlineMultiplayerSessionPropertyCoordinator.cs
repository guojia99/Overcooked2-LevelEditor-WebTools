using System;
using System.Collections.Generic;

namespace Team17.Online
{
	public class OnlineMultiplayerSessionPropertyCoordinator : SteamOnlineMultiplayerSessionPropertyCoordinator, IOnlineMultiplayerSessionPropertyCoordinator
	{
		private bool m_isInitialized;

		private List<OnlineMultiplayerSessionProperty> m_sessionProperties = new List<OnlineMultiplayerSessionProperty>(Enum.GetNames(typeof(OnlineMultiplayerSessionPropertyId)).Length);

		public bool IsInitialized()
		{
			return m_isInitialized;
		}

		public IOnlineMultiplayerSessionProperty FindProperty(OnlineMultiplayerSessionPropertyId id)
		{
			return FindPropertyInternal(id);
		}

		public void Initialize()
		{
			if (m_isInitialized || m_sessionProperties == null)
			{
				return;
			}
			try
			{
				uint num = 0u;
				foreach (OnlineMultiplayerSessionPropertyId value in Enum.GetValues(typeof(OnlineMultiplayerSessionPropertyId)))
				{
					m_sessionProperties.Add(new OnlineMultiplayerSessionProperty
					{
						m_name = value.ToString(),
						m_id = (uint)value,
						m_index = num++
					});
				}
				if (m_sessionProperties.Count > 0)
				{
					if (Open(m_sessionProperties))
					{
						m_isInitialized = true;
					}
					else
					{
						m_sessionProperties.Clear();
					}
				}
			}
			catch (Exception)
			{
				m_isInitialized = false;
				m_sessionProperties.Clear();
			}
		}

		private IOnlineMultiplayerSessionProperty FindPropertyInternal(OnlineMultiplayerSessionPropertyId id)
		{
			if (m_isInitialized)
			{
				for (int i = 0; i < m_sessionProperties.Count; i++)
				{
					if (id == (OnlineMultiplayerSessionPropertyId)m_sessionProperties[i].Id)
					{
						return m_sessionProperties[i];
					}
				}
			}
			return null;
		}
	}
}
