using System.Collections.Generic;
using Team17.Online;

public class AcceptInviteTask : JoinSessionBaseTask
{
	private OnlineMultiplayerSessionInvite m_Invite;

	private IOnlineMultiplayerSessionCoordinator m_iOnlineMultiplayerSessionCoordinator;

	private List<OnlineMultiplayerSessionJoinLocalUserData> m_localUsersJoinData = new List<OnlineMultiplayerSessionJoinLocalUserData>((int)(OnlineMultiplayerConfig.MaxPlayers - 1));

	public void Initialise(OnlineMultiplayerSessionInvite invite)
	{
		m_Invite = invite;
	}

	public override void Start(object startData)
	{
		IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
		m_iOnlineMultiplayerSessionCoordinator = onlinePlatformManager.OnlineMultiplayerSessionCoordinator();
		m_localUsersJoinData.Clear();
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
				OnlineMultiplayerSessionJoinLocalUserData onlineMultiplayerSessionJoinLocalUserData = JoinDataProvider.BuildJoinRequestData(list[i].Slot, NetConnectionState.AcceptInvite, list[i].UserId);
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
		if (!m_iOnlineMultiplayerSessionCoordinator.IsIdle())
		{
			return;
		}
		if (m_localUsersJoinData.Count > 0)
		{
			if (m_iOnlineMultiplayerSessionCoordinator.Join(m_localUsersJoinData, m_Invite, base.OnlineMultiplayerSessionJoinCallback))
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
		m_Invite = null;
		m_localUsersJoinData.Clear();
	}
}
