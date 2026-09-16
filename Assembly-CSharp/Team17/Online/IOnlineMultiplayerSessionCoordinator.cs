using System.Collections.Generic;

namespace Team17.Online
{
	public interface IOnlineMultiplayerSessionCoordinator
	{
		bool IsIdle();

		void RegisterDisconnectionCallback(OnlineMultiplayerSessionDisconnectionCallback callback);

		void UnRegisterDisconnectionCallback(OnlineMultiplayerSessionDisconnectionCallback callback);

		void RegisterRemoteUserDisconnectionCallback(OnlineMultiplayerSessionRemoteUserDisconnectionCallback callback);

		void UnRegisterRemoteUserDisconnectionCallback(OnlineMultiplayerSessionRemoteUserDisconnectionCallback callback);

		void RegisterDataReceivedCallback(OnlineMultiplayerSessionDataReceivedCallback callback);

		void UnRegisterDataReceivedCallback(OnlineMultiplayerSessionDataReceivedCallback callback);

		bool IsHost();

		bool Create(OnlineMultiplayerLocalUserId localUserId, List<OnlineMultiplayerSessionPropertyValue> sessionProperties, OnlineMultiplayerSessionVisibility visibility, string sessionName, OnlineMultiplayerSessionPlayTogetherHosting playtogetherHosting, OnlineMultiplayerSessionCreateCallback createCallback, OnlineMultiplayerSessionJoinDecisionCallback joinDecisionCallback, OnlineMultiplayerSessionUserJoinedCallback newUserJoinedCallback);

		bool Join(List<OnlineMultiplayerSessionJoinLocalUserData> localUserData, OnlineMultiplayerSessionInvite sessionInvite, OnlineMultiplayerSessionJoinCallback joinCallback);

		bool Join(List<OnlineMultiplayerSessionJoinLocalUserData> localUserData, OnlineMultiplayerSessionEnumeratedRoom enumeratedSession, OnlineMultiplayerSessionJoinCallback joinCallback);

		OnlineMultiplayerNonPrimaryLocalUserChangeResult AddNonPrimaryLocalUser(OnlineMultiplayerLocalUserId localUserId, OnlineMultiplayerSessionAddNonPrimaryLocalUserCallback joinCallback);

		OnlineMultiplayerNonPrimaryLocalUserChangeResult RemoveNonPrimaryLocalUser(OnlineMultiplayerLocalUserId localUserId);

		bool Modify(List<OnlineMultiplayerSessionPropertyValue> sessionProperties, OnlineMultiplayerSessionVisibility visibility);

		bool AutoMatchmake(OnlineMultiplayerSessionJoinLocalUserData localUserData, List<OnlineMultiplayerSessionPropertyValue> hostingSessionProperties, string hostingSessionName, OnlineMultiplayerSessionJoinDecisionCallback hostingJoinDecisionCallback, OnlineMultiplayerSessionUserJoinedCallback hostingNewUserJoinedCallback, List<OnlineMultiplayerSessionPropertySearchValue> autoMatchingFilterParameters, OnlineMultiplayerSessionJoinCallback joinCallback);

		bool SendData(IOnlineMultiplayerSessionUserId recipientUserId, byte[] data, int dataSize, bool sendReliably);

		void ShowSendInviteDialog(string msg);

		void Leave();

		IOnlineMultiplayerSessionUserId[] Members();

		bool IsMemberAlready(OnlineMultiplayerSessionInvite pendingSessionInvite);
	}
}
