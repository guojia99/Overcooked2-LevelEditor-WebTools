using System.Collections.Generic;

namespace Team17.Online
{
	public delegate OnlineMultiplayerSessionJoinResult OnlineMultiplayerSessionJoinDecisionCallback(IOnlineMultiplayerSessionUserId remotePrimaryUserId, List<OnlineMultiplayerSessionJoinRemoteUserData> remoteUserData, out byte[] replyData, out int replyDataSize);
}
