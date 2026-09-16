using Team17.Online;

public class PrivilegeCheckAllUsersTask : IMultiplayerTask
{
	private class Task
	{
		public PrivilegeCheckTask PrivilegeCheck = new PrivilegeCheckTask();

		public EngagementSlot Slot;

		public void Initialise(GamepadUser user)
		{
			PrivilegeCheck.Initialise(user);
			Slot = EngagementSlot.Count;
		}
	}

	private int m_iCurrentTask;

	private int m_iTaskCount;

	private Task[] m_Tasks = new Task[4];

	private CompositeStatus m_Status = new CompositeStatus();

	private GamepadUser m_PrimaryUser;

	private Task m_PrimaryUserTask;

	public void Initialise(GamepadUser primaryUser)
	{
		m_PrimaryUser = primaryUser;
		for (int i = 0; i < m_Tasks.Length; i++)
		{
			if (m_Tasks[i] == null)
			{
				m_Tasks[i] = new Task();
			}
		}
	}

	public void Start(object startData)
	{
		m_PrimaryUserTask = null;
		int num = 0;
		int count = ServerUserSystem.m_Users.Count;
		for (int i = 0; i < count; i++)
		{
			User user = ServerUserSystem.m_Users._items[i];
			if (user.IsLocal && null != user.GamepadUser)
			{
				Task task = m_Tasks[num];
				task.Slot = user.Engagement;
				task.Initialise(user.GamepadUser);
				num++;
				if (user.GamepadUser.UID == m_PrimaryUser.UID)
				{
					m_PrimaryUserTask = task;
				}
			}
		}
		m_Status.m_TaskSubStatus = null;
		m_Status.bFinalTask = false;
		m_iCurrentTask = -1;
		m_iTaskCount = num;
		if (m_iTaskCount != 0)
		{
			StartNextCheck();
		}
	}

	public void Stop()
	{
		m_Status.m_TaskSubStatus = null;
		m_Status.bFinalTask = false;
		for (int i = 0; i < m_Tasks.Length; i++)
		{
			m_Tasks[i].PrivilegeCheck.Stop();
		}
	}

	public void Update()
	{
		if (m_iCurrentTask < 0 || m_iCurrentTask >= m_iTaskCount)
		{
			return;
		}
		Task task = m_Tasks[m_iCurrentTask];
		PrivilegeCheckTask privilegeCheck = task.PrivilegeCheck;
		if (privilegeCheck.GetStatus().GetProgress() == eConnectionModeSwitchProgress.Complete)
		{
			if (privilegeCheck.GetStatus().GetResult() == eConnectionModeSwitchResult.Success)
			{
				StartNextCheck();
			}
			else
			{
				m_Status.bFinalTask = true;
			}
		}
	}

	private void StartNextCheck()
	{
		if (m_iCurrentTask != m_iTaskCount)
		{
			m_iCurrentTask++;
			Task task = m_Tasks[m_iCurrentTask];
			PrivilegeCheckTask privilegeCheck = task.PrivilegeCheck;
			privilegeCheck.Start(null);
			m_Status.m_TaskSubStatus = privilegeCheck.GetStatus();
			if (m_iCurrentTask == m_iTaskCount - 1)
			{
				m_Status.bFinalTask = true;
			}
		}
	}

	public IConnectionModeSwitchStatus GetStatus()
	{
		return m_Status;
	}

	public object GetData()
	{
		if (GetStatus().GetProgress() == eConnectionModeSwitchProgress.Complete && GetStatus().GetResult() == eConnectionModeSwitchResult.Success)
		{
			return m_PrimaryUserTask.PrivilegeCheck.GetData();
		}
		return null;
	}
}
