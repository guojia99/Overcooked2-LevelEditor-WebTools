using System.Collections.Generic;
using Team17.Online;

public class JoinEnumeratedRoomTask : JoinSessionBaseTask
{
	private IOnlineMultiplayerSessionCoordinator m_iOnlineMultiplayerSessionCoordinator;

	private OnlineMultiplayerSessionEnumeratedRoom m_room;

	private List<OnlineMultiplayerSessionJoinLocalUserData> m_localUsersJoinData = new List<OnlineMultiplayerSessionJoinLocalUserData>((int)OnlineMultiplayerConfig.MaxPlayers);

	private NetConnectionState m_requestingAgent = NetConnectionState.COUNT;

	public void Initialise(OnlineMultiplayerSessionEnumeratedRoom room, NetConnectionState requestingAgent)
	{
		m_localUsersJoinData.Clear();
		m_Status.currentUser = null;
		m_room = room;
		m_requestingAgent = requestingAgent;
		m_Status.Progress = eConnectionModeSwitchProgress.NotStarted;
		m_Status.Result = eConnectionModeSwitchResult.NotAvailableYet;
	}

	public override void Start(object startData)
	{
		IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
		m_iOnlineMultiplayerSessionCoordinator = onlinePlatformManager.OnlineMultiplayerSessionCoordinator();
		List<UserData> list = startData as List<UserData>;
		if (list != null && list.Count > 0)
		{
			m_Status.currentUser = list[0].UserId;
			bool flag = false;
			for (int i = 0; i < list.Count; i++)
			{
				if (flag)
				{
					break;
				}
				OnlineMultiplayerSessionJoinLocalUserData onlineMultiplayerSessionJoinLocalUserData = JoinDataProvider.BuildJoinRequestData(list[i].Slot, m_requestingAgent, list[i].UserId);
				if (onlineMultiplayerSessionJoinLocalUserData != null)
				{
					m_localUsersJoinData.Add(onlineMultiplayerSessionJoinLocalUserData);
					continue;
				}
				m_localUsersJoinData.Clear();
				flag = true;
				break;
			}
			list.Clear();
		}
		base.Start(startData);
	}

	public override void TryStart()
	{
		if (m_iOnlineMultiplayerSessionCoordinator.IsIdle())
		{
			int num = (int)(OnlineMultiplayerConfig.MaxPlayers - 1);
			if (m_localUsersJoinData != null && m_localUsersJoinData.Count > num)
			{
				m_Status.sessionJoinResult = new OnlineMultiplayerReturnCode<OnlineMultiplayerSessionJoinResult>
				{
					m_returnCode = OnlineMultiplayerSessionJoinResult.eNotEnoughRoomForAllLocalUsers
				};
				m_Status.Progress = eConnectionModeSwitchProgress.Complete;
				m_Status.Result = eConnectionModeSwitchResult.Failure;
			}
			else if (m_iOnlineMultiplayerSessionCoordinator.Join(m_localUsersJoinData, m_room, base.OnlineMultiplayerSessionJoinCallback))
			{
				m_Status.Progress = eConnectionModeSwitchProgress.InProgress;
				m_Status.Result = eConnectionModeSwitchResult.NotAvailableYet;
			}
			else
			{
				m_Status.sessionJoinResult = new OnlineMultiplayerReturnCode<OnlineMultiplayerSessionJoinResult>
				{
					m_returnCode = OnlineMultiplayerSessionJoinResult.eGenericFailure
				};
				m_Status.Progress = eConnectionModeSwitchProgress.Complete;
				m_Status.Result = eConnectionModeSwitchResult.Failure;
			}
			m_localUsersJoinData.Clear();
			m_room = null;
		}
	}
}
