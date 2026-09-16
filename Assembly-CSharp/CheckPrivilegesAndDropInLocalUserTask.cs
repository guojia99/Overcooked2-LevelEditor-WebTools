public class CheckPrivilegesAndDropInLocalUserTask : IMultiplayerTask
{
	private GamepadUser m_GamepadUser;

	private PrivilegeCheckTask m_PrivilegeCheck = new PrivilegeCheckTask();

	private DropInLocalUserTask m_DropInLocalUser = new DropInLocalUserTask();

	private IMultiplayerTask[] m_AddUserToSessionTasks;

	private MultiplayerOperation m_CurrentAction = new MultiplayerOperation();

	private bool m_bBusy;

	public void Initialise(GamepadUser user)
	{
		m_AddUserToSessionTasks = new IMultiplayerTask[2] { m_PrivilegeCheck, m_DropInLocalUser };
		m_GamepadUser = user;
		m_PrivilegeCheck.Initialise(user);
		m_DropInLocalUser.Initialise(user);
	}

	public void Start(object startData)
	{
		m_CurrentAction.Start(m_AddUserToSessionTasks);
		m_bBusy = true;
	}

	public void Stop()
	{
		m_GamepadUser = null;
		m_CurrentAction.Stop();
		m_bBusy = false;
	}

	public void Update()
	{
		if (m_bBusy)
		{
			m_CurrentAction.Update();
		}
	}

	public IConnectionModeSwitchStatus GetStatus()
	{
		return m_CurrentAction.GetStatus();
	}

	public object GetData()
	{
		return null;
	}
}
