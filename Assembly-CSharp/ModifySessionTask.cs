using System.Collections.Generic;
using Team17.Online;

public class ModifySessionTask : IMultiplayerTask
{
	private IOnlineMultiplayerSessionCoordinator m_iOnlineMultiplayerSessionCoordinator;

	private ModifySessionStatus m_Status = new ModifySessionStatus();

	private List<OnlineMultiplayerSessionPropertyValue> m_PropertyValues;

	private OnlineMultiplayerSessionVisibility m_Visibility = OnlineMultiplayerSessionVisibility.eClosed;

	public void Initialise(IOnlineMultiplayerSessionCoordinator sessionCoordinator, OnlineMultiplayerSessionVisibility visibility, List<OnlineMultiplayerSessionPropertyValue> values)
	{
		m_Visibility = visibility;
		m_PropertyValues = values;
		m_iOnlineMultiplayerSessionCoordinator = sessionCoordinator;
	}

	public void Start(object startData)
	{
		if (m_iOnlineMultiplayerSessionCoordinator.Modify(m_PropertyValues, m_Visibility))
		{
			ServerGameSetup.Mode = ServerSessionPropertyValuesProvider.GetGameMode();
			m_Status.Progress = eConnectionModeSwitchProgress.Complete;
			m_Status.Result = eConnectionModeSwitchResult.Success;
		}
		else
		{
			m_Status.Progress = eConnectionModeSwitchProgress.Complete;
			m_Status.Result = eConnectionModeSwitchResult.Failure;
		}
	}

	public void Stop()
	{
		m_Status.Progress = eConnectionModeSwitchProgress.NotStarted;
		m_Status.Result = eConnectionModeSwitchResult.NotAvailableYet;
	}

	public void Update()
	{
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
