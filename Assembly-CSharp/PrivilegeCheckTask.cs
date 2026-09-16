using Team17.Online;

public class PrivilegeCheckTask : IMultiplayerTask
{
	private GamepadUser m_UserToCheck;

	private IOnlineMultiplayerPrivilegeChecksCoordinator m_iOnlinePrivlegeCheckCoordinator;

	private OnlineMultiplayerLocalUserId m_LocalUserId;

	private PrivilegeStatus m_Status = new PrivilegeStatus();

	private IPlayerManager m_PlayerManager;

	public void Initialise(GamepadUser user)
	{
		IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
		m_iOnlinePrivlegeCheckCoordinator = onlinePlatformManager.OnlineMultiplayerPrivilegeChecksCoordinator();
		m_PlayerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
		m_UserToCheck = user;
		m_Status.currentUser = m_UserToCheck;
		m_Status.Progress = eConnectionModeSwitchProgress.NotStarted;
		m_Status.Result = eConnectionModeSwitchResult.NotAvailableYet;
		if (!(m_UserToCheck != null))
		{
		}
	}

	public void Reset()
	{
		m_UserToCheck = null;
		m_Status.currentUser = null;
		m_Status.Progress = eConnectionModeSwitchProgress.NotStarted;
		m_Status.Result = eConnectionModeSwitchResult.NotAvailableYet;
		if (m_iOnlinePrivlegeCheckCoordinator != null && !m_iOnlinePrivlegeCheckCoordinator.IsIdle())
		{
			m_iOnlinePrivlegeCheckCoordinator.Cancel();
		}
	}

	public void Start(object startData)
	{
		if (m_UserToCheck == null)
		{
			m_Status.privilegeCheckResult = new OnlineMultiplayerReturnCode<OnlineMultiplayerPrivilegeCheckResult>
			{
				m_returnCode = OnlineMultiplayerPrivilegeCheckResult.eGenericFailure
			};
			m_Status.Progress = eConnectionModeSwitchProgress.Complete;
			m_Status.Result = eConnectionModeSwitchResult.Failure;
			return;
		}
		if (m_iOnlinePrivlegeCheckCoordinator == null)
		{
			m_Status.Progress = eConnectionModeSwitchProgress.Complete;
			m_Status.Result = eConnectionModeSwitchResult.Success;
			return;
		}
		m_LocalUserId = PrivilegeCheckCache.GetAllowedUser(m_UserToCheck);
		m_Status.currentUser = m_UserToCheck;
		if (m_LocalUserId != null)
		{
			m_Status.Progress = eConnectionModeSwitchProgress.Complete;
			m_Status.Result = eConnectionModeSwitchResult.Success;
		}
		else
		{
			m_Status.Progress = eConnectionModeSwitchProgress.NotStarted;
			m_Status.Result = eConnectionModeSwitchResult.NotAvailableYet;
			TryStart();
		}
		m_PlayerManager.EngagementChangeCallback += OnEngagementChanged;
	}

	public void Stop()
	{
		m_Status.Progress = eConnectionModeSwitchProgress.NotStarted;
		m_Status.Result = eConnectionModeSwitchResult.NotAvailableYet;
		if (m_PlayerManager != null)
		{
			m_PlayerManager.EngagementChangeCallback -= OnEngagementChanged;
		}
	}

	public void OnEngagementChanged(EngagementSlot _e, GamepadUser _prev, GamepadUser _new)
	{
		if (_new == null && _prev != null && _prev == m_UserToCheck && m_Status.GetProgress() != eConnectionModeSwitchProgress.Complete)
		{
			m_iOnlinePrivlegeCheckCoordinator.Cancel();
			m_Status.privilegeCheckResult = new OnlineMultiplayerReturnCode<OnlineMultiplayerPrivilegeCheckResult>
			{
				m_returnCode = OnlineMultiplayerPrivilegeCheckResult.eGenericFailure
			};
			m_Status.Progress = eConnectionModeSwitchProgress.Complete;
			m_Status.Result = eConnectionModeSwitchResult.Failure;
		}
	}

	private void TryStart()
	{
		if (m_Status.Progress == eConnectionModeSwitchProgress.NotStarted && m_iOnlinePrivlegeCheckCoordinator.IsIdle())
		{
			if (m_iOnlinePrivlegeCheckCoordinator.Start(m_UserToCheck, OnlineMultiplayerPrivilegeCheckCallback))
			{
				m_Status.Progress = eConnectionModeSwitchProgress.InProgress;
				m_Status.Result = eConnectionModeSwitchResult.NotAvailableYet;
				return;
			}
			m_Status.privilegeCheckResult = new OnlineMultiplayerReturnCode<OnlineMultiplayerPrivilegeCheckResult>
			{
				m_returnCode = OnlineMultiplayerPrivilegeCheckResult.eGenericFailure
			};
			m_Status.Progress = eConnectionModeSwitchProgress.Complete;
			m_Status.Result = eConnectionModeSwitchResult.Failure;
		}
	}

	public void Update()
	{
		TryStart();
	}

	public IConnectionModeSwitchStatus GetStatus()
	{
		return m_Status;
	}

	public object GetData()
	{
		return m_LocalUserId;
	}

	private void OnlineMultiplayerPrivilegeCheckCallback(OnlineMultiplayerReturnCode<OnlineMultiplayerPrivilegeCheckResult> result, OnlineMultiplayerLocalUserId localOnlineUser)
	{
		if (m_Status.Progress != eConnectionModeSwitchProgress.Complete)
		{
			m_Status.Progress = eConnectionModeSwitchProgress.Complete;
			m_Status.privilegeCheckResult = result;
			if (result != null && result.m_returnCode == OnlineMultiplayerPrivilegeCheckResult.eSuccess && localOnlineUser != null)
			{
				m_LocalUserId = localOnlineUser;
				m_Status.Result = eConnectionModeSwitchResult.Success;
				PrivilegeCheckCache.AddAllowedUser(m_UserToCheck, localOnlineUser);
			}
			else
			{
				m_Status.Result = eConnectionModeSwitchResult.Failure;
			}
		}
	}
}
