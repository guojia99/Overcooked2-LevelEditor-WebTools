using Team17.Online.Multiplayer;

public class OfflineTask : IMultiplayerTask
{
	private DefaultStatus m_Status = new DefaultStatus();

	private Server m_Server;

	private Client m_Client;

	public void Initialise(Server server, Client client)
	{
		m_Server = server;
		m_Client = client;
	}

	public void Start(object startData)
	{
		NetworkSystemConfigurator.Offline(m_Server, m_Client);
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
