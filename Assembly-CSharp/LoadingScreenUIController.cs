using GameModes;
using GameModes.Horde;
using UnityEngine;

public class LoadingScreenUIController : MonoBehaviour
{
	[SerializeField]
	private ProgressBarUI m_progressBar;

	[Header("Generic Scene Load")]
	[SerializeField]
	private GameObject m_genericLoadingScreen;

	[Header("Kitchen Scenes")]
	[SerializeField]
	private GameObject m_levelLoadingScreen;

	[SerializeField]
	private T17Text m_levelName;

	[SerializeField]
	private T17Text m_highScore;

	[SerializeField]
	private T17Image m_previewImage;

	[Header("Campaign Mode")]
	[SerializeField]
	private GameObject m_campaignModeUI;

	[SerializeField]
	[AssignChildRecursive("StarContainer", Editorbility.Editable)]
	private Transform m_starContainer;

	[SerializeField]
	[AssignResource("NGP_LoadingStars", Editorbility.Editable)]
	private GameObject m_starsPrefab;

	[Header("Practice Mode")]
	[SerializeField]
	private GameObject m_practiceModeUI;

	[Header("Survival Mode")]
	[SerializeField]
	private GameObject m_survivalModeUI;

	[SerializeField]
	private DisplayTimeUIController m_survivalModeTimeDisplay;

	private LoadingScreenFlow m_loadingScreenFlow;

	private ScoreBoundaryStar[] m_stars;

	private GameSession m_gameSession;

	private void Awake()
	{
		Setup();
	}

	private void Setup()
	{
		bool flag = false;
		m_loadingScreenFlow = base.gameObject.RequireComponent<LoadingScreenFlow>();
		m_gameSession = GameUtils.GetGameSession();
		if (m_gameSession != null)
		{
			GameProgress progress = m_gameSession.Progress;
			SceneDirectoryData sceneDirectory = progress.GetSceneDirectory();
			if (sceneDirectory != null)
			{
				SceneDirectoryData.SceneDirectoryEntry[] scenes = sceneDirectory.Scenes;
				GameSession.GameTypeSettings typeSettings = m_gameSession.TypeSettings;
				GameProgress.GameProgressData saveableData = progress.SaveableData;
				SceneDirectoryData.PerPlayerCountDirectoryEntry sceneDirectoryVarientEntry = m_gameSession.LevelSettings.SceneDirectoryVarientEntry;
				if (scenes != null && sceneDirectoryVarientEntry != null && !m_loadingScreenFlow.NextScene.Equals(typeSettings.WorldMapScene) && !m_loadingScreenFlow.NextScene.Equals("StartScreen"))
				{
					int levelID = GameUtils.GetLevelID();
					if (levelID >= 0 && levelID < scenes.Length && scenes[levelID].UseKitchenLoadingScreen)
					{
						bool isNewGamePlus = progress.SaveData.IsNGPEnabledForLevel(levelID) && progress.SaveData.NewGamePlusDialogShown;
						SetupKitchenLoad(scenes[levelID].Label, sceneDirectoryVarientEntry, saveableData.GetLevelProgress(levelID), isNewGamePlus);
						flag = true;
					}
				}
			}
		}
		if (!flag)
		{
			SetupGenericLoad();
		}
	}

	private void SetupGenericLoad()
	{
		m_genericLoadingScreen.SetActive(true);
		m_levelLoadingScreen.SetActive(false);
	}

	private void SetupKitchenLoad(string levelName, SceneDirectoryData.PerPlayerCountDirectoryEntry entry, GameProgress.GameProgressData.LevelProgress levelProgress, bool isNewGamePlus)
	{
		m_genericLoadingScreen.SetActive(false);
		m_levelLoadingScreen.SetActive(true);
		if (m_levelName != null)
		{
			m_levelName.SetLocalisedTextCatchAll(levelName);
		}
		if (m_previewImage != null)
		{
			m_previewImage.sprite = entry.Screenshot;
		}
		if (ClientGameSetup.Mode == GameMode.Campaign || ClientGameSetup.Mode == GameMode.Party)
		{
			switch (m_gameSession.GameModeKind)
			{
			case Kind.Campaign:
			{
				m_campaignModeUI.gameObject.SetActive(true);
				m_practiceModeUI.gameObject.SetActive(false);
				m_survivalModeUI.gameObject.SetActive(false);
				if (entry.LevelConfig != null && entry.LevelConfig as HordeLevelConfig != null)
				{
					if (m_highScore != null)
					{
						int highScore = levelProgress.HighScore;
						if (highScore == int.MinValue)
						{
							m_highScore.gameObject.SetActive(false);
							break;
						}
						m_highScore.gameObject.SetActive(true);
						int requiredBitCount = GameUtils.GetRequiredBitCount(65535);
						float num = FloatUtils.FromUnorm(highScore, requiredBitCount);
						m_highScore.SetNonLocalizedText(Localization.Get("LoadingScreen.BestScore", new LocToken("[Score]", string.Format("{0:P0}", num))));
					}
					break;
				}
				if (m_highScore != null)
				{
					int num2 = levelProgress.HighScore;
					if (num2 == int.MinValue)
					{
						num2 = 0;
					}
					m_highScore.SetNonLocalizedText(Localization.Get("LoadingScreen.BestScore", new LocToken("[Score]", num2.ToString())));
				}
				if (m_stars == null && m_starsPrefab != null && m_starContainer != null)
				{
					GameObject gameObject = m_starsPrefab.InstantiateOnParent(m_starContainer);
					gameObject.transform.localScale = Vector3.one;
					m_stars = gameObject.gameObject.RequestComponentsRecursive<ScoreBoundaryStar>();
				}
				if (m_stars == null)
				{
					break;
				}
				for (int i = 0; i < m_stars.Length; i++)
				{
					if (m_stars[i] != null)
					{
						m_stars[i].Score = entry.GetPointsForStar(m_stars[i].m_star);
						if (m_stars[i].m_star > 3)
						{
							m_stars[i].gameObject.SetActive(isNewGamePlus);
						}
					}
				}
				int scoreStars = levelProgress.ScoreStars;
				for (int j = 0; j < m_stars.Length; j++)
				{
					if (m_stars[j] != null)
					{
						m_stars[j].SetUnlocked(m_stars[j].m_star <= scoreStars);
					}
				}
				break;
			}
			case Kind.Practice:
				m_campaignModeUI.gameObject.SetActive(false);
				m_practiceModeUI.gameObject.SetActive(true);
				m_survivalModeUI.gameObject.SetActive(false);
				break;
			case Kind.Survival:
				m_campaignModeUI.gameObject.SetActive(false);
				m_practiceModeUI.gameObject.SetActive(false);
				m_survivalModeUI.gameObject.SetActive(true);
				m_survivalModeTimeDisplay.Value = levelProgress.SurvivalModeTime;
				break;
			}
		}
		else
		{
			m_campaignModeUI.gameObject.SetActive(false);
			m_practiceModeUI.gameObject.SetActive(false);
			m_survivalModeUI.gameObject.SetActive(false);
		}
	}

	private void Update()
	{
		if (m_progressBar != null && m_loadingScreenFlow != null)
		{
			m_progressBar.SetValue(m_loadingScreenFlow.Progress);
		}
	}
}
