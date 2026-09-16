using UnityEngine;

public class SwitchMapNode : MapNode
{
	private int m_switchID = -1;

	[SerializeField]
	private PortalMapNode[] m_completionFlippers = new PortalMapNode[0];

	private GameProgress m_gameProgress;

	public int SwitchID
	{
		get
		{
			return m_switchID;
		}
	}

	protected void Awake()
	{
		m_switchID = (int)EditorIDManager.GetUniqueID(base.gameObject);
		GameSession gameSession = GameUtils.GetGameSession();
		m_gameProgress = gameSession.Progress;
	}

	public bool IsSwitchPressed()
	{
		GameProgress.GameProgressData.SwitchState switchState = m_gameProgress.GetSwitchState(SwitchID);
		return switchState.Activated;
	}

	public bool IsSwitchedDueToCompletion()
	{
		GameSession gameSession = GameUtils.GetGameSession();
		for (int i = 0; i < m_completionFlippers.Length; i++)
		{
			PortalMapNode portalMapNode = m_completionFlippers[i];
			if (portalMapNode != null)
			{
				GameProgress.GameProgressData.LevelProgress levelProgress = gameSession.Progress.SaveData.GetLevelProgress(portalMapNode.LevelIndex);
				if (levelProgress != null && levelProgress.LevelId != -1 && levelProgress.Completed)
				{
					return true;
				}
			}
		}
		return false;
	}

	public bool CanProcessSwitch()
	{
		return !IsSwitchPressed();
	}
}
