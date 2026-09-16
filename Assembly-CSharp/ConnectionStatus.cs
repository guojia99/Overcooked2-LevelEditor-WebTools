using Team17.Online;

internal class ConnectionStatus
{
	private static IOnlineMultiplayerSessionCoordinator m_iOnlineMultiplayerSessionCoordinator;

	private static IOnlineMultiplayerConnectionModeCoordinator m_connectionModeCoordinator;

	public void Initialise()
	{
		IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
		m_iOnlineMultiplayerSessionCoordinator = onlinePlatformManager.OnlineMultiplayerSessionCoordinator();
		m_connectionModeCoordinator = onlinePlatformManager.OnlineMultiplayerConnectionModeCoordinator();
	}

	public void Shutdown()
	{
		m_iOnlineMultiplayerSessionCoordinator = null;
	}

	public static bool IsInSession()
	{
		return m_iOnlineMultiplayerSessionCoordinator != null && null != m_iOnlineMultiplayerSessionCoordinator.Members();
	}

	public static bool IsHost()
	{
		return m_iOnlineMultiplayerSessionCoordinator != null && m_iOnlineMultiplayerSessionCoordinator.IsHost();
	}

	public static OnlineMultiplayerConnectionMode CurrentConnectionMode()
	{
		OnlineMultiplayerConnectionMode result = OnlineMultiplayerConnectionMode.eNone;
		if (m_connectionModeCoordinator != null)
		{
			result = m_connectionModeCoordinator.Mode();
		}
		return result;
	}

	public static bool HasConnectionModes()
	{
		return m_connectionModeCoordinator != null;
	}
}
