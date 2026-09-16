using Team17.Online;

public class LeaveSessionTask : IMultiplayerTask
{
	private IOnlineMultiplayerSessionCoordinator m_iOnlineMultiplayerSessionCoordinator;

	private LeaveSessionStatus m_Status = new LeaveSessionStatus();

	private bool m_bWait = true;

	public void Initialise(bool bWait)
	{
		m_bWait = bWait;
	}

	public void Start(object startData)
	{
		IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
		m_iOnlineMultiplayerSessionCoordinator = onlinePlatformManager.OnlineMultiplayerSessionCoordinator();
		if (m_iOnlineMultiplayerSessionCoordinator == null)
		{
			m_Status.Progress = eConnectionModeSwitchProgress.Complete;
			m_Status.Result = eConnectionModeSwitchResult.Success;
			return;
		}
		m_iOnlineMultiplayerSessionCoordinator.Leave();
		if (!m_bWait || m_iOnlineMultiplayerSessionCoordinator.IsIdle())
		{
			m_Status.Progress = eConnectionModeSwitchProgress.Complete;
			m_Status.Result = eConnectionModeSwitchResult.Success;
		}
		else
		{
			m_Status.Progress = eConnectionModeSwitchProgress.InProgress;
			m_Status.Result = eConnectionModeSwitchResult.NotAvailableYet;
		}
	}

	public void Stop()
	{
		m_Status.Progress = eConnectionModeSwitchProgress.NotStarted;
		m_Status.Result = eConnectionModeSwitchResult.NotAvailableYet;
	}

	public void Update()
	{
		if (m_Status.Progress == eConnectionModeSwitchProgress.InProgress && m_iOnlineMultiplayerSessionCoordinator.IsIdle())
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
		return null;
	}
}
