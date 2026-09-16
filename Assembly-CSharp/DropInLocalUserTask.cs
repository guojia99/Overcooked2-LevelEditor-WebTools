using Team17.Online;

public class DropInLocalUserTask : IMultiplayerTask
{
	private IOnlineMultiplayerSessionCoordinator m_iOnlineMultiplayerSessionCoordinator;

	private OnlineMultiplayerLocalUserId m_LocalUserId;

	private GamepadUser m_UserToCheck;

	private JoinSessionStatus m_Status = new JoinSessionStatus();

	private IPlayerManager m_PlayerManager;

	public void Initialise(GamepadUser userToDropIn)
	{
		IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
		m_iOnlineMultiplayerSessionCoordinator = onlinePlatformManager.OnlineMultiplayerSessionCoordinator();
		m_PlayerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
		m_UserToCheck = userToDropIn;
		m_Status.currentUser = m_LocalUserId;
		m_Status.Progress = eConnectionModeSwitchProgress.NotStarted;
		m_Status.Result = eConnectionModeSwitchResult.NotAvailableYet;
	}

	public void Reset()
	{
		m_LocalUserId = null;
		m_UserToCheck = null;
		m_Status.currentUser = null;
		m_Status.Progress = eConnectionModeSwitchProgress.NotStarted;
		m_Status.Result = eConnectionModeSwitchResult.NotAvailableYet;
	}

	public void Start(object startData)
	{
		m_LocalUserId = (OnlineMultiplayerLocalUserId)startData;
		m_Status.currentUser = m_LocalUserId;
		if (m_iOnlineMultiplayerSessionCoordinator == null || LocalDroppedInUserCache.HasBeenDroppedIn(m_LocalUserId))
		{
			m_Status.Progress = eConnectionModeSwitchProgress.Complete;
			m_Status.Result = eConnectionModeSwitchResult.Success;
			m_Status.sessionJoinResult = new OnlineMultiplayerReturnCode<OnlineMultiplayerSessionJoinResult>
			{
				m_returnCode = OnlineMultiplayerSessionJoinResult.eSuccess
			};
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
		m_PlayerManager.EngagementChangeCallback -= OnEngagementChanged;
	}

	public void OnEngagementChanged(EngagementSlot _e, GamepadUser _prev, GamepadUser _new)
	{
		if (_new == null && _prev != null && _prev == m_UserToCheck && m_Status.GetProgress() != eConnectionModeSwitchProgress.Complete && m_iOnlineMultiplayerSessionCoordinator != null)
		{
			m_iOnlineMultiplayerSessionCoordinator.RemoveNonPrimaryLocalUser(m_LocalUserId);
			m_Status.sessionJoinResult = new OnlineMultiplayerReturnCode<OnlineMultiplayerSessionJoinResult>
			{
				m_returnCode = OnlineMultiplayerSessionJoinResult.eGenericFailure
			};
			m_Status.Progress = eConnectionModeSwitchProgress.Complete;
			m_Status.Result = eConnectionModeSwitchResult.Failure;
		}
	}

	public void Update()
	{
		if (m_Status.Progress == eConnectionModeSwitchProgress.NotStarted && m_Status.Result == eConnectionModeSwitchResult.NotAvailableYet)
		{
			TryStart();
		}
	}

	public object GetData()
	{
		return null;
	}

	public IConnectionModeSwitchStatus GetStatus()
	{
		return m_Status;
	}

	public void TryStart()
	{
		if (m_Status.Progress != eConnectionModeSwitchProgress.NotStarted)
		{
			return;
		}
		if (m_iOnlineMultiplayerSessionCoordinator.IsIdle())
		{
			m_Status.Progress = eConnectionModeSwitchProgress.Complete;
			m_Status.Result = eConnectionModeSwitchResult.Failure;
			return;
		}
		switch (m_iOnlineMultiplayerSessionCoordinator.AddNonPrimaryLocalUser(m_LocalUserId, OnlineMultiplayerSessionAddNonPrimaryLocalUserCallback))
		{
		case OnlineMultiplayerNonPrimaryLocalUserChangeResult.eStarted:
			m_Status.Progress = eConnectionModeSwitchProgress.InProgress;
			m_Status.Result = eConnectionModeSwitchResult.NotAvailableYet;
			break;
		case OnlineMultiplayerNonPrimaryLocalUserChangeResult.eComplete:
			LocalDroppedInUserCache.AddDroppedInUser(m_LocalUserId);
			m_Status.Progress = eConnectionModeSwitchProgress.Complete;
			m_Status.Result = eConnectionModeSwitchResult.Success;
			m_Status.sessionJoinResult = new OnlineMultiplayerReturnCode<OnlineMultiplayerSessionJoinResult>
			{
				m_returnCode = OnlineMultiplayerSessionJoinResult.eSuccess
			};
			break;
		case OnlineMultiplayerNonPrimaryLocalUserChangeResult.eBusy:
			break;
		case OnlineMultiplayerNonPrimaryLocalUserChangeResult.eNotPossible:
			m_Status.Progress = eConnectionModeSwitchProgress.Complete;
			m_Status.Result = eConnectionModeSwitchResult.Failure;
			m_Status.sessionJoinResult = new OnlineMultiplayerReturnCode<OnlineMultiplayerSessionJoinResult>
			{
				m_returnCode = OnlineMultiplayerSessionJoinResult.eGenericFailure
			};
			break;
		}
	}

	private void OnlineMultiplayerSessionAddNonPrimaryLocalUserCallback(OnlineMultiplayerLocalUserId localUserId, OnlineMultiplayerReturnCode<OnlineMultiplayerSessionJoinResult> result)
	{
		m_Status.Progress = eConnectionModeSwitchProgress.Complete;
		m_Status.sessionJoinResult = result;
		if (result != null && result.m_returnCode == OnlineMultiplayerSessionJoinResult.eSuccess && localUserId != null)
		{
			m_Status.Result = eConnectionModeSwitchResult.Success;
			LocalDroppedInUserCache.AddDroppedInUser(m_LocalUserId);
		}
		else
		{
			m_Status.Result = eConnectionModeSwitchResult.Failure;
		}
	}
}
