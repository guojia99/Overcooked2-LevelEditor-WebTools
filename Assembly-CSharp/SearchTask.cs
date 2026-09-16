using System.Collections.Generic;
using Team17.Online;

public class SearchTask : IMultiplayerTask
{
	public class SearchResultData
	{
		public List<OnlineMultiplayerSessionEnumeratedRoom> m_AvailableSessions;

		public OnlineMultiplayerLocalUserId m_LocalUser;
	}

	private IOnlineMultiplayerSessionEnumerateCoordinator m_iOnlineMultiplayerSessionEnumerater;

	private SearchStatus m_Status = new SearchStatus();

	private OnlineMultiplayerLocalUserId m_LocalUser;

	private List<OnlineMultiplayerSessionPropertySearchValue> m_FilterParameter;

	private SearchResultData m_SearchResultData = new SearchResultData();

	private bool m_registeredForErrors;

	public void Initialise(List<OnlineMultiplayerSessionPropertySearchValue> filterParameter)
	{
		m_FilterParameter = filterParameter;
	}

	public void Start(object startData)
	{
		m_Status.Progress = eConnectionModeSwitchProgress.NotStarted;
		m_Status.Result = eConnectionModeSwitchResult.NotAvailableYet;
		IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
		m_iOnlineMultiplayerSessionEnumerater = onlinePlatformManager.OnlineMultiplayerSessionEnumerateCoordinator();
		if (!m_iOnlineMultiplayerSessionEnumerater.IsIdle())
		{
			m_iOnlineMultiplayerSessionEnumerater.Cancel();
		}
		m_LocalUser = startData as OnlineMultiplayerLocalUserId;
		TryStart();
	}

	public void Stop()
	{
		if (m_registeredForErrors)
		{
			IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
			IOnlineMultiplayerConnectionModeCoordinator onlineMultiplayerConnectionModeCoordinator = onlinePlatformManager.OnlineMultiplayerConnectionModeCoordinator();
			if (onlineMultiplayerConnectionModeCoordinator != null)
			{
				onlineMultiplayerConnectionModeCoordinator.UnRegisterErrorCallback(OnlineMultiplayerConnectionModeErrorCallback);
			}
			m_registeredForErrors = false;
		}
		m_Status.Progress = eConnectionModeSwitchProgress.NotStarted;
		m_Status.Result = eConnectionModeSwitchResult.NotAvailableYet;
	}

	public void TryStart()
	{
		if (!m_iOnlineMultiplayerSessionEnumerater.IsIdle())
		{
			return;
		}
		if (m_iOnlineMultiplayerSessionEnumerater.Start(m_LocalUser, m_FilterParameter, OnlineMultiplayerConfig.MaxBrowsedSessionsToEnumerate, OnlineMultiplayerSessionEnumerateCallback))
		{
			m_Status.Progress = eConnectionModeSwitchProgress.InProgress;
			m_Status.Result = eConnectionModeSwitchResult.NotAvailableYet;
			if (!m_registeredForErrors)
			{
				IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
				IOnlineMultiplayerConnectionModeCoordinator onlineMultiplayerConnectionModeCoordinator = onlinePlatformManager.OnlineMultiplayerConnectionModeCoordinator();
				if (onlineMultiplayerConnectionModeCoordinator != null)
				{
					onlineMultiplayerConnectionModeCoordinator.RegisterErrorCallback(OnlineMultiplayerConnectionModeErrorCallback);
				}
				m_registeredForErrors = true;
			}
		}
		else
		{
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

	public void OnlineMultiplayerConnectionModeErrorCallback(OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeErrorResult> result)
	{
		IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
		IOnlineMultiplayerConnectionModeCoordinator onlineMultiplayerConnectionModeCoordinator = onlinePlatformManager.OnlineMultiplayerConnectionModeCoordinator();
		if (onlineMultiplayerConnectionModeCoordinator != null)
		{
			onlineMultiplayerConnectionModeCoordinator.UnRegisterErrorCallback(OnlineMultiplayerConnectionModeErrorCallback);
		}
		m_registeredForErrors = false;
		m_SearchResultData.m_AvailableSessions = null;
		m_SearchResultData.m_LocalUser = null;
		result.DisplayPlatformSpecificError();
		m_Status.Progress = eConnectionModeSwitchProgress.Complete;
		m_Status.Result = eConnectionModeSwitchResult.Failure;
	}

	public IConnectionModeSwitchStatus GetStatus()
	{
		return m_Status;
	}

	public object GetData()
	{
		return m_SearchResultData;
	}

	private void OnlineMultiplayerSessionEnumerateCallback(List<OnlineMultiplayerSessionEnumeratedRoom> availableSessions, bool requestSuccessful)
	{
		if (requestSuccessful)
		{
			m_SearchResultData.m_AvailableSessions = availableSessions;
			m_SearchResultData.m_LocalUser = m_LocalUser;
			m_Status.Result = eConnectionModeSwitchResult.Success;
		}
		else
		{
			m_SearchResultData.m_AvailableSessions = null;
			m_SearchResultData.m_LocalUser = null;
			m_Status.Result = eConnectionModeSwitchResult.Failure;
		}
		if (m_registeredForErrors)
		{
			IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
			IOnlineMultiplayerConnectionModeCoordinator onlineMultiplayerConnectionModeCoordinator = onlinePlatformManager.OnlineMultiplayerConnectionModeCoordinator();
			if (onlineMultiplayerConnectionModeCoordinator != null)
			{
				onlineMultiplayerConnectionModeCoordinator.UnRegisterErrorCallback(OnlineMultiplayerConnectionModeErrorCallback);
			}
			m_registeredForErrors = false;
		}
		m_Status.Progress = eConnectionModeSwitchProgress.Complete;
	}
}
