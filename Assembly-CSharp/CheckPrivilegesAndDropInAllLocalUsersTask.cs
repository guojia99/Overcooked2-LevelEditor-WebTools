using System.Collections.Generic;
using Team17.Online;

public class CheckPrivilegesAndDropInAllLocalUsersTask : IMultiplayerTask
{
	private CheckPrivilegesAndDropInLocalUserTask m_CurrentUserTask = new CheckPrivilegesAndDropInLocalUserTask();

	private DefaultStatus m_NothingToDoStatus = new DefaultStatus();

	private bool m_bFinished;

	private bool m_bStarted;

	private CompositeStatus m_Status = new CompositeStatus();

	private IPlayerManager m_PlayerManager;

	private EngagementSlot m_Slot = EngagementSlot.Count;

	private GamepadUser m_CurrentUser;

	private GenericVoid<GamepadUser, EngagementSlot, IConnectionModeSwitchStatus> m_UserChecked;

	public void Initialise(GenericVoid<GamepadUser, EngagementSlot, IConnectionModeSwitchStatus> userChecked)
	{
		m_PlayerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
		m_Status.m_TaskSubStatus = null;
		m_NothingToDoStatus.Progress = eConnectionModeSwitchProgress.Complete;
		m_NothingToDoStatus.Result = eConnectionModeSwitchResult.Success;
		m_UserChecked = userChecked;
	}

	public void Start(object startData)
	{
		m_bFinished = false;
		m_Status.bFinalTask = false;
		m_Status.m_TaskSubStatus = m_CurrentUserTask.GetStatus();
		m_bStarted = true;
		TryStart();
	}

	public void Stop()
	{
		m_bStarted = false;
		m_Status.m_TaskSubStatus = null;
	}

	private void StartNewDropIn(GamepadUser pad, EngagementSlot slot)
	{
		m_Slot = slot;
		m_CurrentUser = pad;
		m_CurrentUserTask.Initialise(pad);
		m_CurrentUserTask.Start(null);
	}

	private bool DropInPreExistingUser(EngagementSlot slot)
	{
		bool result = false;
		GamepadUser user = m_PlayerManager.GetUser(slot);
		if (null != user)
		{
			OnlineMultiplayerLocalUserId allowedUser = PrivilegeCheckCache.GetAllowedUser(user);
			if (allowedUser != null)
			{
				if (!LocalDroppedInUserCache.HasBeenDroppedIn(allowedUser))
				{
					StartNewDropIn(user, slot);
					result = true;
				}
			}
			else
			{
				StartNewDropIn(user, slot);
				result = true;
			}
		}
		return result;
	}

	private bool DropInEngagedPad(EngagementSlot slot)
	{
		bool result = false;
		GamepadUser user = m_PlayerManager.GetUser(slot);
		if (null != user)
		{
			OnlineMultiplayerLocalUserId allowedUser = PrivilegeCheckCache.GetAllowedUser(user);
			if (allowedUser == null || !LocalDroppedInUserCache.HasBeenDroppedIn(allowedUser))
			{
				StartNewDropIn(user, slot);
				result = true;
			}
		}
		return result;
	}

	private void TryStart()
	{
		if (!m_bStarted || m_CurrentUserTask.GetStatus().GetProgress() == eConnectionModeSwitchProgress.InProgress)
		{
			return;
		}
		bool flag = false;
		for (int i = 1; i < 4; i++)
		{
			FastList<User> users = ServerUserSystem.m_Users;
			User.MachineID s_LocalMachineId = ServerUserSystem.s_LocalMachineId;
			User user = UserSystemUtils.FindUser(users, null, s_LocalMachineId, (EngagementSlot)i);
			flag = ((user == null) ? DropInEngagedPad((EngagementSlot)i) : DropInPreExistingUser((EngagementSlot)i));
			if (flag)
			{
				break;
			}
		}
		if (!flag)
		{
			if (m_Status.m_TaskSubStatus.GetProgress() != eConnectionModeSwitchProgress.Complete)
			{
				m_Status.m_TaskSubStatus = m_NothingToDoStatus;
			}
			m_Status.bFinalTask = true;
			m_bFinished = true;
		}
	}

	public void Update()
	{
		if (m_bFinished)
		{
			return;
		}
		m_CurrentUserTask.Update();
		switch (m_CurrentUserTask.GetStatus().GetProgress())
		{
		case eConnectionModeSwitchProgress.NotStarted:
			TryStart();
			break;
		case eConnectionModeSwitchProgress.Complete:
			if (m_CurrentUserTask.GetStatus().GetResult() != eConnectionModeSwitchResult.Success)
			{
				m_Status.bFinalTask = true;
				m_bFinished = true;
			}
			if (m_UserChecked != null)
			{
				m_UserChecked(m_CurrentUser, m_Slot, m_CurrentUserTask.GetStatus());
			}
			if (m_CurrentUserTask.GetStatus().GetResult() == eConnectionModeSwitchResult.Success)
			{
				TryStart();
			}
			break;
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
