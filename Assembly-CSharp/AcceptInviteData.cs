using Team17.Online;

public class AcceptInviteData
{
	public enum LocalUsersChoice
	{
		eNotChosenYet = 0,
		ePrimary = 1,
		eAll = 2
	}

	public OnlineMultiplayerSessionInvite Invite;

	public GamepadUser User;

	public bool FromIIS;

	public LocalUsersChoice JoinLocalUsersChoice;
}
