using GameModes;
using UnityEngine;

[ExecutionDependency(typeof(BootstrapManager))]
public class LevelPortalMapNode : PortalMapNode
{
	[SerializeField]
	[AssignResource("GameModeUIData", Editorbility.Editable)]
	public GameModeUIData m_gameModeUIData;

	[HideInInspector]
	public GameProgress.GameProgressData.LevelProgress m_sceneProgress;

	protected override void Awake()
	{
		base.Awake();
		m_sceneProgress = m_gameProgress.GetProgress(LevelIndex);
	}

	public GameProgress.GameProgressData.LevelProgress GetLevelProgress()
	{
		if (m_sceneProgress != null)
		{
			return m_sceneProgress;
		}
		return null;
	}

	public override WorldMapLevelIconUI GetUIPrefab(Kind _kind)
	{
		return m_gameModeUIData.m_gameModes[(int)_kind].m_levelPreview;
	}
}
