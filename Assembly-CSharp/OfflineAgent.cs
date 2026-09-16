using Team17.Online;
using Team17.Online.Multiplayer;

public class OfflineAgent : ConnectionModeAgent
{
	private IMultiplayerTask[] m_Tasks;

	private Server m_LocalServer;

	private Client m_LocalClient;

	private GenericVoid<IConnectionModeSwitchStatus> m_Callback;

	private bool m_bCalledBack;

	private MultiplayerOperation m_CurrentAction = new MultiplayerOperation();

	private LeaveSessionTask m_LeaveSessionTask = new LeaveSessionTask();

	private SetupConnectionModeTask m_SetupConnectionModeTask = new SetupConnectionModeTask();

	private OfflineTask m_OfflineTask = new OfflineTask();

	private PrivilegeCheckAllUsersTask m_PrivilegeCheckAllUsersTask = new PrivilegeCheckAllUsersTask();

	private SearchTask m_SearchTask = new SearchTask();

	public virtual bool Start(Server server, Client client, object data, GenericVoid<IConnectionModeSwitchStatus> callback)
	{
		GameUtils.RequireManagerInterface<OvercookedEngagementController>().IsClientMode = false;
		IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
		m_Callback = callback;
		bool flag = false;
		OnlineMultiplayerConnectionMode onlineMultiplayerConnectionMode = OnlineMultiplayerConnectionMode.eNone;
		OnlineMultiplayerConnectionMode onlineMultiplayerConnectionMode2 = OnlineMultiplayerConnectionMode.eNone;
		IOnlineMultiplayerConnectionModeCoordinator onlineMultiplayerConnectionModeCoordinator = onlinePlatformManager.OnlineMultiplayerConnectionModeCoordinator();
		if (onlineMultiplayerConnectionModeCoordinator != null)
		{
			onlineMultiplayerConnectionMode = onlineMultiplayerConnectionModeCoordinator.Mode();
			onlineMultiplayerConnectionMode2 = onlineMultiplayerConnectionMode;
			flag = true;
		}
		if (data != null)
		{
			OfflineOptions offlineOptions = (OfflineOptions)data;
			if (offlineOptions.connectionMode.HasValue)
			{
				onlineMultiplayerConnectionMode2 = offlineOptions.connectionMode.Value;
			}
		}
		bool flag2 = false;
		if (flag)
		{
			flag2 = onlineMultiplayerConnectionMode2 == onlineMultiplayerConnectionMode;
		}
		if (flag2 && GetStatus().GetProgress() == eConnectionModeSwitchProgress.Complete && GetStatus().GetResult() == eConnectionModeSwitchResult.Success && !ConnectionStatus.IsInSession() && (data == null || ((OfflineOptions)data).eAdditionalAction == OfflineOptions.AdditionalAction.None))
		{
			if (callback != null)
			{
				callback(GetStatus());
			}
			return true;
		}
		if (GetStatus().GetProgress() == eConnectionModeSwitchProgress.InProgress && GetStatus().GetResult() == eConnectionModeSwitchResult.NotAvailableYet)
		{
			return true;
		}
		m_bCalledBack = false;
		m_LocalServer = server;
		m_LocalClient = client;
		ServerMessenger.OnServerStopped();
		m_LocalServer.Reset();
		m_LocalClient.Reset();
		m_LocalServer.Initialise(false);
		m_LocalClient.Initialise(false);
		m_LeaveSessionTask.Initialise(false);
		PrivilegeCheckCache.Clear();
		if (data != null)
		{
			OfflineOptions offlineOptions2 = (OfflineOptions)data;
			switch (offlineOptions2.eAdditionalAction)
			{
			case OfflineOptions.AdditionalAction.PrivilegeCheckAllUsersAndSearchForGames:
				m_Tasks = new IMultiplayerTask[5] { m_LeaveSessionTask, m_SetupConnectionModeTask, m_OfflineTask, m_PrivilegeCheckAllUsersTask, m_SearchTask };
				m_PrivilegeCheckAllUsersTask.Initialise(offlineOptions2.hostUser);
				SearchSessionPropertyValuesProvider.SetGameMode(offlineOptions2.searchGameMode);
				m_SearchTask.Initialise(SearchSessionPropertyValuesProvider.GetValues());
				break;
			case OfflineOptions.AdditionalAction.PrivilegeCheckAllUsers:
				m_Tasks = new IMultiplayerTask[4] { m_LeaveSessionTask, m_SetupConnectionModeTask, m_OfflineTask, m_PrivilegeCheckAllUsersTask };
				m_PrivilegeCheckAllUsersTask.Initialise(offlineOptions2.hostUser);
				break;
			case OfflineOptions.AdditionalAction.None:
				m_Tasks = new IMultiplayerTask[3] { m_LeaveSessionTask, m_SetupConnectionModeTask, m_OfflineTask };
				break;
			}
			m_SetupConnectionModeTask.Initialise(offlineOptions2.hostUser, onlineMultiplayerConnectionMode2);
		}
		else
		{
			m_Tasks = new IMultiplayerTask[3] { m_LeaveSessionTask, m_SetupConnectionModeTask, m_OfflineTask };
			m_SetupConnectionModeTask.Initialise(m_SetupConnectionModeTask.GetCurrentUser(), onlineMultiplayerConnectionMode2);
		}
		m_OfflineTask.Initialise(server, client);
		m_CurrentAction.Start(m_Tasks);
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
		return m_CurrentAction.GetTaskData();
	}

	public virtual void OnDisconnected()
	{
	}

	public virtual void Update()
	{
		IConnectionModeSwitchStatus status = GetStatus();
		if (m_CurrentAction == null || m_bCalledBack)
		{
			return;
		}
		m_CurrentAction.Update();
		if (status.GetProgress() == eConnectionModeSwitchProgress.Complete)
		{
			m_bCalledBack = true;
			if (status.GetResult() == eConnectionModeSwitchResult.Success)
			{
			}
			if (m_Callback != null)
			{
				m_Callback(GetStatus());
			}
		}
	}

	public IConnectionModeSwitchStatus GetStatus()
	{
		return m_CurrentAction.GetStatus();
	}
}
