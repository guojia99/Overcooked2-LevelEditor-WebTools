using System;
using System.Collections.Generic;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;

public class HighScoreRepository
{
	private int m_iDLC = -1;

	private List<int> m_TempDelete = new List<int>();

	private Dictionary<int, GameProgress.HighScores> m_Data = new Dictionary<int, GameProgress.HighScores>();

	public int DLC
	{
		get
		{
			return m_iDLC;
		}
	}

	public void Initialise(int DLC)
	{
		m_iDLC = DLC;
		Mailbox.Server.RegisterForMessageType(MessageType.HighScores, OnMessageReceived);
		Mailbox.Client.RegisterForMessageType(MessageType.HighScores, OnMessageReceived);
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Combine(ClientUserSystem.usersChanged, new GenericVoid(OnUsersChanged));
	}

	public void Shutdown()
	{
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Remove(ClientUserSystem.usersChanged, new GenericVoid(OnUsersChanged));
		Mailbox.Client.UnregisterForMessageType(MessageType.HighScores, OnMessageReceived);
		Mailbox.Server.UnregisterForMessageType(MessageType.HighScores, OnMessageReceived);
	}

	public void Clear()
	{
		m_Data.Clear();
	}

	public void OnMessageReceived(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		HighScoresMessage highScoresMessage = (HighScoresMessage)message;
		SetScoresForMachine(highScoresMessage.m_Machine, highScoresMessage.DLC, highScoresMessage.HighScores);
	}

	public void SetScoresForMachine(User.MachineID machine, int DLC, GameProgress.HighScores highScores)
	{
		if (m_iDLC != DLC)
		{
			Clear();
			m_iDLC = DLC;
		}
		if (m_Data.ContainsKey((int)machine))
		{
			m_Data[(int)machine] = highScores.Copy();
		}
		else
		{
			m_Data.Add((int)machine, highScores.Copy());
		}
	}

	public void LevelProgress(GameProgress.HighScores.Score score)
	{
		for (int i = 0; i < 4; i++)
		{
			User.MachineID machineID = (User.MachineID)i;
			if (UserSystemUtils.FindUser(ClientUserSystem.m_Users, null, machineID) == null)
			{
				continue;
			}
			GameProgress.HighScores value;
			if (!m_Data.TryGetValue((int)machineID, out value))
			{
				value = new GameProgress.HighScores();
				m_Data.Add((int)machineID, value);
			}
			int num = value.Scores.FindIndex((GameProgress.HighScores.Score item) => item.iLevelID == score.iLevelID);
			if (num >= 0 && num < value.Scores.Count)
			{
				int iHighScore = value.Scores[num].iHighScore;
				if (iHighScore < score.iHighScore || iHighScore == 65535)
				{
					value.Scores[num].iHighScore = score.iHighScore;
				}
				int iSurvivalModeTime = value.Scores[num].iSurvivalModeTime;
				if (iSurvivalModeTime < score.iSurvivalModeTime || iSurvivalModeTime == 0)
				{
					value.Scores[num].iSurvivalModeTime = score.iSurvivalModeTime;
				}
			}
			else
			{
				value.Scores.Add(score);
			}
		}
	}

	public bool GetScore(User.MachineID machine, int iLevelID, ref GameProgress.HighScores.Score score)
	{
		GameProgress.HighScores value;
		if (m_Data.TryGetValue((int)machine, out value))
		{
			score = value.Scores.Find((GameProgress.HighScores.Score find) => find.iLevelID == iLevelID);
			return score != null;
		}
		return false;
	}

	public void Fill(HighScoreRepository otherRepo, bool copy)
	{
		if (copy)
		{
			Clear();
			{
				foreach (KeyValuePair<int, GameProgress.HighScores> datum in otherRepo.m_Data)
				{
					m_Data.Add(datum.Key, datum.Value.Copy());
				}
				return;
			}
		}
		m_Data = otherRepo.m_Data;
	}

	private void OnUsersChanged()
	{
		m_TempDelete.Clear();
		foreach (KeyValuePair<int, GameProgress.HighScores> datum in m_Data)
		{
			if (UserSystemUtils.FindUser(ClientUserSystem.m_Users, null, (User.MachineID)datum.Key) == null)
			{
				m_TempDelete.Add(datum.Key);
			}
		}
		for (int i = 0; i < m_TempDelete.Count; i++)
		{
			m_Data.Remove(m_TempDelete[i]);
		}
		m_TempDelete.Clear();
	}
}
