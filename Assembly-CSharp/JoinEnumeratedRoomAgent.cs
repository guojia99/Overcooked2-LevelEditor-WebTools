using Team17.Online;
using Team17.Online.Multiplayer;

public class JoinEnumeratedRoomAgent : ConnectionModeAgent
{
	private IMultiplayerTask[] m_Tasks = new IMultiplayerTask[3]
	{
		new LeaveSessionTask(),
		new PrivilegeCheckAllUsersForJoinEnumeratedRoomTask(),
		new JoinEnumeratedRoomTask()
	};

	private Server m_LocalServer;

	private Client m_LocalClient;

	private MultiplayerOperation m_CurrentAction;

	private bool m_ProcessedCompletion;

	private IOnlineMultiplayerSessionCoordinator m_iOnlineMultiplayerSessionCoordinator;

	private GenericVoid<IConnectionModeSwitchStatus> m_Callback;

	public IConnectionModeSwitchStatus GetStatus()
	{
		return m_CurrentAction.GetStatus();
	}

	public virtual bool Start(Server server, Client client, object data, GenericVoid<IConnectionModeSwitchStatus> callback)
	{
		GameUtils.RequireManagerInterface<OvercookedEngagementController>().IsClientMode = true;
		JoinEnumeratedRoomOptions joinEnumeratedRoomOptions = data as JoinEnumeratedRoomOptions;
		ServerMessenger.OnServerStopped();
		m_LocalServer = server;
		m_LocalClient = client;
		m_LocalServer.Reset();
		m_LocalClient.Reset();
		PrivilegeCheckAllUsersForJoinEnumeratedRoomTask privilegeCheckAllUsersForJoinEnumeratedRoomTask = m_Tasks[1] as PrivilegeCheckAllUsersForJoinEnumeratedRoomTask;
		privilegeCheckAllUsersForJoinEnumeratedRoomTask.Initialise();
		JoinEnumeratedRoomTask joinEnumeratedRoomTask = m_Tasks[2] as JoinEnumeratedRoomTask;
		joinEnumeratedRoomTask.Initialise(joinEnumeratedRoomOptions.Room, NetConnectionState.JoinEnumeratedRoom);
		m_CurrentAction = new MultiplayerOperation();
		m_CurrentAction.Start(m_Tasks);
		IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
		m_iOnlineMultiplayerSessionCoordinator = onlinePlatformManager.OnlineMultiplayerSessionCoordinator();
		m_ProcessedCompletion = false;
		m_Callback = callback;
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
		return null;
	}

	public virtual void OnDisconnected()
	{
	}

	public virtual void Update()
	{
		if (m_CurrentAction == null || m_ProcessedCompletion)
		{
			return;
		}
		m_CurrentAction.Update();
		if (m_CurrentAction.GetStatus().GetProgress() == eConnectionModeSwitchProgress.Complete)
		{
			m_ProcessedCompletion = true;
			if (m_CurrentAction.GetStatus().GetResult() == eConnectionModeSwitchResult.Success)
			{
				NetworkSystemConfigurator.Client(m_LocalClient, m_CurrentAction.GetTaskData() as JoinData, m_iOnlineMultiplayerSessionCoordinator);
			}
			if (m_Callback != null)
			{
				m_Callback(GetStatus());
			}
		}
	}
}
