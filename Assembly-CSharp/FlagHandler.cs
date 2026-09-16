using GameModes.Horde;
using Team17.Online;
using UnityEngine;

public class FlagHandler : MonoBehaviour
{
	[SerializeField]
	private LevelPortalMapNode m_levelMapNode;

	[SerializeField]
	private MeshRenderer m_renderer;

	[SerializeField]
	private MeshFilter m_mesh;

	[Space]
	[SerializeField]
	private Mesh m_unCompleteMesh;

	[SerializeField]
	private Mesh[] m_completeMeshs;

	[Space]
	[SerializeField]
	private int m_lightMaterialIndex = 1;

	[SerializeField]
	private Material m_lightOff;

	[SerializeField]
	private Material m_lightOn;

	private void Start()
	{
		if (!(m_levelMapNode != null))
		{
			return;
		}
		GameProgress.GameProgressData.LevelProgress levelProgress = m_levelMapNode.GetLevelProgress();
		if (levelProgress == null)
		{
			return;
		}
		int num = ClientUserSystem.m_Users.Count;
		if (num == 0)
		{
			num = 1;
		}
		int levelIndex = m_levelMapNode.LevelIndex;
		SceneDirectoryData sceneDirectory = GameUtils.GetGameSession().Progress.GetSceneDirectory();
		SceneDirectoryData.SceneDirectoryEntry sceneDirectoryEntry = sceneDirectory.Scenes[levelIndex];
		SceneDirectoryData.PerPlayerCountDirectoryEntry sceneVarient = sceneDirectoryEntry.GetSceneVarient(num);
		LevelConfigBase levelConfigBase = ((sceneVarient == null) ? null : sceneVarient.LevelConfig);
		if (m_renderer != null)
		{
			Material[] sharedMaterials = m_renderer.sharedMaterials;
			if (sharedMaterials.Length > m_lightMaterialIndex)
			{
				if ((sceneVarient != null && null != levelConfigBase && levelConfigBase.m_objectives != null && levelConfigBase.m_objectives.Length == 0) || levelProgress.ObjectivesCompleted)
				{
					sharedMaterials[m_lightMaterialIndex] = m_lightOff;
				}
				else
				{
					sharedMaterials[m_lightMaterialIndex] = m_lightOn;
				}
			}
			m_renderer.sharedMaterials = sharedMaterials;
		}
		if (!(m_mesh != null))
		{
			return;
		}
		if (levelProgress.Completed)
		{
			if (levelConfigBase != null && levelConfigBase as HordeLevelConfig != null)
			{
				int num2 = 0;
				int highScore = levelProgress.HighScore;
				if (highScore != int.MinValue)
				{
					int requiredBitCount = GameUtils.GetRequiredBitCount(65535);
					float num3 = FloatUtils.FromUnorm(highScore, requiredBitCount);
					if (num3 > 0f && num3 < 1f)
					{
						num2 = 1 + (int)Mathf.Round(MathUtils.ClampedRemap(num3, 0f, 1f, -0.49f, (float)(m_completeMeshs.Length - 2) - 0.51f));
					}
					else if (num3 >= 1f)
					{
						num2 = m_completeMeshs.Length - 1;
					}
				}
				m_mesh.mesh = m_completeMeshs[num2];
			}
			else
			{
				GameSession gameSession = GameUtils.GetGameSession();
				int b = ((!gameSession.Progress.SaveData.IsNGPEnabledForLevel(m_levelMapNode.LevelIndex)) ? 3 : 4);
				m_mesh.mesh = m_completeMeshs[Mathf.Min(levelProgress.ScoreStars, b)];
			}
		}
		else
		{
			m_mesh.mesh = m_unCompleteMesh;
		}
	}
}
