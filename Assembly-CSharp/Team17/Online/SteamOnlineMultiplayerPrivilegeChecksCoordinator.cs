using System;
using Steamworks;

namespace Team17.Online
{
	public class SteamOnlineMultiplayerPrivilegeChecksCoordinator : IOnlineMultiplayerPrivilegeChecksCoordinator
	{
		private enum Status : byte
		{
			eIdle = 0,
			eStart = 1,
			eComplete = 2
		}

		private readonly float m_waitForNetworkLinkMaxTimeInSeconds = 10f;

		private bool m_isInitialized;

		private Status m_status;

		private OnlineMultiplayerPrivilegeCheckCallback m_callback;

		private float m_gameTimeAtStartOfFrame;

		private float m_gameTimeCurrentRequestTimeout;

		private OnlineMultiplayerPrivilegeCheckResult m_result = OnlineMultiplayerPrivilegeCheckResult.eGenericFailure;

		public void Initialize()
		{
			if (!m_isInitialized)
			{
				m_isInitialized = true;
			}
		}

		public bool IsIdle()
		{
			if (m_isInitialized)
			{
				return Status.eIdle == m_status;
			}
			return false;
		}

		public bool Start(GamepadUser localUser, OnlineMultiplayerPrivilegeCheckCallback callback)
		{
			if (m_isInitialized && m_status == Status.eIdle && null != localUser && callback != null)
			{
				m_callback = callback;
				m_status = Status.eStart;
				return true;
			}
			return false;
		}

		public void Cancel()
		{
			if (m_isInitialized && m_status != Status.eIdle)
			{
				ResetInternalData();
			}
		}

		public void Update(float gameTimeAtStartOfFrame)
		{
			if (!m_isInitialized)
			{
				return;
			}
			m_gameTimeAtStartOfFrame = gameTimeAtStartOfFrame;
			switch (m_status)
			{
			case Status.eIdle:
				break;
			case Status.eStart:
				LoggedOnTest();
				break;
			case Status.eComplete:
				if (m_callback != null)
				{
					try
					{
						OnlineMultiplayerLocalUserId localOnlineUser = null;
						if (m_result == OnlineMultiplayerPrivilegeCheckResult.eSuccess)
						{
							CSteamID steamID = SteamUser.GetSteamID();
							OnlineMultiplayerLocalUserId onlineMultiplayerLocalUserId = new OnlineMultiplayerLocalUserId();
							onlineMultiplayerLocalUserId.m_userName = SteamFriends.GetPersonaName();
							onlineMultiplayerLocalUserId.m_platformId = new OnlineUserPlatformId
							{
								m_steamId = steamID
							};
							onlineMultiplayerLocalUserId.m_steamId = steamID;
							onlineMultiplayerLocalUserId.m_steamUserRestrictions = SteamFriends.GetUserRestrictions();
							localOnlineUser = onlineMultiplayerLocalUserId;
						}
						OnlineMultiplayerReturnCode<OnlineMultiplayerPrivilegeCheckResult> onlineMultiplayerReturnCode = new OnlineMultiplayerReturnCode<OnlineMultiplayerPrivilegeCheckResult>();
						onlineMultiplayerReturnCode.m_returnCode = m_result;
						OnlineMultiplayerReturnCode<OnlineMultiplayerPrivilegeCheckResult> result = onlineMultiplayerReturnCode;
						m_callback(result, localOnlineUser);
					}
					catch (Exception)
					{
					}
				}
				ResetInternalData();
				break;
			}
		}

		private void LoggedOnTest()
		{
			if (m_status != Status.eStart)
			{
				return;
			}
			try
			{
				if (SteamUser.BLoggedOn())
				{
					m_result = OnlineMultiplayerPrivilegeCheckResult.eSuccess;
					m_status = Status.eComplete;
				}
				else
				{
					m_result = OnlineMultiplayerPrivilegeCheckResult.eNotSignedInToPlatform;
					m_status = Status.eComplete;
				}
			}
			catch (Exception)
			{
				m_result = OnlineMultiplayerPrivilegeCheckResult.eNotSignedInToPlatform;
				m_status = Status.eComplete;
			}
		}

		private void ResetInternalData()
		{
			m_status = Status.eIdle;
			m_callback = null;
			m_gameTimeCurrentRequestTimeout = 0f;
			m_result = OnlineMultiplayerPrivilegeCheckResult.eGenericFailure;
		}
	}
}
