using UnityEngine;

public class FlagBaseHandler : MonoBehaviour
{
	private LevelPortalMapNode m_levelMapNode;

	private WorldMapFlowController m_flowController;

	[SerializeField]
	private GameObject m_locked;

	[SerializeField]
	private GameObject m_unlocked;

	private void Start()
	{
		m_levelMapNode = base.gameObject.RequireComponent<LevelPortalMapNode>();
		m_flowController = GameUtils.RequireManager<WorldMapFlowController>();
		SceneDirectoryData sceneDirectory = m_flowController.GetSceneDirectory();
		GameProgress.GameProgressData.LevelProgress levelProgress = m_levelMapNode.GetLevelProgress();
		SceneDirectoryData.SceneDirectoryEntry sceneDirectoryEntry = sceneDirectory.Scenes.TryAtIndex(m_levelMapNode.LevelIndex);
		if (sceneDirectoryEntry.IsHidden && levelProgress.Completed)
		{
			m_locked.SetActive(false);
			m_unlocked.SetActive(false);
		}
		else
		{
			m_locked.SetActive(true);
			m_unlocked.SetActive(true);
		}
	}
}
