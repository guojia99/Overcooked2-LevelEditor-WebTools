using System.Collections.Generic;
using Team17.Online;
using Team17.Online.Multiplayer;

public class CreateSessionTask : IMultiplayerTask
{
	private Server m_Server;

	private Client m_Client;

	private OnlineMultiplayerSessionVisibility m_Visibility = OnlineMultiplayerSessionVisibility.eClosed;

	private OnlineMultiplayerSessionPlayTogetherHosting m_PlayTogether;

	private bool m_bStarted;

	private OnlineMultiplayerLocalUserId m_LocalUserId;

	private List<OnlineMultiplayerSessionPropertyValue> m_PropertyValues;

	private CreateSessionStatus m_Status = new CreateSessionStatus();

	private IOnlineMultiplayerSessionCoordinator m_iOnlineMultiplayerSessionCoordinator;

	public void Initialise(Server server, Client client, OnlineMultiplayerSessionVisibility visibility, OnlineMultiplayerSessionPlayTogetherHosting playTogether, List<OnlineMultiplayerSessionPropertyValue> values)
	{
		m_Server = server;
		m_Client = client;
		m_Visibility = visibility;
		m_PlayTogether = playTogether;
		m_PropertyValues = values;
		IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
		m_iOnlineMultiplayerSessionCoordinator = onlinePlatformManager.OnlineMultiplayerSessionCoordinator();
	}

	public void Start(object startData)
	{
		OnlineMultiplayerLocalUserId onlineMultiplayerLocalUserId = startData as OnlineMultiplayerLocalUserId;
		if (onlineMultiplayerLocalUserId == null)
		{
			m_Status.sessionCreateResult = new OnlineMultiplayerReturnCode<OnlineMultiplayerSessionCreateResult>
			{
				m_returnCode = OnlineMultiplayerSessionCreateResult.eGenericFailure
			};
			m_Status.Progress = eConnectionModeSwitchProgress.Complete;
			m_Status.Result = eConnectionModeSwitchResult.Failure;
		}
		else if (m_iOnlineMultiplayerSessionCoordinator == null)
		{
			m_Status.sessionCreateResult = new OnlineMultiplayerReturnCode<OnlineMultiplayerSessionCreateResult>
			{
				m_returnCode = OnlineMultiplayerSessionCreateResult.eGenericFailure
			};
			m_Status.Progress = eConnectionModeSwitchProgress.Complete;
			m_Status.Result = eConnectionModeSwitchResult.Failure;
		}
		else if (m_LocalUserId != onlineMultiplayerLocalUserId || ((!m_iOnlineMultiplayerSessionCoordinator.IsHost() || m_Status.Progress != eConnectionModeSwitchProgress.Complete || m_Status.Result != eConnectionModeSwitchResult.Success) && (m_Status.Progress != eConnectionModeSwitchProgress.InProgress || m_Status.Result != eConnectionModeSwitchResult.NotAvailableYet)))
		{
			if (!m_iOnlineMultiplayerSessionCoordinator.IsIdle())
			{
				m_Status.sessionCreateResult = new OnlineMultiplayerReturnCode<OnlineMultiplayerSessionCreateResult>
				{
					m_returnCode = OnlineMultiplayerSessionCreateResult.eGenericFailure
				};
				m_Status.Progress = eConnectionModeSwitchProgress.Complete;
				m_Status.Result = eConnectionModeSwitchResult.Failure;
			}
			else
			{
				m_LocalUserId = onlineMultiplayerLocalUserId;
				m_Status.currentUser = m_LocalUserId;
				m_Status.Progress = eConnectionModeSwitchProgress.NotStarted;
				m_Status.Result = eConnectionModeSwitchResult.NotAvailableYet;
				m_bStarted = false;
				TryStart();
			}
		}
	}

	public void Stop()
	{
		m_Status.Progress = eConnectionModeSwitchProgress.NotStarted;
		m_Status.Result = eConnectionModeSwitchResult.NotAvailableYet;
	}

	private void TryStart()
	{
		if (!m_bStarted)
		{
			string sessionName = Localization.Get("Online.RoomName", new LocToken("[HostName]", m_LocalUserId.m_userName));
			m_bStarted = m_iOnlineMultiplayerSessionCoordinator.Create(m_LocalUserId, m_PropertyValues, m_Visibility, sessionName, m_PlayTogether, OnlineMultiplayerSessionCreateCallback, m_Server.OnlineMultiplayerSessionJoinDecisionCallback, m_Server.OnlineMultiplayerSessionUserJoinedCallback);
			if (m_bStarted)
			{
				m_Status.Progress = eConnectionModeSwitchProgress.InProgress;
				m_Status.Result = eConnectionModeSwitchResult.NotAvailableYet;
				return;
			}
			m_Status.sessionCreateResult = new OnlineMultiplayerReturnCode<OnlineMultiplayerSessionCreateResult>
			{
				m_returnCode = OnlineMultiplayerSessionCreateResult.eGenericFailure
			};
			m_Status.Progress = eConnectionModeSwitchProgress.Complete;
			m_Status.Result = eConnectionModeSwitchResult.Failure;
		}
	}

	public void Update()
	{
		TryStart();
	}

	public object GetData()
	{
		return (m_Status.Result != eConnectionModeSwitchResult.Success) ? null : m_LocalUserId;
	}

	public IConnectionModeSwitchStatus GetStatus()
	{
		return m_Status;
	}

	private void OnlineMultiplayerSessionCreateCallback(OnlineMultiplayerReturnCode<OnlineMultiplayerSessionCreateResult> result)
	{
		m_Status.sessionCreateResult = result;
		if (result != null && result.m_returnCode == OnlineMultiplayerSessionCreateResult.eSuccess)
		{
			NetworkSystemConfigurator.Server(m_Server, m_Client, m_LocalUserId);
			m_Status.Result = eConnectionModeSwitchResult.Success;
		}
		else
		{
			m_Status.Result = eConnectionModeSwitchResult.Failure;
		}
		m_Status.Progress = eConnectionModeSwitchProgress.Complete;
	}
}
