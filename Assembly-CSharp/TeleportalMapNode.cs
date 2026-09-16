using UnityEngine;

public class TeleportalMapNode : MapNode
{
	[SerializeField]
	private SceneDirectoryData.World m_world = SceneDirectoryData.World.Invalid;

	private GameProgress m_gameProgress;

	public SceneDirectoryData.World World
	{
		get
		{
			return m_world;
		}
	}

	protected void Awake()
	{
		GameSession gameSession = GameUtils.GetGameSession();
		m_gameProgress = gameSession.Progress;
	}

	public bool ShouldFocus()
	{
		if (!base.enabled)
		{
			return false;
		}
		GameProgress.GameProgressData.TeleportalState teleportalState = m_gameProgress.GetTeleportalState(m_world);
		return teleportalState.World == SceneDirectoryData.World.COUNT;
	}
}
