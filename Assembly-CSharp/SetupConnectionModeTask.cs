using Team17.Online;

public class SetupConnectionModeTask : IMultiplayerTask
{
	private OnlineMultiplayerConnectionMode m_connectionMode;

	private IOnlineMultiplayerConnectionModeCoordinator m_connectionModeCoordinator;

	private ConnectionModeStatus m_Status = new ConnectionModeStatus();

	private GamepadUser m_user;

	private object m_PassthroughObject;

	public void Initialise(GamepadUser user, OnlineMultiplayerConnectionMode mode)
	{
		m_user = user;
		m_connectionMode = mode;
		m_Status.Progress = eConnectionModeSwitchProgress.NotStarted;
		m_Status.Result = eConnectionModeSwitchResult.NotAvailableYet;
	}

	public void Start(object startData)
	{
		m_PassthroughObject = startData;
		IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
		m_connectionModeCoordinator = onlinePlatformManager.OnlineMultiplayerConnectionModeCoordinator();
		if (m_connectionModeCoordinator == null)
		{
			m_Status.Progress = eConnectionModeSwitchProgress.Complete;
			m_Status.Result = eConnectionModeSwitchResult.Success;
			return;
		}
		OnlineMultiplayerConnectionMode onlineMultiplayerConnectionMode = m_connectionModeCoordinator.Mode();
		if (onlineMultiplayerConnectionMode == m_connectionMode)
		{
			m_Status.Progress = eConnectionModeSwitchProgress.Complete;
			m_Status.Result = eConnectionModeSwitchResult.Success;
			return;
		}
		if (onlineMultiplayerConnectionMode != OnlineMultiplayerConnectionMode.eNone)
		{
			m_connectionModeCoordinator.Disconnect();
		}
		m_Status.Progress = eConnectionModeSwitchProgress.NotStarted;
		m_Status.Result = eConnectionModeSwitchResult.NotAvailableYet;
		TryStart();
	}

	public void Stop()
	{
		m_Status.Progress = eConnectionModeSwitchProgress.NotStarted;
		m_Status.Result = eConnectionModeSwitchResult.NotAvailableYet;
	}

	private void TryStart()
	{
		if (m_connectionModeCoordinator.IsIdle())
		{
			if (m_connectionModeCoordinator.Connect(m_user, m_connectionMode, OnlineMultiplayerConnectionModeConnectCallback))
			{
				m_Status.Progress = eConnectionModeSwitchProgress.InProgress;
				m_Status.Result = eConnectionModeSwitchResult.NotAvailableYet;
			}
			else
			{
				m_Status.Progress = eConnectionModeSwitchProgress.Complete;
				m_Status.Result = eConnectionModeSwitchResult.Failure;
			}
		}
	}

	public void Update()
	{
		if (m_Status.Progress == eConnectionModeSwitchProgress.NotStarted && m_Status.Result == eConnectionModeSwitchResult.NotAvailableYet)
		{
			TryStart();
		}
		if (m_connectionMode == OnlineMultiplayerConnectionMode.eNone && m_connectionModeCoordinator.Mode() == m_connectionMode)
		{
			m_Status.Progress = eConnectionModeSwitchProgress.Complete;
			m_Status.Result = eConnectionModeSwitchResult.Success;
		}
	}

	public IConnectionModeSwitchStatus GetStatus()
	{
		return m_Status;
	}

	public object GetData()
	{
		return m_PassthroughObject;
	}

	public GamepadUser GetCurrentUser()
	{
		return m_user;
	}

	private void OnlineMultiplayerConnectionModeConnectCallback(OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeConnectResult> result)
	{
		m_Status.m_Result = result;
		m_Status.Progress = eConnectionModeSwitchProgress.Complete;
		if (result.m_returnCode == OnlineMultiplayerConnectionModeConnectResult.eSuccess)
		{
			m_Status.Result = eConnectionModeSwitchResult.Success;
		}
		else
		{
			m_Status.Result = eConnectionModeSwitchResult.Failure;
		}
	}
}
