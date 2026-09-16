using Team17.Online;
using Team17.Online.Multiplayer;

public class MatchmakingAgent : ConnectionModeAgent
{
	private IMultiplayerTask[] m_Tasks = new IMultiplayerTask[6]
	{
		new LeaveSessionTask(),
		new OfflineTask(),
		new SetupConnectionModeTask(),
		new PrivilegeCheckTask(),
		new SearchTask(),
		new JoinRandomRoomTask()
	};

	private bool m_bCalledBack;

	private GenericVoid<IConnectionModeSwitchStatus> m_Callback;

	private MultiplayerOperation m_CurrentAction = new MultiplayerOperation();

	private MatchmakeData m_Data;

	private Server m_LocalServer;

	private Client m_LocalClient;

	public virtual bool Start(Server server, Client client, object data, GenericVoid<IConnectionModeSwitchStatus> callback)
	{
		m_Callback = callback;
		if (GetStatus().GetProgress() == eConnectionModeSwitchProgress.Complete && GetStatus().GetResult() == eConnectionModeSwitchResult.Success && ConnectionStatus.IsInSession())
		{
			if (callback != null)
			{
				callback(GetStatus());
			}
			m_bCalledBack = true;
			return true;
		}
		m_Data = (MatchmakeData)data;
		m_LocalServer = server;
		m_LocalClient = client;
		OfflineTask offlineTask = m_Tasks[1] as OfflineTask;
		offlineTask.Initialise(m_LocalServer, m_LocalClient);
		SetupConnectionModeTask setupConnectionModeTask = m_Tasks[2] as SetupConnectionModeTask;
		setupConnectionModeTask.Initialise(m_Data.User, m_Data.connectionMode);
		PrivilegeCheckTask privilegeCheckTask = m_Tasks[3] as PrivilegeCheckTask;
		privilegeCheckTask.Initialise(m_Data.User);
		SearchSessionPropertyValuesProvider.SetGameMode(m_Data.gameMode);
		SearchTask searchTask = m_Tasks[4] as SearchTask;
		searchTask.Initialise(SearchSessionPropertyValuesProvider.GetValues());
		ServerMessenger.OnServerStopped();
		m_CurrentAction.Start(m_Tasks);
		m_bCalledBack = false;
		return true;
	}

	public void Stop()
	{
		m_CurrentAction.Stop();
	}

	public void InvalidateCallback(GenericVoid<IConnectionModeSwitchStatus> callback)
	{
		if (m_Callback == callback)
		{
			m_Callback = null;
		}
	}

	public object GetAgentData()
	{
		return m_Data;
	}

	public virtual void Update()
	{
		if (m_bCalledBack)
		{
			return;
		}
		m_CurrentAction.Update();
		IConnectionModeSwitchStatus status = GetStatus();
		if (status.GetProgress() != eConnectionModeSwitchProgress.Complete)
		{
			return;
		}
		if (status.GetResult() == eConnectionModeSwitchResult.Success)
		{
			IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
			IOnlineMultiplayerSessionCoordinator onlineMultiplayerSessionCoordinator = onlinePlatformManager.OnlineMultiplayerSessionCoordinator();
			IOnlineMultiplayerSessionUserId sessionHostUser = UserSystemUtils.GetSessionHostUser();
			JoinData joinData = m_CurrentAction.GetTaskData() as JoinData;
			m_LocalServer.Reset();
			m_LocalClient.Reset();
			if (ConnectionStatus.IsHost())
			{
				if (sessionHostUser != null && onlineMultiplayerSessionCoordinator != null)
				{
					m_LocalServer.Initialise(true);
					m_LocalClient.Initialise(false);
					NetworkSystemConfigurator.Server(m_LocalServer, m_LocalClient, PrivilegeCheckCache.GetAllowedUser(m_Data.User));
					ServerMessenger.TimeSync(0f);
				}
			}
			else if (sessionHostUser != null && joinData != null && onlineMultiplayerSessionCoordinator != null)
			{
				NetworkSystemConfigurator.Client(m_LocalClient, joinData, onlineMultiplayerSessionCoordinator);
			}
		}
		if (m_Callback != null)
		{
			m_Callback(GetStatus());
		}
		m_bCalledBack = true;
	}

	public IConnectionModeSwitchStatus GetStatus()
	{
		return m_CurrentAction.GetStatus();
	}
}
