namespace Team17.Online
{
	public interface IOnlineAvatarImageCoordinator
	{
		bool RequestAvatarImage(GamepadUser localUser, AvatarImageRequestCompletionCallback completionCallback, out ulong uniqueRequestId);

		bool RequestAvatarImage(GamepadUser primaryLocalUser, OnlineUserPlatformId remoteUser, AvatarImageRequestCompletionCallback completionCallback, out ulong uniqueRequestId);
	}
}
