using GameModes;
using UnityEngine;

public abstract class PortalMapNode : MapNode
{
	[SerializeField]
	[LevelIndex]
	public int m_levelIndex;

	[SerializeField]
	public bool m_forceUnlocked;

	[SerializeField]
	public bool m_uiAlwaysActive;

	[HideInInspector]
	public GameProgress m_gameProgress;

	[HideInInspector]
	public SceneDirectoryData.SceneDirectoryEntry m_sceneDirectoryEntry;

	[HideInInspector]
	public WorldMapFlowController m_worldMapFlowController;

	protected GameSession.GameType m_gameType;

	public bool ForceUnlocked
	{
		get
		{
			return m_forceUnlocked;
		}
	}

	public virtual int LevelIndex
	{
		get
		{
			return m_levelIndex;
		}
	}

	protected virtual void Awake()
	{
		m_worldMapFlowController = GameUtils.RequireManager<WorldMapFlowController>();
		m_sceneDirectoryEntry = m_worldMapFlowController.GetSceneDirectory().Scenes.TryAtIndex(LevelIndex);
		GameSession gameSession = GameUtils.GetGameSession();
		m_gameProgress = gameSession.Progress;
		m_gameType = gameSession.TypeSettings.Type;
	}

	public WorldMapKitchenLevelIconUI.State GetState()
	{
		GameSession gameSession = GameUtils.GetGameSession();
		if (MaskUtils.HasFlag(m_sceneDirectoryEntry.m_supportedGameModes, gameSession.GameModeKind))
		{
			GameProgress.GameProgressData.LevelProgress progress = m_gameProgress.GetProgress(LevelIndex);
			if (progress.Purchased || DebugManager.Instance.GetOption("Unlock all levels") || ForceUnlocked)
			{
				return WorldMapKitchenLevelIconUI.State.Purchased;
			}
			if (m_gameProgress.GetStarTotal() >= m_sceneDirectoryEntry.StarCost)
			{
				return WorldMapKitchenLevelIconUI.State.Affordable;
			}
			return WorldMapKitchenLevelIconUI.State.UnAffordable;
		}
		return WorldMapKitchenLevelIconUI.State.UnSupported;
	}

	public abstract WorldMapLevelIconUI GetUIPrefab(Kind _kind);
}
