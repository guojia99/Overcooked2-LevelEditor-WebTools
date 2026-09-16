using Team17.Online.Multiplayer;

public class ResetServerUsersTask : IMultiplayerTask
{
	private DefaultStatus m_Status = new DefaultStatus();

	private Server m_Server;

	public void Initialise(Server server)
	{
		m_Server = server;
	}

	public void Start(object startData)
	{
		m_Server.GetUserSystem().ResetUsersToOfflineState();
		m_Status.Progress = eConnectionModeSwitchProgress.Complete;
		m_Status.Result = eConnectionModeSwitchResult.Success;
	}

	public void Stop()
	{
		m_Status.Progress = eConnectionModeSwitchProgress.NotStarted;
		m_Status.Result = eConnectionModeSwitchResult.NotAvailableYet;
	}

	public void Update()
	{
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
