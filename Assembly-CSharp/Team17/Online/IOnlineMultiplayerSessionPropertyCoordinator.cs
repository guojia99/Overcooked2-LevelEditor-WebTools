namespace Team17.Online
{
	public interface IOnlineMultiplayerSessionPropertyCoordinator
	{
		bool IsInitialized();

		IOnlineMultiplayerSessionProperty FindProperty(OnlineMultiplayerSessionPropertyId id);
	}
}
