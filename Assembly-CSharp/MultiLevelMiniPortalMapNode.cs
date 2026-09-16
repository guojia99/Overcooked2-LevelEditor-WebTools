using System.Collections.Generic;
using UnityEngine;

[ExecutionDependency(typeof(BootstrapManager))]
public class MultiLevelMiniPortalMapNode : MiniLevelPortalMapNode
{
	[Space]
	[SerializeField]
	[LevelIndex]
	private int[] m_altLevels = new int[0];

	private int m_altLevelIndex = -1;

	public override int LevelIndex
	{
		get
		{
			if (m_altLevelIndex != -1)
			{
				return m_altLevelIndex;
			}
			return base.LevelIndex;
		}
	}

	public int[] AlternateLevelIndexes
	{
		get
		{
			return m_altLevels;
		}
	}

	protected override void Awake()
	{
		base.Awake();
		KeyValuePair<int, int> keyValuePair = m_altLevels.FindHighestScoring(LevelScoreFunction);
		if (keyValuePair.Key != -1)
		{
			m_altLevelIndex = keyValuePair.Value;
		}
	}

	private static float LevelScoreFunction(int _levelIndex)
	{
		GameSession gameSession = GameUtils.GetGameSession();
		if (gameSession.Progress.SaveData.IsLevelUnlocked(_levelIndex, false))
		{
			return _levelIndex;
		}
		return float.MinValue;
	}
}
