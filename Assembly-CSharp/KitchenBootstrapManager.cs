using UnityEngine;
using UnityEngine.SceneManagement;

public class KitchenBootstrapManager : BootstrapManager
{
	[SerializeField]
	private GameInputConfigData m_levelInputConfig;

	[SerializeField]
	private GameSession.SelectedChefData m_playerOneChef;

	[SerializeField]
	private GameSession.SelectedChefData m_playerTwoChef;

	[SerializeField]
	private GameSession.SelectedChefData m_playerThreeChef;

	[SerializeField]
	private GameSession.SelectedChefData m_playerFourChef;

	[SerializeField]
	private bool m_hasNoSceneDirectory;

	[SerializeField]
	[HideInInspectorTest("m_hasNoSceneDirectory", true)]
	private LevelConfigBase m_bootstrapConfig;

	public override void EnsureSetup()
	{
		GameSession gameSession = GameUtils.GetGameSession();
		if (PlayerInputLookup.GetBaseInputConfig() == null)
		{
			PlayerInputLookup.SetBaseInputConfig(m_levelInputConfig.Config);
		}
		base.EnsureSetup();
		if (!(gameSession == null))
		{
			return;
		}
		GameSession gameSession2 = GameUtils.GetGameSession();
		gameSession2.LevelSettings = new GameSession.GameLevelSettings();
		AvatarDirectoryData avatarDirectory = gameSession2.Progress.GetAvatarDirectory();
		if (!m_hasNoSceneDirectory)
		{
			SceneDirectoryData sceneDirectory = gameSession2.Progress.GetSceneDirectory();
			string text = SceneManager.GetActiveScene().name;
			for (int i = 0; i < sceneDirectory.Scenes.Length; i++)
			{
				SceneDirectoryData.PerPlayerCountDirectoryEntry[] sceneVarients = sceneDirectory.Scenes[i].SceneVarients;
				for (int j = 0; j < sceneVarients.Length; j++)
				{
					if (text == sceneVarients[j].SceneName)
					{
						gameSession2.LevelSettings.SceneDirectoryVarientEntry = sceneVarients[j];
						return;
					}
				}
			}
		}
		else
		{
			gameSession2.LevelSettings.SceneDirectoryVarientEntry = new SceneDirectoryData.PerPlayerCountDirectoryEntry();
			gameSession2.LevelSettings.SceneDirectoryVarientEntry.LevelConfig = m_bootstrapConfig;
			gameSession2.LevelSettings.SceneDirectoryVarientEntry.PlayerCount = 4;
			gameSession2.LevelSettings.SceneDirectoryVarientEntry.SceneName = SceneManager.GetActiveScene().name;
			gameSession2.LevelSettings.SceneDirectoryVarientEntry.Screenshot = null;
		}
	}

	public GameSession.SelectedChefData GetDefaultChef(int index)
	{
		GameSession.SelectedChefData result = null;
		switch (index)
		{
		case 0:
			result = m_playerOneChef;
			break;
		case 1:
			result = m_playerTwoChef;
			break;
		case 2:
			result = m_playerThreeChef;
			break;
		case 3:
			result = m_playerFourChef;
			break;
		}
		return result;
	}
}
