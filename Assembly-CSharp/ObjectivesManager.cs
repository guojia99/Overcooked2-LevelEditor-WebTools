using System;
using System.Collections.Generic;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ObjectivesManager : Manager
{
	protected List<LevelObjectiveBase> m_levelObjectives = new List<LevelObjectiveBase>();

	private void Start()
	{
		LevelConfigBase levelConfig = GameUtils.GetLevelConfig();
		if (levelConfig.m_objectives != null)
		{
			for (int i = 0; i < levelConfig.m_objectives.Length; i++)
			{
				LevelObjectiveBase item = UnityEngine.Object.Instantiate(levelConfig.m_objectives[i]);
				m_levelObjectives.Add(item);
			}
			Mailbox.Client.RegisterForMessageType(MessageType.GameState, OnGameStateChanged);
		}
	}

	public void OnDestroy()
	{
		int count = m_levelObjectives.Count;
		for (int i = 0; i < count; i++)
		{
			m_levelObjectives[i].CleanUp();
			UnityEngine.Object.Destroy(m_levelObjectives[i]);
		}
		m_levelObjectives.Clear();
		Mailbox.Client.UnregisterForMessageType(MessageType.GameState, OnGameStateChanged);
	}

	private void OnGameStateChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		GameStateMessage gameStateMessage = (GameStateMessage)message;
		if (gameStateMessage.m_State == GameState.StartEntities)
		{
			int count = m_levelObjectives.Count;
			for (int i = 0; i < count; i++)
			{
				m_levelObjectives[i].Initialise();
			}
		}
	}

	public bool HasObjectives(bool includeComplete = true)
	{
		if (!includeComplete)
		{
			for (int i = 0; i < m_levelObjectives.Count; i++)
			{
				if (!m_levelObjectives[i].IsObjectiveComplete())
				{
					return true;
				}
			}
		}
		return m_levelObjectives.Count > 0;
	}

	public bool HasObjective(Type objectiveType, bool includeComplete = true)
	{
		for (int i = 0; i < m_levelObjectives.Count; i++)
		{
			if ((includeComplete || !m_levelObjectives[i].IsObjectiveComplete()) && m_levelObjectives[i].GetType() == objectiveType)
			{
				return true;
			}
		}
		return false;
	}

	public bool AllObjectivesComplete()
	{
		bool flag = true;
		for (int i = 0; i < m_levelObjectives.Count; i++)
		{
			flag &= m_levelObjectives[i].IsObjectiveComplete();
		}
		return flag;
	}

	public bool IsObjectiveComplete(Type objectiveType)
	{
		for (int i = 0; i < m_levelObjectives.Count; i++)
		{
			if (m_levelObjectives[i].GetType() == objectiveType)
			{
				return m_levelObjectives[i].IsObjectiveComplete();
			}
		}
		return false;
	}
}
