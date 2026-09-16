using Team17.Online;

public interface InviteHandler
{
	void Start();

	void Stop();

	void Update();

	void HandleAcceptedInvite(AcceptInviteData invite);

	void HandlePlayTogetherHost(OnlineMultiplayerSessionPlayTogetherHosting host);

	bool IsBusy();

	bool IsAwaitingUserInput();
}
