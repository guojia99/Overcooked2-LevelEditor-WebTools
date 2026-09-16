using System;
using System.Collections.Generic;
using BitStream;
using Steamworks;
using Team17.Online.Shared;

namespace Team17.Online
{
	public class SteamOnlineMultiplayerSessionCoordinator : IOnlineMultiplayerSessionCoordinator
	{
		private enum SessionStatus : byte
		{
			eIdle = 0,
			eCreating = 1,
			eRunning = 2,
			eJoining = 3,
			eConnectingToHost = 4
		}

		private enum CreatingSubStatus : byte
		{
			eIdle = 0,
			eInitialDelay = 1,
			eWaitingForSteamToCreateTheLobby = 2,
			eComplete = 3
		}

		private enum JoiningSubStatus : byte
		{
			eIdle = 0,
			eInitialDelay = 1,
			eWaitingForSteamToJoinTheLobby = 2,
			eCompleteComplete = 3
		}

		private enum TransportMessageTypes : byte
		{
			eNothing = 0,
			eJoinRequest = 1,
			eJoinRequestReply = 2,
			eUserJoined = 3,
			eUserDisconnected = 4,
			eKeepalive = 5
		}

		private readonly float m_entryDelayInSeconds = 1f;

		private readonly float m_connectToClientMaxTimeInSeconds = 30f;

		private readonly float m_connectToHostMaxTimeInSeconds = 20f;

		private readonly float m_keepaliveMessageFrequencyInSeconds = 1f;

		private static Callback<LobbyCreated_t> s_steamLobbyCreatedCallback;

		private static Callback<LobbyEnter_t> s_steamLobbyJoinedCallback;

		private static Callback<LobbyChatUpdate_t> s_steamLobbyMembersChangedCallback;

		private float m_gameTimeAtStartOfFrame;

		private bool m_isInitialized;

		private bool m_leaveRequested;

		private bool m_forceModify;

		private CSteamID m_hostSteamId = default(CSteamID);

		private CSteamID m_hostSteamIdDuringConnection = default(CSteamID);

		private byte m_uniqueUserIdCounter = OnlineMultiplayerSessionUserId.c_InvalidUniqueId;

		private CSteamID m_lobbyId = default(CSteamID);

		private CSteamID m_lobbyIdToJoin = default(CSteamID);

		private SessionStatus m_sessionStatus;

		private CreatingSubStatus m_creatingSubStatus;

		private JoiningSubStatus m_joiningSubStatus;

		private EChatRoomEnterResponse m_joiningLobbyResponse = EChatRoomEnterResponse.k_EChatRoomEnterResponseError;

		private List<OnlineMultiplayerSessionPropertyValue> m_creatingPropertyValues;

		private float m_entryDelayMaxGameTime;

		private OnlineMultiplayerLocalUserId m_localPlayerUserId;

		private OnlineMultiplayerSessionVisibility m_sessionVisibility = OnlineMultiplayerSessionVisibility.eClosed;

		public List<OnlineMultiplayerSessionUserId> m_userIdList = new List<OnlineMultiplayerSessionUserId>();

		private OnlineMultiplayerSessionPropertyCoordinator m_sessionPropertyCoordinator;

		private OnlineMultiplayerTransportStats m_transportStats;

		private OnlineMultiplayerSessionCreateCallback m_createSessionCallback;

		private OnlineMultiplayerSessionDisconnectionCallback m_disconnectionCallbacks = delegate
		{
		};

		private OnlineMultiplayerSessionRemoteUserDisconnectionCallback m_remoteUserDisconnectionCallbacks = delegate
		{
		};

		private OnlineMultiplayerSessionDataReceivedCallback m_dataReceivedCallback = delegate
		{
		};

		private OnlineMultiplayerSessionJoinDecisionCallback m_joinDecisionCallback;

		private OnlineMultiplayerSessionUserJoinedCallback m_newUserJoinedCallback;

		private OnlineMultiplayerSessionJoinCallback m_joinSessionCallback;

		private FastList<byte> m_transportSendList = new FastList<byte>((int)OnlineMultiplayerConfig.MaxTransportMessageSize);

		private byte[] m_transportReceiveBuffer = new byte[OnlineMultiplayerConfig.MaxTransportMessageSize];

		private OnlineMultiplayerSessionTransportMessageHeader m_transportMessageHeader = new OnlineMultiplayerSessionTransportMessageHeader();

		private BitStreamWriter m_transportBitStreamWriter;

		private BitStreamReader m_transportBitStreamReader;

		private SteamOnlineMultiplayerSessionTransportCoordinator m_transportCoordinator = new SteamOnlineMultiplayerSessionTransportCoordinator();

		private float m_connectingToHostMaxGameTime;

		private SteamOnlineMultiplayerSessionUserId.TransportConnectionStatus m_connectingToHostTransportStatus;

		private List<OnlineMultiplayerLocalUserId> m_secondaryLocalUserIds = new List<OnlineMultiplayerLocalUserId>();

		private List<OnlineMultiplayerSessionJoinLocalUserData> m_localUserJoinData = new List<OnlineMultiplayerSessionJoinLocalUserData>((int)(OnlineMultiplayerConfig.MaxPlayers - 1));

		private List<OnlineMultiplayerSessionJoinRemoteUserData> m_remoteUserJoinData = new List<OnlineMultiplayerSessionJoinRemoteUserData>((int)(OnlineMultiplayerConfig.MaxPlayers - 1));

		public void Initialize(OnlineMultiplayerSessionPropertyCoordinator sessionPropertyCoordinator, OnlineMultiplayerTransportStats transportStats)
		{
			if (m_isInitialized || sessionPropertyCoordinator == null)
			{
				return;
			}
			try
			{
				if (SteamPlayerManager.Initialized)
				{
					m_transportCoordinator.Initialize();
					s_steamLobbyCreatedCallback = Callback<LobbyCreated_t>.Create(OnSteamLobbyCreated);
					s_steamLobbyJoinedCallback = Callback<LobbyEnter_t>.Create(OnSteamLobbyJoined);
					s_steamLobbyMembersChangedCallback = Callback<LobbyChatUpdate_t>.Create(OnSteamLobbyMembersChanged);
					m_transportStats = transportStats;
					m_sessionPropertyCoordinator = sessionPropertyCoordinator;
					m_transportBitStreamWriter = new BitStreamWriter(m_transportSendList);
					m_transportBitStreamReader = new BitStreamReader(m_transportReceiveBuffer);
					m_isInitialized = true;
				}
			}
			catch (Exception)
			{
			}
		}

		public void RegisterDisconnectionCallback(OnlineMultiplayerSessionDisconnectionCallback callback)
		{
			if (callback != null)
			{
				m_disconnectionCallbacks = (OnlineMultiplayerSessionDisconnectionCallback)Delegate.Combine(m_disconnectionCallbacks, callback);
			}
		}

		public void UnRegisterDisconnectionCallback(OnlineMultiplayerSessionDisconnectionCallback callback)
		{
			if (callback != null)
			{
				m_disconnectionCallbacks = (OnlineMultiplayerSessionDisconnectionCallback)Delegate.Remove(m_disconnectionCallbacks, callback);
			}
		}

		public void RegisterRemoteUserDisconnectionCallback(OnlineMultiplayerSessionRemoteUserDisconnectionCallback callback)
		{
			if (callback != null)
			{
				m_remoteUserDisconnectionCallbacks = (OnlineMultiplayerSessionRemoteUserDisconnectionCallback)Delegate.Combine(m_remoteUserDisconnectionCallbacks, callback);
			}
		}

		public void UnRegisterRemoteUserDisconnectionCallback(OnlineMultiplayerSessionRemoteUserDisconnectionCallback callback)
		{
			if (callback != null)
			{
				m_remoteUserDisconnectionCallbacks = (OnlineMultiplayerSessionRemoteUserDisconnectionCallback)Delegate.Remove(m_remoteUserDisconnectionCallbacks, callback);
			}
		}

		public void RegisterDataReceivedCallback(OnlineMultiplayerSessionDataReceivedCallback callback)
		{
			if (callback != null)
			{
				m_dataReceivedCallback = (OnlineMultiplayerSessionDataReceivedCallback)Delegate.Combine(m_dataReceivedCallback, callback);
			}
		}

		public void UnRegisterDataReceivedCallback(OnlineMultiplayerSessionDataReceivedCallback callback)
		{
			if (callback != null)
			{
				m_dataReceivedCallback = (OnlineMultiplayerSessionDataReceivedCallback)Delegate.Remove(m_dataReceivedCallback, callback);
			}
		}

		public void Update(float gameTimeAtStartOfFrame)
		{
			if (!m_isInitialized)
			{
				return;
			}
			m_gameTimeAtStartOfFrame = gameTimeAtStartOfFrame;
			switch (m_sessionStatus)
			{
			case SessionStatus.eCreating:
				UpdateCreatingLobby();
				break;
			case SessionStatus.eJoining:
				UpdateJoiningLobby();
				break;
			case SessionStatus.eConnectingToHost:
				UpdateConnectingToHost();
				break;
			case SessionStatus.eRunning:
				UpdateRunning();
				break;
			}
			if (m_leaveRequested)
			{
				if (m_lobbyId.IsValid())
				{
					try
					{
						SteamMatchmaking.LeaveLobby(m_lobbyId);
					}
					catch (Exception)
					{
					}
				}
				ResetToIdle();
			}
			m_transportCoordinator.Update(m_transportReceiveBuffer, OnlineMultiplayerConfig.MaxSocketIterationsPerUpdate);
		}

		public bool IsIdle()
		{
			return m_isInitialized && SessionStatus.eIdle == m_sessionStatus;
		}

		public bool IsHost()
		{
			return m_sessionStatus == SessionStatus.eRunning && m_hostSteamId == m_localPlayerUserId.m_steamId;
		}

		public bool Create(OnlineMultiplayerLocalUserId localUserId, List<OnlineMultiplayerSessionPropertyValue> sessionProperties, OnlineMultiplayerSessionVisibility visibility, string sessionName, OnlineMultiplayerSessionPlayTogetherHosting playtogetherHosting, OnlineMultiplayerSessionCreateCallback createCallback, OnlineMultiplayerSessionJoinDecisionCallback joinDecisionCallback, OnlineMultiplayerSessionUserJoinedCallback newUserJoinedCallback)
		{
			if (IsIdle())
			{
				if (m_sessionPropertyCoordinator.IsInitialized() && localUserId != null && sessionProperties != null && createCallback != null && joinDecisionCallback != null && newUserJoinedCallback != null && ValidateSessionProperties(sessionProperties))
				{
					try
					{
						if (SteamUser.BLoggedOn())
						{
							m_localPlayerUserId = localUserId;
							m_creatingPropertyValues = sessionProperties;
							m_sessionVisibility = visibility;
							m_createSessionCallback = createCallback;
							m_joinDecisionCallback = joinDecisionCallback;
							m_newUserJoinedCallback = newUserJoinedCallback;
							m_entryDelayMaxGameTime = m_gameTimeAtStartOfFrame + m_entryDelayInSeconds;
							m_sessionStatus = SessionStatus.eCreating;
							m_creatingSubStatus = CreatingSubStatus.eInitialDelay;
							return true;
						}
					}
					catch (Exception)
					{
					}
				}
				ResetToIdle();
			}
			return false;
		}

		public bool Join(List<OnlineMultiplayerSessionJoinLocalUserData> localUserData, OnlineMultiplayerSessionInvite sessionInvite, OnlineMultiplayerSessionJoinCallback joinCallback)
		{
			if (IsIdle())
			{
				if (ValidateJoinLocalUserData(localUserData) && sessionInvite != null && joinCallback != null)
				{
					try
					{
						if (sessionInvite.m_steamLobbyId.IsValid() && sessionInvite.m_steamLobbyId.IsLobby() && SteamUser.BLoggedOn())
						{
							m_joinSessionCallback = joinCallback;
							for (int i = 0; i < localUserData.Count; i++)
							{
								OnlineMultiplayerSessionJoinLocalUserData onlineMultiplayerSessionJoinLocalUserData = localUserData[i];
								m_localUserJoinData.Add(onlineMultiplayerSessionJoinLocalUserData);
								if (i == 0)
								{
									m_localPlayerUserId = onlineMultiplayerSessionJoinLocalUserData.Id;
								}
								else
								{
									m_secondaryLocalUserIds.Add(onlineMultiplayerSessionJoinLocalUserData.Id);
								}
							}
							m_lobbyIdToJoin = sessionInvite.m_steamLobbyId;
							m_entryDelayMaxGameTime = m_gameTimeAtStartOfFrame + m_entryDelayInSeconds;
							m_sessionStatus = SessionStatus.eJoining;
							m_joiningSubStatus = JoiningSubStatus.eInitialDelay;
							return true;
						}
					}
					catch (Exception)
					{
					}
				}
				ResetToIdle();
			}
			return false;
		}

		public bool Join(List<OnlineMultiplayerSessionJoinLocalUserData> localUserData, OnlineMultiplayerSessionEnumeratedRoom enumeratedSession, OnlineMultiplayerSessionJoinCallback joinCallback)
		{
			if (IsIdle())
			{
				if (ValidateJoinLocalUserData(localUserData) && enumeratedSession != null && joinCallback != null)
				{
					try
					{
						if (enumeratedSession.m_steamLobbyId.IsValid() && enumeratedSession.m_steamLobbyId.IsLobby() && SteamUser.BLoggedOn())
						{
							m_joinSessionCallback = joinCallback;
							for (int i = 0; i < localUserData.Count; i++)
							{
								OnlineMultiplayerSessionJoinLocalUserData onlineMultiplayerSessionJoinLocalUserData = localUserData[i];
								m_localUserJoinData.Add(onlineMultiplayerSessionJoinLocalUserData);
								if (i == 0)
								{
									m_localPlayerUserId = onlineMultiplayerSessionJoinLocalUserData.Id;
								}
								else
								{
									m_secondaryLocalUserIds.Add(onlineMultiplayerSessionJoinLocalUserData.Id);
								}
							}
							m_lobbyIdToJoin = enumeratedSession.m_steamLobbyId;
							m_entryDelayMaxGameTime = m_gameTimeAtStartOfFrame + m_entryDelayInSeconds;
							m_sessionStatus = SessionStatus.eJoining;
							m_joiningSubStatus = JoiningSubStatus.eInitialDelay;
							return true;
						}
					}
					catch (Exception)
					{
					}
				}
				ResetToIdle();
			}
			return false;
		}

		public OnlineMultiplayerNonPrimaryLocalUserChangeResult AddNonPrimaryLocalUser(OnlineMultiplayerLocalUserId localUserId, OnlineMultiplayerSessionAddNonPrimaryLocalUserCallback joinCallback)
		{
			OnlineMultiplayerNonPrimaryLocalUserChangeResult result = OnlineMultiplayerNonPrimaryLocalUserChangeResult.eNotPossible;
			try
			{
				if (localUserId != null && joinCallback != null && m_sessionStatus == SessionStatus.eRunning && !m_leaveRequested && IsHost() && !m_secondaryLocalUserIds.Exists((OnlineMultiplayerLocalUserId x) => x == localUserId) && m_userIdList.Count + m_secondaryLocalUserIds.Count < OnlineMultiplayerConfig.MaxPlayers)
				{
					m_secondaryLocalUserIds.Add(localUserId);
					result = OnlineMultiplayerNonPrimaryLocalUserChangeResult.eComplete;
				}
			}
			catch (Exception)
			{
			}
			return result;
		}

		public OnlineMultiplayerNonPrimaryLocalUserChangeResult RemoveNonPrimaryLocalUser(OnlineMultiplayerLocalUserId localUserId)
		{
			OnlineMultiplayerNonPrimaryLocalUserChangeResult result = OnlineMultiplayerNonPrimaryLocalUserChangeResult.eNotPossible;
			try
			{
				if (localUserId != null && m_sessionStatus == SessionStatus.eRunning && !m_leaveRequested && IsHost() && m_secondaryLocalUserIds.Exists((OnlineMultiplayerLocalUserId x) => x == localUserId))
				{
					m_secondaryLocalUserIds.Remove(localUserId);
					result = OnlineMultiplayerNonPrimaryLocalUserChangeResult.eComplete;
				}
			}
			catch (Exception)
			{
			}
			return result;
		}

		public bool Modify(List<OnlineMultiplayerSessionPropertyValue> sessionProperties, OnlineMultiplayerSessionVisibility visibility)
		{
			if (m_sessionStatus == SessionStatus.eRunning && !m_leaveRequested && IsHost())
			{
				try
				{
					if (sessionProperties != null && sessionProperties.Count > 0)
					{
						for (int i = 0; i < sessionProperties.Count; i++)
						{
							string name = sessionProperties[i].m_property.Name;
							string pchValue = sessionProperties[i].m_value.ToString();
							SteamMatchmaking.SetLobbyData(m_lobbyId, name, pchValue);
						}
					}
					if (visibility != m_sessionVisibility || m_forceModify)
					{
						m_sessionVisibility = visibility;
						SteamMatchmaking.SetLobbyJoinable(m_lobbyId, m_sessionVisibility != OnlineMultiplayerSessionVisibility.eClosed);
						ELobbyType eLobbyType = ELobbyType.k_ELobbyTypePrivate;
						OnlineMultiplayerSessionVisibility sessionVisibility = m_sessionVisibility;
						if (sessionVisibility == OnlineMultiplayerSessionVisibility.ePublic || sessionVisibility == OnlineMultiplayerSessionVisibility.eMatchmaking)
						{
							eLobbyType = ELobbyType.k_ELobbyTypePublic;
						}
						SteamMatchmaking.SetLobbyType(m_lobbyId, eLobbyType);
					}
					return true;
				}
				catch (Exception)
				{
				}
			}
			return false;
		}

		public bool AutoMatchmake(OnlineMultiplayerSessionJoinLocalUserData localUserData, List<OnlineMultiplayerSessionPropertyValue> hostingSessionProperties, string hostingSessionName, OnlineMultiplayerSessionJoinDecisionCallback hostingJoinDecisionCallback, OnlineMultiplayerSessionUserJoinedCallback hostingNewUserJoinedCallback, List<OnlineMultiplayerSessionPropertySearchValue> autoMatchingFilterParameters, OnlineMultiplayerSessionJoinCallback joinCallback)
		{
			return false;
		}

		public bool SendData(IOnlineMultiplayerSessionUserId recipientUserId, byte[] data, int dataSize, bool sendReliably)
		{
			if (m_sessionStatus == SessionStatus.eRunning && !m_leaveRequested && recipientUserId != null && data != null && dataSize > 0 && dataSize <= data.Length && dataSize <= OnlineMultiplayerConfig.MaxTransportMessageSize && !recipientUserId.IsLocal)
			{
				OnlineMultiplayerSessionUserId onlineMultiplayerSessionUserId = recipientUserId as OnlineMultiplayerSessionUserId;
				if (onlineMultiplayerSessionUserId != null && onlineMultiplayerSessionUserId.HasDirectTransportConnection() && m_transportCoordinator.SendData(onlineMultiplayerSessionUserId.m_steamId, data, dataSize, sendReliably))
				{
					onlineMultiplayerSessionUserId.m_steamLocalKeepaliveLastSendTime = m_gameTimeAtStartOfFrame;
					if (m_transportStats != null)
					{
						m_transportStats.Add(OnlineMultiplayerTransportStats.StatType.eDataSent, (uint)dataSize);
					}
					return true;
				}
			}
			return false;
		}

		public void Leave()
		{
			if (!m_isInitialized || !m_isInitialized || m_sessionStatus == SessionStatus.eIdle || m_leaveRequested)
			{
				return;
			}
			m_leaveRequested = true;
			for (int i = 0; i < m_userIdList.Count; i++)
			{
				OnlineMultiplayerSessionUserId onlineMultiplayerSessionUserId = m_userIdList[i];
				if (!onlineMultiplayerSessionUserId.IsLocal)
				{
					m_transportCoordinator.CloseConnection(onlineMultiplayerSessionUserId.m_steamId);
					onlineMultiplayerSessionUserId.m_steamLocalTransportConnectionStatus = SteamOnlineMultiplayerSessionUserId.TransportConnectionStatus.eConnectionDead;
				}
			}
			if (m_hostSteamIdDuringConnection.IsValid())
			{
				m_transportCoordinator.CloseConnection(m_hostSteamIdDuringConnection);
				m_hostSteamIdDuringConnection.Clear();
				m_connectingToHostTransportStatus = SteamOnlineMultiplayerSessionUserId.TransportConnectionStatus.eConnectionDead;
				m_connectingToHostMaxGameTime = 0f;
			}
			m_transportCoordinator.Close();
		}

		public void ShowSendInviteDialog(string msg)
		{
			if (m_sessionStatus == SessionStatus.eRunning && !m_leaveRequested && m_lobbyId.IsValid())
			{
				try
				{
					SteamFriends.ActivateGameOverlayInviteDialog(m_lobbyId);
				}
				catch (Exception)
				{
				}
			}
		}

		public IOnlineMultiplayerSessionUserId[] Members()
		{
			if (m_sessionStatus == SessionStatus.eRunning && !m_leaveRequested && m_userIdList.Count > 0)
			{
				return m_userIdList.ToArray();
			}
			return null;
		}

		public bool IsMemberAlready(OnlineMultiplayerSessionInvite pendingSessionInvite)
		{
			if (m_isInitialized && pendingSessionInvite != null && m_sessionStatus != SessionStatus.eIdle && !m_leaveRequested && m_lobbyId.IsValid() && m_lobbyId == pendingSessionInvite.m_steamLobbyId)
			{
				return true;
			}
			return false;
		}

		private void ResetToIdle()
		{
			m_localUserJoinData.Clear();
			m_remoteUserJoinData.Clear();
			m_secondaryLocalUserIds.Clear();
			m_sessionStatus = SessionStatus.eIdle;
			m_creatingSubStatus = CreatingSubStatus.eIdle;
			m_joiningSubStatus = JoiningSubStatus.eIdle;
			m_joiningLobbyResponse = EChatRoomEnterResponse.k_EChatRoomEnterResponseError;
			m_creatingPropertyValues = null;
			m_entryDelayMaxGameTime = 0f;
			m_localPlayerUserId = null;
			m_leaveRequested = false;
			m_forceModify = false;
			m_userIdList.Clear();
			m_uniqueUserIdCounter = OnlineMultiplayerSessionUserId.c_InvalidUniqueId;
			m_sessionVisibility = OnlineMultiplayerSessionVisibility.eClosed;
			m_createSessionCallback = null;
			m_joinDecisionCallback = null;
			m_newUserJoinedCallback = null;
			m_joinSessionCallback = null;
			m_connectingToHostMaxGameTime = 0f;
			m_connectingToHostTransportStatus = SteamOnlineMultiplayerSessionUserId.TransportConnectionStatus.eNotApplicable;
			m_hostSteamId.Clear();
			m_hostSteamIdDuringConnection.Clear();
			m_lobbyId.Clear();
			m_lobbyIdToJoin.Clear();
			m_transportCoordinator.Close();
		}

		private byte GenerateUniqueUserId()
		{
			bool flag = false;
			byte id = OnlineMultiplayerSessionUserId.c_InvalidUniqueId;
			do
			{
				flag = false;
				id = ++m_uniqueUserIdCounter;
				if (OnlineMultiplayerSessionUserId.c_InvalidUniqueId != id)
				{
					flag = !m_userIdList.Exists((OnlineMultiplayerSessionUserId x) => x.m_uniqueId == id);
				}
			}
			while (!flag);
			return id;
		}

		private bool ValidateSessionProperties(List<OnlineMultiplayerSessionPropertyValue> sessionProperties)
		{
			foreach (OnlineMultiplayerSessionPropertyId entry in Enum.GetValues(typeof(OnlineMultiplayerSessionPropertyId)))
			{
				if (!sessionProperties.Exists((OnlineMultiplayerSessionPropertyValue x) => x.m_property.Id == (uint)entry))
				{
					return false;
				}
			}
			return true;
		}

		private bool ValidateJoinLocalUserData(List<OnlineMultiplayerSessionJoinLocalUserData> localUserData)
		{
			bool flag = true;
			if (localUserData != null && localUserData.Count > 0 && localUserData.Count < OnlineMultiplayerConfig.MaxPlayers)
			{
				for (int i = 0; i < localUserData.Count; i++)
				{
					if (!flag)
					{
						break;
					}
					flag &= null != localUserData[i].Id;
					flag &= null != localUserData[i].GameData;
					flag &= 0 != localUserData[i].GameDataSize;
					flag &= localUserData[i].GameDataSize <= localUserData[i].GameData.Length;
				}
			}
			else
			{
				flag = false;
			}
			return flag;
		}

		private void UpdateCreatingLobby()
		{
			switch (m_creatingSubStatus)
			{
			case CreatingSubStatus.eInitialDelay:
				if (!(m_gameTimeAtStartOfFrame >= m_entryDelayMaxGameTime))
				{
					break;
				}
				if (!m_leaveRequested)
				{
					if (SteamUser.BLoggedOn())
					{
						try
						{
							if (SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePrivate, (int)OnlineMultiplayerConfig.MaxPlayers).m_SteamAPICall != 0)
							{
								m_entryDelayMaxGameTime = 0f;
								m_creatingSubStatus = CreatingSubStatus.eWaitingForSteamToCreateTheLobby;
							}
							else
							{
								OnLocalDisconnection(OnlineMultiplayerSessionDisconnectionResult.eGeneric);
							}
							break;
						}
						catch (Exception)
						{
							OnLocalDisconnection(OnlineMultiplayerSessionDisconnectionResult.eGeneric);
							break;
						}
					}
					OnLocalDisconnection(OnlineMultiplayerSessionDisconnectionResult.eGoneOffline);
				}
				else
				{
					ResetToIdle();
				}
				break;
			case CreatingSubStatus.eWaitingForSteamToCreateTheLobby:
				break;
			case CreatingSubStatus.eComplete:
				if (m_lobbyId.IsValid())
				{
					m_creatingSubStatus = CreatingSubStatus.eIdle;
					if (!m_leaveRequested)
					{
						if (ValidateSessionProperties(m_creatingPropertyValues))
						{
							m_hostSteamId = m_localPlayerUserId.m_steamId;
							m_sessionStatus = SessionStatus.eRunning;
							OnlineMultiplayerSessionUserId onlineMultiplayerSessionUserId = new OnlineMultiplayerSessionUserId();
							onlineMultiplayerSessionUserId.m_isHost = true;
							onlineMultiplayerSessionUserId.m_isLocal = true;
							onlineMultiplayerSessionUserId.m_uniqueId = GenerateUniqueUserId();
							onlineMultiplayerSessionUserId.m_displayName = m_localPlayerUserId.m_userName;
							onlineMultiplayerSessionUserId.m_steamId = m_localPlayerUserId.m_steamId;
							onlineMultiplayerSessionUserId.m_steamUserRestrictions = m_localPlayerUserId.m_steamUserRestrictions;
							m_userIdList.Add(onlineMultiplayerSessionUserId);
							m_forceModify = true;
							if (Modify(m_creatingPropertyValues, m_sessionVisibility))
							{
								m_forceModify = false;
								m_creatingPropertyValues = null;
								m_transportCoordinator.Open(m_lobbyId, OnTransportDisconnectionCallback, OnTransportDataCallback, OnTransportVoipDataCallback);
								try
								{
									OnlineMultiplayerReturnCode<OnlineMultiplayerSessionCreateResult> onlineMultiplayerReturnCode = new OnlineMultiplayerReturnCode<OnlineMultiplayerSessionCreateResult>();
									onlineMultiplayerReturnCode.m_returnCode = OnlineMultiplayerSessionCreateResult.eSuccess;
									OnlineMultiplayerReturnCode<OnlineMultiplayerSessionCreateResult> result = onlineMultiplayerReturnCode;
									m_createSessionCallback(result);
									break;
								}
								catch (Exception)
								{
									break;
								}
							}
							OnLocalDisconnection(OnlineMultiplayerSessionDisconnectionResult.eGeneric);
						}
						else
						{
							OnLocalDisconnection(OnlineMultiplayerSessionDisconnectionResult.eGeneric);
						}
					}
					else
					{
						Leave();
					}
				}
				else
				{
					OnLocalDisconnection(OnlineMultiplayerSessionDisconnectionResult.eGeneric);
				}
				break;
			}
		}

		private void UpdateJoiningLobby()
		{
			switch (m_joiningSubStatus)
			{
			case JoiningSubStatus.eInitialDelay:
				if (!(m_gameTimeAtStartOfFrame >= m_entryDelayMaxGameTime))
				{
					break;
				}
				if (!m_leaveRequested)
				{
					if (SteamUser.BLoggedOn())
					{
						try
						{
							if (SteamMatchmaking.JoinLobby(m_lobbyIdToJoin).m_SteamAPICall != 0)
							{
								m_entryDelayMaxGameTime = 0f;
								m_joiningSubStatus = JoiningSubStatus.eWaitingForSteamToJoinTheLobby;
							}
							else
							{
								OnLocalDisconnection(OnlineMultiplayerSessionDisconnectionResult.eGeneric);
							}
							break;
						}
						catch (Exception)
						{
							OnLocalDisconnection(OnlineMultiplayerSessionDisconnectionResult.eGeneric);
							break;
						}
					}
					OnLocalDisconnection(OnlineMultiplayerSessionDisconnectionResult.eGoneOffline);
				}
				else
				{
					ResetToIdle();
				}
				break;
			case JoiningSubStatus.eWaitingForSteamToJoinTheLobby:
				break;
			case JoiningSubStatus.eCompleteComplete:
				if (m_leaveRequested)
				{
					break;
				}
				m_joiningSubStatus = JoiningSubStatus.eIdle;
				if (m_lobbyId.IsValid())
				{
					OnlineMultiplayerSessionUserId onlineMultiplayerSessionUserId = new OnlineMultiplayerSessionUserId();
					onlineMultiplayerSessionUserId.m_isHost = false;
					onlineMultiplayerSessionUserId.m_isLocal = true;
					onlineMultiplayerSessionUserId.m_uniqueId = OnlineMultiplayerSessionUserId.c_InvalidUniqueId;
					onlineMultiplayerSessionUserId.m_displayName = m_localPlayerUserId.m_userName;
					onlineMultiplayerSessionUserId.m_steamId = m_localPlayerUserId.m_steamId;
					onlineMultiplayerSessionUserId.m_steamUserRestrictions = m_localPlayerUserId.m_steamUserRestrictions;
					m_userIdList.Add(onlineMultiplayerSessionUserId);
					try
					{
						CSteamID lobbyOwner = SteamMatchmaking.GetLobbyOwner(m_lobbyId);
						if (lobbyOwner != onlineMultiplayerSessionUserId.m_steamId)
						{
							m_hostSteamIdDuringConnection = lobbyOwner;
							m_hostSteamId = lobbyOwner;
						}
					}
					catch (Exception)
					{
					}
					if (m_hostSteamIdDuringConnection.IsValid() && m_transportCoordinator.Open(m_lobbyId, OnTransportDisconnectionCallback, OnTransportDataCallback, OnTransportVoipDataCallback))
					{
						try
						{
							SetupOutgoingMessage(TransportMessageTypes.eJoinRequest);
							m_transportBitStreamWriter.Write(OnlineMultiplayerConfig.CodeVersion, 32);
							onlineMultiplayerSessionUserId.Serialize(m_transportBitStreamWriter);
							if (OnlineMultiplayerSessionJoinUserDataHelper.Serialize(m_localUserJoinData, m_transportBitStreamWriter))
							{
								m_localUserJoinData.Clear();
								if (m_transportCoordinator.SendData(m_hostSteamIdDuringConnection, m_transportSendList._items, m_transportSendList.Count, true))
								{
									m_sessionStatus = SessionStatus.eConnectingToHost;
									m_connectingToHostMaxGameTime = m_gameTimeAtStartOfFrame + m_connectToHostMaxTimeInSeconds;
									m_connectingToHostTransportStatus = SteamOnlineMultiplayerSessionUserId.TransportConnectionStatus.eWaitingForJoinApprovalFromHost;
									break;
								}
							}
						}
						catch (Exception)
						{
						}
					}
					OnLocalDisconnection(OnlineMultiplayerSessionDisconnectionResult.eHostDisconnected);
				}
				else
				{
					OnLocalDisconnection(OnlineMultiplayerSessionDisconnectionResult.eGeneric);
				}
				break;
			}
		}

		private void UpdateConnectingToHost()
		{
			if (!m_leaveRequested && m_gameTimeAtStartOfFrame > m_connectingToHostMaxGameTime)
			{
				OnLocalDisconnection(OnlineMultiplayerSessionDisconnectionResult.eHostDisconnected);
			}
		}

		private void UpdateRunning()
		{
			if (m_leaveRequested)
			{
				return;
			}
			if (SteamUser.BLoggedOn())
			{
				for (int i = 0; i < m_userIdList.Count; i++)
				{
					OnlineMultiplayerSessionUserId onlineMultiplayerSessionUserId = m_userIdList[i];
					if (!onlineMultiplayerSessionUserId.IsLocal && onlineMultiplayerSessionUserId.HasDirectTransportConnection())
					{
						float num = m_gameTimeAtStartOfFrame - onlineMultiplayerSessionUserId.m_steamLocalKeepaliveLastSendTime;
						if (num > m_keepaliveMessageFrequencyInSeconds)
						{
							SendKeepaliveMessage(onlineMultiplayerSessionUserId);
						}
						float num2 = m_gameTimeAtStartOfFrame - onlineMultiplayerSessionUserId.m_steamLocalKeepaliveLastReceiveTime;
						if (num2 > (float)SteamOnlineMultiplayerSessionTransportCoordinator.DisconnectionTimeInSeconds * 1f)
						{
							OnUserDisconnected(onlineMultiplayerSessionUserId);
							break;
						}
					}
				}
			}
			else
			{
				OnLocalDisconnection(OnlineMultiplayerSessionDisconnectionResult.eGoneOffline);
			}
		}

		private void OnLocalDisconnection(OnlineMultiplayerSessionDisconnectionResult disconnectionReason)
		{
			if (!m_isInitialized)
			{
				return;
			}
			switch (m_sessionStatus)
			{
			case SessionStatus.eCreating:
				if (m_leaveRequested)
				{
					break;
				}
				try
				{
					OnlineMultiplayerSessionCreateResult returnCode2 = OnlineMultiplayerSessionCreateResult.eGenericFailure;
					switch (disconnectionReason)
					{
					case OnlineMultiplayerSessionDisconnectionResult.eLostNetwork:
						returnCode2 = OnlineMultiplayerSessionCreateResult.eLostNetwork;
						break;
					case OnlineMultiplayerSessionDisconnectionResult.eLoggedOut:
						returnCode2 = OnlineMultiplayerSessionCreateResult.eLoggedOut;
						break;
					case OnlineMultiplayerSessionDisconnectionResult.eGoneOffline:
						returnCode2 = OnlineMultiplayerSessionCreateResult.eGoneOffline;
						break;
					}
					OnlineMultiplayerReturnCode<OnlineMultiplayerSessionCreateResult> onlineMultiplayerReturnCode3 = new OnlineMultiplayerReturnCode<OnlineMultiplayerSessionCreateResult>();
					onlineMultiplayerReturnCode3.m_returnCode = returnCode2;
					OnlineMultiplayerReturnCode<OnlineMultiplayerSessionCreateResult> result3 = onlineMultiplayerReturnCode3;
					m_createSessionCallback(result3);
				}
				catch (Exception)
				{
				}
				Leave();
				break;
			case SessionStatus.eJoining:
			case SessionStatus.eConnectingToHost:
				if (m_leaveRequested)
				{
					break;
				}
				try
				{
					OnlineMultiplayerSessionJoinResult returnCode = OnlineMultiplayerSessionJoinResult.eGenericFailure;
					switch (disconnectionReason)
					{
					case OnlineMultiplayerSessionDisconnectionResult.eHostDisconnected:
						returnCode = OnlineMultiplayerSessionJoinResult.eNoHostConnection;
						break;
					case OnlineMultiplayerSessionDisconnectionResult.eLostNetwork:
						returnCode = OnlineMultiplayerSessionJoinResult.eLostNetwork;
						break;
					case OnlineMultiplayerSessionDisconnectionResult.eGoneOffline:
						returnCode = OnlineMultiplayerSessionJoinResult.eGoneOffline;
						break;
					case OnlineMultiplayerSessionDisconnectionResult.eGeneric:
						switch (m_joiningLobbyResponse)
						{
						case EChatRoomEnterResponse.k_EChatRoomEnterResponseFull:
							returnCode = OnlineMultiplayerSessionJoinResult.eFull;
							break;
						case EChatRoomEnterResponse.k_EChatRoomEnterResponseDoesntExist:
							returnCode = OnlineMultiplayerSessionJoinResult.eNoLongerExists;
							break;
						}
						break;
					}
					OnlineMultiplayerReturnCode<OnlineMultiplayerSessionJoinResult> onlineMultiplayerReturnCode2 = new OnlineMultiplayerReturnCode<OnlineMultiplayerSessionJoinResult>();
					onlineMultiplayerReturnCode2.m_returnCode = returnCode;
					OnlineMultiplayerReturnCode<OnlineMultiplayerSessionJoinResult> result2 = onlineMultiplayerReturnCode2;
					m_joinSessionCallback(result2, null, 0);
				}
				catch (Exception)
				{
				}
				Leave();
				break;
			case SessionStatus.eRunning:
				if (!m_leaveRequested)
				{
					try
					{
						OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult> onlineMultiplayerReturnCode = new OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult>();
						onlineMultiplayerReturnCode.m_returnCode = disconnectionReason;
						OnlineMultiplayerReturnCode<OnlineMultiplayerSessionDisconnectionResult> result = onlineMultiplayerReturnCode;
						m_disconnectionCallbacks(result);
					}
					catch (Exception)
					{
					}
					Leave();
				}
				break;
			}
		}

		private void OnUserDisconnected(OnlineMultiplayerSessionUserId userId)
		{
			if (m_leaveRequested)
			{
				return;
			}
			SessionStatus sessionStatus = m_sessionStatus;
			if (sessionStatus != SessionStatus.eRunning || userId == null)
			{
				return;
			}
			m_transportCoordinator.CloseConnection(userId.m_steamId);
			userId.m_steamLocalTransportConnectionStatus = SteamOnlineMultiplayerSessionUserId.TransportConnectionStatus.eConnectionDead;
			if (IsHost())
			{
				m_userIdList.Remove(userId);
				try
				{
					m_remoteUserDisconnectionCallbacks(userId);
				}
				catch (Exception)
				{
				}
				SetupOutgoingMessage(TransportMessageTypes.eUserDisconnected);
				m_transportBitStreamWriter.Write(userId.m_uniqueId, 8);
				for (int i = 0; i < m_userIdList.Count; i++)
				{
					if (!m_userIdList[i].m_isLocal)
					{
						m_transportCoordinator.SendData(m_userIdList[i].m_steamId, m_transportSendList._items, m_transportSendList.Count, true);
					}
				}
			}
			else if (userId.IsHost)
			{
				OnlineMultiplayerSessionDisconnectionResult disconnectionReason = ((!SteamUser.BLoggedOn()) ? OnlineMultiplayerSessionDisconnectionResult.eGoneOffline : OnlineMultiplayerSessionDisconnectionResult.eHostDisconnected);
				OnLocalDisconnection(disconnectionReason);
			}
		}

		private void SendKeepaliveMessage(OnlineMultiplayerSessionUserId targetUserId)
		{
			if (targetUserId != null && !targetUserId.IsLocal)
			{
				SetupOutgoingMessage(TransportMessageTypes.eKeepalive);
				m_transportBitStreamWriter.Write(0, 8);
				targetUserId.m_steamLocalKeepaliveLastSendTime = m_gameTimeAtStartOfFrame;
				if (m_transportCoordinator.SendData(targetUserId.m_steamId, m_transportSendList._items, m_transportSendList.Count, false) && m_transportStats != null)
				{
					m_transportStats.Add(OnlineMultiplayerTransportStats.StatType.eDataSent, (uint)m_transportSendList.Count);
				}
			}
		}

		private void OnSteamLobbyCreated(LobbyCreated_t param)
		{
			if (!m_isInitialized)
			{
				return;
			}
			CSteamID cSteamID = new CSteamID(param.m_ulSteamIDLobby);
			if (m_sessionStatus == SessionStatus.eCreating && m_creatingSubStatus == CreatingSubStatus.eWaitingForSteamToCreateTheLobby && !m_leaveRequested)
			{
				m_creatingSubStatus = CreatingSubStatus.eComplete;
				if (param.m_eResult == EResult.k_EResultOK && cSteamID.IsValid() && cSteamID.IsLobby())
				{
					m_lobbyId = cSteamID;
					return;
				}
			}
			if (cSteamID.IsValid())
			{
				try
				{
					SteamMatchmaking.LeaveLobby(cSteamID);
				}
				catch (Exception)
				{
				}
			}
		}

		private void OnSteamLobbyJoined(LobbyEnter_t param)
		{
			if (!m_isInitialized)
			{
				return;
			}
			bool flag = false;
			CSteamID cSteamID = new CSteamID(param.m_ulSteamIDLobby);
			if (m_sessionStatus == SessionStatus.eJoining && m_joiningSubStatus == JoiningSubStatus.eWaitingForSteamToJoinTheLobby && !m_leaveRequested)
			{
				if (cSteamID.IsValid() && cSteamID.IsLobby())
				{
					if (cSteamID == m_lobbyIdToJoin)
					{
						m_joiningSubStatus = JoiningSubStatus.eCompleteComplete;
						m_joiningLobbyResponse = (EChatRoomEnterResponse)param.m_EChatRoomEnterResponse;
						if (param.m_EChatRoomEnterResponse == 1)
						{
							m_lobbyId = cSteamID;
							return;
						}
					}
					else
					{
						flag = param.m_EChatRoomEnterResponse == 1;
					}
				}
			}
			else if (m_lobbyId.IsValid() && m_lobbyId == cSteamID)
			{
				return;
			}
			if (cSteamID.IsValid() && flag)
			{
				try
				{
					SteamMatchmaking.LeaveLobby(cSteamID);
				}
				catch (Exception)
				{
				}
			}
		}

		private void OnSteamLobbyMembersChanged(LobbyChatUpdate_t param)
		{
			if (!m_isInitialized || m_leaveRequested)
			{
				return;
			}
			EChatMemberStateChange eChatMemberStateChange = EChatMemberStateChange.k_EChatMemberStateChangeLeft | EChatMemberStateChange.k_EChatMemberStateChangeDisconnected | EChatMemberStateChange.k_EChatMemberStateChangeKicked | EChatMemberStateChange.k_EChatMemberStateChangeBanned;
			if (m_sessionStatus == SessionStatus.eRunning)
			{
				if (param.m_ulSteamIDLobby == m_lobbyId.m_SteamID && ((uint)eChatMemberStateChange & param.m_rgfChatMemberStateChange) != 0)
				{
					OnlineMultiplayerSessionUserId onlineMultiplayerSessionUserId = m_userIdList.Find((OnlineMultiplayerSessionUserId x) => x.m_steamId.m_SteamID == param.m_ulSteamIDUserChanged);
					if (onlineMultiplayerSessionUserId != null)
					{
						OnUserDisconnected(onlineMultiplayerSessionUserId);
					}
				}
			}
			else if (m_sessionStatus == SessionStatus.eConnectingToHost && param.m_ulSteamIDLobby == m_lobbyId.m_SteamID && ((uint)eChatMemberStateChange & param.m_rgfChatMemberStateChange) != 0 && param.m_ulSteamIDUserChanged == m_hostSteamIdDuringConnection.m_SteamID)
			{
				OnLocalDisconnection(OnlineMultiplayerSessionDisconnectionResult.eHostDisconnected);
			}
		}

		private void SetupOutgoingMessage(TransportMessageTypes msgType)
		{
			m_transportSendList.Clear();
			m_transportBitStreamWriter.Reset(m_transportSendList);
			m_transportMessageHeader.IsGameMessage = false;
			m_transportMessageHeader.MessageTypeId = (byte)msgType;
			m_transportMessageHeader.Serialize(m_transportBitStreamWriter);
		}

		private void ReceiveJoinRequestMessage(CSteamID fromSteamId)
		{
			if (!IsHost())
			{
				return;
			}
			OnlineMultiplayerSessionJoinResult onlineMultiplayerSessionJoinResult = OnlineMultiplayerSessionJoinResult.eGenericFailure;
			uint num = m_transportBitStreamReader.ReadUInt32(32);
			if (OnlineMultiplayerConfig.CodeVersion == num)
			{
				if (m_userIdList.Count + m_secondaryLocalUserIds.Count < OnlineMultiplayerConfig.MaxPlayers)
				{
					byte b = GenerateUniqueUserId();
					OnlineMultiplayerSessionUserId onlineMultiplayerSessionUserId = new OnlineMultiplayerSessionUserId();
					onlineMultiplayerSessionUserId.Deserialize(m_transportBitStreamReader);
					onlineMultiplayerSessionUserId.m_steamLocalTransportConnectionStatus = SteamOnlineMultiplayerSessionUserId.TransportConnectionStatus.eConnectionActive;
					onlineMultiplayerSessionUserId.m_uniqueId = b;
					onlineMultiplayerSessionUserId.m_steamLocalKeepaliveLastSendTime = m_gameTimeAtStartOfFrame;
					onlineMultiplayerSessionUserId.m_steamLocalKeepaliveLastReceiveTime = m_gameTimeAtStartOfFrame;
					if (OnlineMultiplayerSessionJoinUserDataHelper.Deserialize(m_transportBitStreamReader, m_remoteUserJoinData))
					{
						byte[] replyData = null;
						int replyDataSize = 0;
						try
						{
							onlineMultiplayerSessionJoinResult = m_joinDecisionCallback(onlineMultiplayerSessionUserId, m_remoteUserJoinData, out replyData, out replyDataSize);
						}
						catch (Exception)
						{
						}
						m_remoteUserJoinData.Clear();
						if (onlineMultiplayerSessionJoinResult == OnlineMultiplayerSessionJoinResult.eSuccess)
						{
							SetupOutgoingMessage(TransportMessageTypes.eJoinRequestReply);
							m_transportBitStreamWriter.Write(0, 8);
							m_transportBitStreamWriter.Write(b, 8);
							m_transportBitStreamWriter.Write((uint)m_userIdList.Count, 8);
							for (int i = 0; i < m_userIdList.Count; i++)
							{
								m_userIdList[i].Serialize(m_transportBitStreamWriter);
							}
							if (replyData != null && replyDataSize > 0)
							{
								m_transportBitStreamWriter.Write((uint)replyDataSize, 32);
								for (int j = 0; j < replyDataSize; j++)
								{
									m_transportBitStreamWriter.Write(replyData[j], 8);
								}
							}
							else
							{
								uint bits = 0u;
								m_transportBitStreamWriter.Write(bits, 32);
							}
							m_transportCoordinator.SendData(fromSteamId, m_transportSendList._items, m_transportSendList.Count, true);
							SetupOutgoingMessage(TransportMessageTypes.eUserJoined);
							onlineMultiplayerSessionUserId.Serialize(m_transportBitStreamWriter);
							for (int k = 0; k < m_userIdList.Count; k++)
							{
								if (!m_userIdList[k].m_isLocal)
								{
									m_transportCoordinator.SendData(m_userIdList[k].m_steamId, m_transportSendList._items, m_transportSendList.Count, true);
								}
							}
							m_userIdList.Add(onlineMultiplayerSessionUserId);
							try
							{
								m_newUserJoinedCallback(onlineMultiplayerSessionUserId);
								return;
							}
							catch (Exception)
							{
								return;
							}
						}
					}
					else
					{
						onlineMultiplayerSessionJoinResult = OnlineMultiplayerSessionJoinResult.eNoHostConnection;
					}
				}
				else
				{
					onlineMultiplayerSessionJoinResult = OnlineMultiplayerSessionJoinResult.eFull;
				}
			}
			else
			{
				onlineMultiplayerSessionJoinResult = OnlineMultiplayerSessionJoinResult.eCodeVersionMismatch;
			}
			SetupOutgoingMessage(TransportMessageTypes.eJoinRequestReply);
			m_transportBitStreamWriter.Write((byte)onlineMultiplayerSessionJoinResult, 8);
			m_transportCoordinator.SendData(fromSteamId, m_transportSendList._items, m_transportSendList.Count, true);
		}

		private void ReceiveJoinRequestReplyMessage(CSteamID fromSteamId)
		{
			if (m_connectingToHostTransportStatus != SteamOnlineMultiplayerSessionUserId.TransportConnectionStatus.eWaitingForJoinApprovalFromHost || !(m_hostSteamIdDuringConnection == fromSteamId))
			{
				return;
			}
			OnlineMultiplayerSessionJoinResult onlineMultiplayerSessionJoinResult = (OnlineMultiplayerSessionJoinResult)m_transportBitStreamReader.ReadByte(8);
			string commandLineArgument = GetCommandLineArgument("-p2p");
			if (!string.IsNullOrEmpty(commandLineArgument) && string.Compare(commandLineArgument, "fail") == 0)
			{
				onlineMultiplayerSessionJoinResult = OnlineMultiplayerSessionJoinResult.eGenericFailure;
			}
			if (onlineMultiplayerSessionJoinResult == OnlineMultiplayerSessionJoinResult.eSuccess)
			{
				m_userIdList[0].m_uniqueId = m_transportBitStreamReader.ReadByte(8);
				uint num = m_transportBitStreamReader.ReadUInt32(8);
				for (uint num2 = 0u; num2 < num; num2++)
				{
					OnlineMultiplayerSessionUserId onlineMultiplayerSessionUserId = new OnlineMultiplayerSessionUserId();
					onlineMultiplayerSessionUserId.Deserialize(m_transportBitStreamReader);
					if (onlineMultiplayerSessionUserId.IsHost)
					{
						onlineMultiplayerSessionUserId.m_steamLocalTransportConnectionStatus = SteamOnlineMultiplayerSessionUserId.TransportConnectionStatus.eConnectionActive;
						onlineMultiplayerSessionUserId.m_steamLocalKeepaliveLastSendTime = m_gameTimeAtStartOfFrame;
						onlineMultiplayerSessionUserId.m_steamLocalKeepaliveLastReceiveTime = m_gameTimeAtStartOfFrame;
					}
					m_userIdList.Add(onlineMultiplayerSessionUserId);
				}
				byte[] array = null;
				int num3 = (int)m_transportBitStreamReader.ReadUInt32(32);
				if (num3 > 0)
				{
					array = new byte[num3];
					for (int i = 0; i < num3; i++)
					{
						array[i] = m_transportBitStreamReader.ReadByte(8);
					}
				}
				m_connectingToHostMaxGameTime = 0f;
				m_hostSteamIdDuringConnection.Clear();
				m_connectingToHostTransportStatus = SteamOnlineMultiplayerSessionUserId.TransportConnectionStatus.eNotApplicable;
				m_sessionStatus = SessionStatus.eRunning;
				try
				{
					OnlineMultiplayerReturnCode<OnlineMultiplayerSessionJoinResult> onlineMultiplayerReturnCode = new OnlineMultiplayerReturnCode<OnlineMultiplayerSessionJoinResult>();
					onlineMultiplayerReturnCode.m_returnCode = onlineMultiplayerSessionJoinResult;
					OnlineMultiplayerReturnCode<OnlineMultiplayerSessionJoinResult> result = onlineMultiplayerReturnCode;
					m_joinSessionCallback(result, array, num3);
					return;
				}
				catch (Exception)
				{
					return;
				}
			}
			try
			{
				OnlineMultiplayerReturnCode<OnlineMultiplayerSessionJoinResult> onlineMultiplayerReturnCode = new OnlineMultiplayerReturnCode<OnlineMultiplayerSessionJoinResult>();
				onlineMultiplayerReturnCode.m_returnCode = onlineMultiplayerSessionJoinResult;
				OnlineMultiplayerReturnCode<OnlineMultiplayerSessionJoinResult> result2 = onlineMultiplayerReturnCode;
				m_joinSessionCallback(result2, null, 0);
			}
			catch (Exception)
			{
			}
			Leave();
		}

		private void ReceiveUserJoinedMessage(CSteamID fromSteamId)
		{
			OnlineMultiplayerSessionUserId onlineMultiplayerSessionUserId = m_userIdList.Find((OnlineMultiplayerSessionUserId x) => x.m_steamId == fromSteamId);
			if (onlineMultiplayerSessionUserId != null && onlineMultiplayerSessionUserId.IsHost)
			{
				OnlineMultiplayerSessionUserId onlineMultiplayerSessionUserId2 = new OnlineMultiplayerSessionUserId();
				onlineMultiplayerSessionUserId2.Deserialize(m_transportBitStreamReader);
				onlineMultiplayerSessionUserId2.m_steamLocalTransportConnectionStatus = SteamOnlineMultiplayerSessionUserId.TransportConnectionStatus.eWaitingToStartClientConnection;
				m_userIdList.Add(onlineMultiplayerSessionUserId2);
			}
		}

		private void ReceiveUserDisconnectedMessage(CSteamID fromSteamId)
		{
			OnlineMultiplayerSessionUserId onlineMultiplayerSessionUserId = m_userIdList.Find((OnlineMultiplayerSessionUserId x) => x.m_steamId == fromSteamId);
			if (onlineMultiplayerSessionUserId != null && onlineMultiplayerSessionUserId.IsHost)
			{
				byte uniqueId = m_transportBitStreamReader.ReadByte(8);
				OnlineMultiplayerSessionUserId onlineMultiplayerSessionUserId2 = m_userIdList.Find((OnlineMultiplayerSessionUserId x) => x.m_uniqueId == uniqueId);
				if (onlineMultiplayerSessionUserId2 != null)
				{
					m_transportCoordinator.CloseConnection(onlineMultiplayerSessionUserId2.m_steamId);
					m_userIdList.Remove(onlineMultiplayerSessionUserId2);
				}
			}
		}

		private void OnTransportDisconnectionCallback(CSteamID steamId)
		{
			if (m_leaveRequested)
			{
				return;
			}
			OnlineMultiplayerSessionUserId onlineMultiplayerSessionUserId = null;
			switch (m_sessionStatus)
			{
			case SessionStatus.eConnectingToHost:
				OnLocalDisconnection(OnlineMultiplayerSessionDisconnectionResult.eHostDisconnected);
				break;
			case SessionStatus.eRunning:
				onlineMultiplayerSessionUserId = m_userIdList.Find((OnlineMultiplayerSessionUserId x) => x.m_steamId == steamId);
				if (onlineMultiplayerSessionUserId != null)
				{
					OnUserDisconnected(onlineMultiplayerSessionUserId);
				}
				break;
			}
		}

		private void OnTransportDataCallback(CSteamID fromSteamId, byte[] data, int dataSizeInBytes)
		{
			if (m_leaveRequested)
			{
				return;
			}
			try
			{
				if (m_transportStats != null)
				{
					m_transportStats.Add(OnlineMultiplayerTransportStats.StatType.eDataReceived, (uint)dataSizeInBytes);
				}
				m_transportBitStreamReader.Reset(data, dataSizeInBytes);
				m_transportMessageHeader.Deserialize(m_transportBitStreamReader);
				OnlineMultiplayerSessionUserId onlineMultiplayerSessionUserId = m_userIdList.Find((OnlineMultiplayerSessionUserId x) => x.m_steamId == fromSteamId);
				if (onlineMultiplayerSessionUserId != null)
				{
					onlineMultiplayerSessionUserId.m_steamLocalKeepaliveLastReceiveTime = m_gameTimeAtStartOfFrame;
				}
				if (m_transportMessageHeader.IsGameMessage)
				{
					if (onlineMultiplayerSessionUserId != null)
					{
						m_dataReceivedCallback(onlineMultiplayerSessionUserId, data, dataSizeInBytes);
					}
					return;
				}
				switch ((TransportMessageTypes)m_transportMessageHeader.MessageTypeId)
				{
				case TransportMessageTypes.eJoinRequest:
					ReceiveJoinRequestMessage(fromSteamId);
					break;
				case TransportMessageTypes.eJoinRequestReply:
					ReceiveJoinRequestReplyMessage(fromSteamId);
					break;
				case TransportMessageTypes.eUserJoined:
					ReceiveUserJoinedMessage(fromSteamId);
					break;
				case TransportMessageTypes.eUserDisconnected:
					ReceiveUserDisconnectedMessage(fromSteamId);
					break;
				case TransportMessageTypes.eKeepalive:
					break;
				}
			}
			catch (Exception)
			{
			}
		}

		private void OnTransportVoipDataCallback(CSteamID fromSteamId, byte[] data, int dataSiszeInBytes)
		{
		}

		private string GetCommandLineArgument(string Key)
		{
			string[] commandLineArgs = Environment.GetCommandLineArgs();
			for (int i = 0; i < commandLineArgs.Length; i++)
			{
				if (commandLineArgs[i].Contains(Key))
				{
					string[] array = commandLineArgs[i].Split(':');
					if (array.Length == 2)
					{
						return array[1];
					}
				}
			}
			return null;
		}
	}
}
