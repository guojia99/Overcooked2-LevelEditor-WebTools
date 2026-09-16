using Team17.Online;
using Team17.Online.Multiplayer;
using UnityEngine;

public class ServerAgent : ConnectionModeAgent
{
	private ServerOptions m_ServerOptions = default(ServerOptions);

	private LeaveSessionTask m_LeaveSessionTask = new LeaveSessionTask();

	private SetupConnectionModeTask m_ConnectionModeTask = new SetupConnectionModeTask();

	private ResetServerUsersTask m_ResetServerUsers = new ResetServerUsersTask();

	private PrivilegeCheckAllUsersTask m_PrivilegeCheckAllUsersTask = new PrivilegeCheckAllUsersTask();

	private CreateSessionTask m_CreateSessionTask = new CreateSessionTask();

	private CheckPrivilegesAndDropInAllLocalUsersTask m_AddLocalUsersTask = new CheckPrivilegesAndDropInAllLocalUsersTask();

	private IMultiplayerTask[] m_StartServerTasks;

	private ModifySessionTask m_ModifySessionTask = new ModifySessionTask();

	private IMultiplayerTask[] m_ModifyServerTasks;

	private Server m_LocalServer;

	private Client m_LocalClient;

	private MultiplayerOperation m_CurrentAction = new MultiplayerOperation();

	private GenericVoid<IConnectionModeSwitchStatus> m_Callback;

	private IOnlineMultiplayerConnectionModeCoordinator m_iOnlineMultiplayerConnectionModeCoordinator;

	private IOnlineMultiplayerSessionCoordinator m_iOnlineMultiplayerSessionCoordinator;

	private bool m_bSetup;

	private bool m_bCalledBack;

	public IConnectionModeSwitchStatus GetStatus()
	{
		return m_CurrentAction.GetStatus();
	}

	private void Setup()
	{
		if (!m_bSetup)
		{
			IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
			m_iOnlineMultiplayerSessionCoordinator = onlinePlatformManager.OnlineMultiplayerSessionCoordinator();
			m_iOnlineMultiplayerConnectionModeCoordinator = onlinePlatformManager.OnlineMultiplayerConnectionModeCoordinator();
			m_StartServerTasks = new IMultiplayerTask[6] { m_LeaveSessionTask, m_ConnectionModeTask, m_ResetServerUsers, m_PrivilegeCheckAllUsersTask, m_CreateSessionTask, m_AddLocalUsersTask };
			m_ModifyServerTasks = new IMultiplayerTask[1] { m_ModifySessionTask };
			m_bSetup = true;
			m_bCalledBack = false;
		}
		m_bCalledBack = false;
	}

	public virtual bool Start(Server server, Client client, object data, GenericVoid<IConnectionModeSwitchStatus> callback)
	{
		Setup();
		ServerOptions options = (ServerOptions)data;
		try
		{
		}
		catch (UnityException ex)
		{
			if (ex == null)
			{
			}
		}
		bool flag = m_iOnlineMultiplayerSessionCoordinator.IsHost() && (m_iOnlineMultiplayerConnectionModeCoordinator == null || m_iOnlineMultiplayerConnectionModeCoordinator.Mode() == options.connectionMode);
		if (options.gameMode == GameMode.COUNT || (!flag && options.hostUser == null))
		{
			if (callback != null)
			{
				OnlineMultiplayerReturnCode<OnlineMultiplayerPrivilegeCheckResult> onlineMultiplayerReturnCode = new OnlineMultiplayerReturnCode<OnlineMultiplayerPrivilegeCheckResult>();
				onlineMultiplayerReturnCode.m_returnCode = OnlineMultiplayerPrivilegeCheckResult.eGenericFailure;
				onlineMultiplayerReturnCode.m_usePlatformError = false;
				OnlineMultiplayerReturnCode<OnlineMultiplayerPrivilegeCheckResult> privilegeCheckResult = onlineMultiplayerReturnCode;
				callback(new PrivilegeStatus
				{
					Result = eConnectionModeSwitchResult.Failure,
					privilegeCheckResult = privilegeCheckResult
				});
			}
			m_bCalledBack = true;
			return false;
		}
		m_Callback = callback;
		IConnectionModeSwitchStatus status = GetStatus();
		bool flag2 = status.GetProgress() == eConnectionModeSwitchProgress.Complete && status.GetResult() == eConnectionModeSwitchResult.Success;
		if (options.gameMode == m_ServerOptions.gameMode && options.visibility == m_ServerOptions.visibility && options.hostUser == m_ServerOptions.hostUser && options.connectionMode == m_ServerOptions.connectionMode && options.playTogetherHost == m_ServerOptions.playTogetherHost && ConnectionStatus.IsInSession() && ConnectionStatus.IsHost())
		{
			if (flag2)
			{
				if (m_Callback != null)
				{
					m_Callback(GetStatus());
				}
				return true;
			}
			if (status.GetProgress() == eConnectionModeSwitchProgress.InProgress && status.GetResult() == eConnectionModeSwitchResult.NotAvailableYet)
			{
				return true;
			}
		}
		m_ServerOptions.gameMode = options.gameMode;
		m_ServerOptions.visibility = options.visibility;
		m_ServerOptions.hostUser = options.hostUser;
		m_ServerOptions.connectionMode = options.connectionMode;
		m_ServerOptions.playTogetherHost = options.playTogetherHost;
		ServerSessionPropertyValuesProvider.SetGameMode(m_ServerOptions.gameMode);
		OnlineMultiplayerSessionVisibility visibility = options.visibility;
		if (flag)
		{
			StartModify(options);
		}
		else
		{
			StartCreate(server, client, options);
		}
		if (GetStatus().GetProgress() == eConnectionModeSwitchProgress.Complete && m_Callback != null)
		{
			m_Callback(GetStatus());
		}
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
		return m_ServerOptions;
	}

	private void StartModify(ServerOptions options)
	{
		OnlineMultiplayerSessionVisibility visibility = options.visibility;
		m_ModifySessionTask.Initialise(m_iOnlineMultiplayerSessionCoordinator, visibility, ServerSessionPropertyValuesProvider.GetValues());
		m_CurrentAction.Start(m_ModifyServerTasks);
	}

	private void StartCreate(Server server, Client client, ServerOptions options)
	{
		ServerMessenger.OnServerStopped();
		m_LocalServer = server;
		m_LocalClient = client;
		m_LocalServer.Reset();
		m_LocalClient.Reset();
		m_LocalServer.Initialise(true);
		m_LocalClient.Initialise(false);
		m_PrivilegeCheckAllUsersTask.Initialise(options.hostUser);
		m_ConnectionModeTask.Initialise(options.hostUser, options.connectionMode);
		m_ResetServerUsers.Initialise(server);
		m_AddLocalUsersTask.Initialise(null);
		m_CreateSessionTask.Initialise(server, client, options.visibility, options.playTogetherHost, ServerSessionPropertyValuesProvider.GetValues());
		options.playTogetherHost = null;
		m_ServerOptions.playTogetherHost = null;
		m_CurrentAction.Start(m_StartServerTasks);
	}

	public virtual void OnDisconnected()
	{
	}

	public virtual void Update()
	{
		if (m_CurrentAction == null || m_bCalledBack)
		{
			return;
		}
		m_CurrentAction.Update();
		if (m_CurrentAction.GetStatus().GetProgress() == eConnectionModeSwitchProgress.Complete)
		{
			if (m_Callback != null)
			{
				m_Callback(GetStatus());
			}
			m_bCalledBack = true;
		}
	}
}
