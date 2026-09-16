using System.Collections.Generic;

public class JoinRandomRoomTask : IMultiplayerTask
{
	private SearchStatus m_Status = new SearchStatus();

	private int m_iJoinAttemptIndex;

	private SearchTask.SearchResultData m_SearchData = new SearchTask.SearchResultData();

	private JoinEnumeratedRoomTask m_JoinRoomTask = new JoinEnumeratedRoomTask();

	private List<JoinSessionBaseTask.UserData> m_joinUserData = new List<JoinSessionBaseTask.UserData>(1);

	public void Start(object startData)
	{
		m_Status.Progress = eConnectionModeSwitchProgress.NotStarted;
		m_Status.Result = eConnectionModeSwitchResult.NotAvailableYet;
		m_joinUserData.Clear();
		m_iJoinAttemptIndex = 0;
		m_SearchData = startData as SearchTask.SearchResultData;
		GameUtils.s_RoomSearch_NoneAvailable = false;
		if (m_SearchData == null || m_SearchData.m_AvailableSessions == null || m_SearchData.m_LocalUser == null)
		{
			if (m_SearchData != null && m_SearchData.m_AvailableSessions == null)
			{
				GameUtils.s_RoomSearch_NoneAvailable = true;
			}
			Fail();
		}
		else
		{
			JoinGame();
		}
	}

	public void Stop()
	{
		m_Status.Progress = eConnectionModeSwitchProgress.NotStarted;
		m_Status.Result = eConnectionModeSwitchResult.NotAvailableYet;
	}

	private void JoinGame()
	{
		if (m_iJoinAttemptIndex == m_SearchData.m_AvailableSessions.Count)
		{
			Fail();
			return;
		}
		m_Status.Progress = eConnectionModeSwitchProgress.InProgress;
		m_Status.Result = eConnectionModeSwitchResult.NotAvailableYet;
		m_joinUserData.Add(new JoinSessionBaseTask.UserData
		{
			UserId = m_SearchData.m_LocalUser,
			Slot = EngagementSlot.One
		});
		m_JoinRoomTask.Initialise(m_SearchData.m_AvailableSessions[m_iJoinAttemptIndex], NetConnectionState.Matchmake);
		m_JoinRoomTask.Start(m_joinUserData);
		m_joinUserData.Clear();
	}

	public void Update()
	{
		switch (m_JoinRoomTask.GetStatus().GetProgress())
		{
		case eConnectionModeSwitchProgress.NotStarted:
			JoinGame();
			break;
		case eConnectionModeSwitchProgress.Complete:
			UpdateComplete();
			break;
		}
	}

	private void UpdateComplete()
	{
		switch (m_JoinRoomTask.GetStatus().GetResult())
		{
		case eConnectionModeSwitchResult.NotAvailableYet:
			Fail();
			break;
		case eConnectionModeSwitchResult.Failure:
			m_iJoinAttemptIndex++;
			JoinGame();
			break;
		case eConnectionModeSwitchResult.Success:
			Success();
			break;
		}
	}

	private void Fail()
	{
		m_SearchData.m_AvailableSessions = null;
		m_SearchData.m_LocalUser = null;
		m_Status.Progress = eConnectionModeSwitchProgress.Complete;
		m_Status.Result = eConnectionModeSwitchResult.Failure;
	}

	private void Success()
	{
		m_Status.Progress = eConnectionModeSwitchProgress.Complete;
		m_Status.Result = eConnectionModeSwitchResult.Success;
	}

	public IConnectionModeSwitchStatus GetStatus()
	{
		return m_Status;
	}

	public object GetData()
	{
		return m_JoinRoomTask.GetData();
	}
}
