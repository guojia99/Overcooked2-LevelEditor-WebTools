namespace Team17.Online
{
	public interface IOnlineMultiplayerConnectionModeCoordinator
	{
		string DebugStatus();

		bool IsIdle();

		void RegisterErrorCallback(OnlineMultiplayerConnectionModeErrorCallback callback);

		void UnRegisterErrorCallback(OnlineMultiplayerConnectionModeErrorCallback callback);

		OnlineMultiplayerConnectionMode Mode();

		bool Connect(GamepadUser localUser, OnlineMultiplayerConnectionMode mode, OnlineMultiplayerConnectionModeConnectCallback connectCallback);

		void Disconnect();
	}
}
