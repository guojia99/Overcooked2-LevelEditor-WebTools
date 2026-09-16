using System;
using System.Collections.Generic;
using Steamworks;

namespace Team17.Online
{
	public class SteamOnlineMultiplayerSessionEnumerateCoordinator : IOnlineMultiplayerSessionEnumerateCoordinator
	{
		private enum Status : short
		{
			eIdle = 0,
			eDelayedStart = 1,
			eEnumerating = 2
		}

		private readonly float m_delayedStartTimeInSeconds = 2f;

		private readonly float m_requestMaxTimeInSeconds = 10f;

		private static Callback<LobbyMatchList_t> s_steamLobbyListCallback;

		private bool m_isInitialized;

		private OnlineMultiplayerSessionPropertyCoordinator m_sessionPropertyCoordinator;

		private float m_gameTimeAtStartOfFrame;

		private float m_timeoutGameTime;

		private Status m_status;

		private OnlineMultiplayerSessionEnumerateCallback m_resultsCallback;

		private OnlineMultiplayerLocalUserId m_localUserId;

		private List<OnlineMultiplayerSessionPropertySearchValue> m_filterParameters;

		private uint m_maxResults;

		public void Initialize(OnlineMultiplayerSessionPropertyCoordinator sessionPropertyCoordinator)
		{
			if (m_isInitialized || sessionPropertyCoordinator == null)
			{
				return;
			}
			try
			{
				if (SteamPlayerManager.Initialized)
				{
					s_steamLobbyListCallback = Callback<LobbyMatchList_t>.Create(OnSteamLobbyList);
					m_sessionPropertyCoordinator = sessionPropertyCoordinator;
					m_isInitialized = true;
				}
			}
			catch (Exception)
			{
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

		public bool Start(OnlineMultiplayerLocalUserId localUserId, List<OnlineMultiplayerSessionPropertySearchValue> filterParameters, ushort maxResults, OnlineMultiplayerSessionEnumerateCallback enumerateCallback)
		{
			if (m_isInitialized && m_status == Status.eIdle && SteamUser.BLoggedOn() && localUserId != null && maxResults > 0 && enumerateCallback != null && ValidateFilterParameters(filterParameters))
			{
				m_localUserId = localUserId;
				m_filterParameters = filterParameters;
				m_maxResults = maxResults;
				m_resultsCallback = enumerateCallback;
				m_timeoutGameTime = m_gameTimeAtStartOfFrame + m_delayedStartTimeInSeconds;
				m_status = Status.eDelayedStart;
				return true;
			}
			return false;
		}

		public void Cancel()
		{
			if (m_isInitialized)
			{
				m_localUserId = null;
				m_filterParameters = null;
				m_maxResults = 0u;
				m_timeoutGameTime = 0f;
				m_resultsCallback = null;
				m_status = Status.eIdle;
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
			case Status.eDelayedStart:
				if (m_gameTimeAtStartOfFrame >= m_timeoutGameTime)
				{
					StartEnumeration();
				}
				break;
			case Status.eEnumerating:
				if (m_gameTimeAtStartOfFrame >= m_timeoutGameTime)
				{
					CompleteRequest(null, false);
				}
				break;
			}
		}

		private void StartEnumeration()
		{
			if (SteamUser.BLoggedOn() && ValidateFilterParameters(m_filterParameters))
			{
				try
				{
					if (m_filterParameters != null)
					{
						for (int i = 0; i < m_filterParameters.Count; i++)
						{
							OnlineMultiplayerSessionPropertySearchValue onlineMultiplayerSessionPropertySearchValue = m_filterParameters[i];
							string name = onlineMultiplayerSessionPropertySearchValue.m_property.Name;
							string pchValueToMatch = onlineMultiplayerSessionPropertySearchValue.m_value.ToString();
							ELobbyComparison eComparisonType = ELobbyComparison.k_ELobbyComparisonEqual;
							switch (onlineMultiplayerSessionPropertySearchValue.m_operator)
							{
							case OnlineMultiplayerSessionPropertySearchValue.Operator.eEquals:
								eComparisonType = ELobbyComparison.k_ELobbyComparisonEqual;
								break;
							case OnlineMultiplayerSessionPropertySearchValue.Operator.eGreaterEqualsThan:
								eComparisonType = ELobbyComparison.k_ELobbyComparisonEqualToOrGreaterThan;
								break;
							case OnlineMultiplayerSessionPropertySearchValue.Operator.eGreaterThan:
								eComparisonType = ELobbyComparison.k_ELobbyComparisonGreaterThan;
								break;
							case OnlineMultiplayerSessionPropertySearchValue.Operator.eLessEqualsThan:
								eComparisonType = ELobbyComparison.k_ELobbyComparisonEqualToOrLessThan;
								break;
							case OnlineMultiplayerSessionPropertySearchValue.Operator.eLessThan:
								eComparisonType = ELobbyComparison.k_ELobbyComparisonLessThan;
								break;
							case OnlineMultiplayerSessionPropertySearchValue.Operator.eNotEquals:
								eComparisonType = ELobbyComparison.k_ELobbyComparisonNotEqual;
								break;
							}
							SteamMatchmaking.AddRequestLobbyListStringFilter(name, pchValueToMatch, eComparisonType);
						}
					}
					SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterDefault);
					if (SteamMatchmaking.RequestLobbyList().m_SteamAPICall != 0)
					{
						m_status = Status.eEnumerating;
						m_timeoutGameTime = m_gameTimeAtStartOfFrame + m_requestMaxTimeInSeconds;
						return;
					}
				}
				catch (Exception)
				{
				}
			}
			CompleteRequest(null, false);
		}

		private void CompleteRequest(List<OnlineMultiplayerSessionEnumeratedRoom> enumeratedSessions, bool wasSuccessful)
		{
			if (m_resultsCallback != null)
			{
				try
				{
					m_resultsCallback(enumeratedSessions, wasSuccessful);
				}
				catch (Exception)
				{
				}
			}
			Cancel();
		}

		private bool ValidateFilterParameters(List<OnlineMultiplayerSessionPropertySearchValue> searchProperties)
		{
			if (searchProperties != null)
			{
				for (int i = 0; i < searchProperties.Count; i++)
				{
					OnlineMultiplayerSessionPropertySearchValue onlineMultiplayerSessionPropertySearchValue = searchProperties[i];
					if (onlineMultiplayerSessionPropertySearchValue == null || onlineMultiplayerSessionPropertySearchValue.m_property == null)
					{
						return false;
					}
				}
			}
			return true;
		}

		private void OnSteamLobbyList(LobbyMatchList_t param)
		{
			if (m_status != Status.eEnumerating)
			{
				return;
			}
			bool wasSuccessful = true;
			List<OnlineMultiplayerSessionEnumeratedRoom> list = null;
			try
			{
				if (param.m_nLobbiesMatching != 0)
				{
					list = new List<OnlineMultiplayerSessionEnumeratedRoom>((int)m_maxResults);
					for (uint num = 0u; num < param.m_nLobbiesMatching; num++)
					{
						if (list.Count >= (int)m_maxResults)
						{
							break;
						}
						CSteamID lobbyByIndex = SteamMatchmaking.GetLobbyByIndex((int)num);
						if (lobbyByIndex.IsValid() && lobbyByIndex.IsLobby())
						{
							list.Add(new OnlineMultiplayerSessionEnumeratedRoom
							{
								m_steamLobbyId = lobbyByIndex
							});
						}
					}
				}
			}
			catch (Exception)
			{
				list = null;
				wasSuccessful = false;
			}
			CompleteRequest(list, wasSuccessful);
		}
	}
}
