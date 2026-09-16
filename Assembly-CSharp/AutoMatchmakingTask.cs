using System.Collections.Generic;
using Team17.Online;
using Team17.Online.Multiplayer;

public class AutoMatchmakingTask : JoinSessionBaseTask
{
	private IOnlineMultiplayerSessionCoordinator m_iOnlineMultiplayerSessionCoordinator;

	private OnlineMultiplayerLocalUserId m_LocalUser;

	private List<OnlineMultiplayerSessionPropertySearchValue> m_FilterParameter;

	private Server m_Server;

	public void Initialise(List<OnlineMultiplayerSessionPropertySearchValue> filterParameter, Server server, Client client)
	{
		m_FilterParameter = filterParameter;
		m_Server = server;
		if (!(m_Status is AutoMatchmakingStatus))
		{
			m_Status = new AutoMatchmakingStatus();
		}
	}

	public override void Start(object startData)
	{
		IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
		m_iOnlineMultiplayerSessionCoordinator = onlinePlatformManager.OnlineMultiplayerSessionCoordinator();
		if (!m_iOnlineMultiplayerSessionCoordinator.IsIdle())
		{
			m_Status.sessionJoinResult = new OnlineMultiplayerReturnCode<OnlineMultiplayerSessionJoinResult>
			{
				m_returnCode = OnlineMultiplayerSessionJoinResult.eGenericFailure
			};
			m_Status.Progress = eConnectionModeSwitchProgress.Complete;
			m_Status.Result = eConnectionModeSwitchResult.Failure;
		}
		else
		{
			m_LocalUser = startData as OnlineMultiplayerLocalUserId;
			m_Status.currentUser = m_LocalUser;
			base.Start(startData);
		}
	}

	public override void TryStart()
	{
		if (m_iOnlineMultiplayerSessionCoordinator.IsIdle())
		{
			List<OnlineMultiplayerSessionPropertyValue> values = ServerSessionPropertyValuesProvider.GetValues();
			for (int i = 0; i < values.Count; i++)
			{
			}
			for (int j = 0; j < m_FilterParameter.Count; j++)
			{
			}
			OnlineMultiplayerSessionJoinLocalUserData onlineMultiplayerSessionJoinLocalUserData = JoinDataProvider.BuildJoinRequestData(EngagementSlot.One, NetConnectionState.Matchmake, m_LocalUser);
			if (onlineMultiplayerSessionJoinLocalUserData != null && m_iOnlineMultiplayerSessionCoordinator.AutoMatchmake(onlineMultiplayerSessionJoinLocalUserData, ServerSessionPropertyValuesProvider.GetValues(), "placeholder room name", m_Server.OnlineMultiplayerSessionJoinDecisionCallback, m_Server.OnlineMultiplayerSessionUserJoinedCallback, m_FilterParameter, base.OnlineMultiplayerSessionJoinCallback))
			{
				m_Status.Progress = eConnectionModeSwitchProgress.InProgress;
				m_Status.Result = eConnectionModeSwitchResult.NotAvailableYet;
				return;
			}
			m_Status.sessionJoinResult = new OnlineMultiplayerReturnCode<OnlineMultiplayerSessionJoinResult>
			{
				m_returnCode = OnlineMultiplayerSessionJoinResult.eGenericFailure
			};
			m_Status.Progress = eConnectionModeSwitchProgress.Complete;
			m_Status.Result = eConnectionModeSwitchResult.Failure;
		}
	}
}
