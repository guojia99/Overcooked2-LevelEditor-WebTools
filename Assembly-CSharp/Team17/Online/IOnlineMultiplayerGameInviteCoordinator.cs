namespace Team17.Online
{
	public interface IOnlineMultiplayerGameInviteCoordinator
	{
		OnlineMultiplayerSessionInvite InviteAccepted();

		bool HasPendingAcceptedInvite();

		OnlineMultiplayerSessionPlayTogetherHosting PlayTogetherHosting();
	}
}
