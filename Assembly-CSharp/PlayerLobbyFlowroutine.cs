using System;
using System.Collections;
using System.Collections.Generic;
using Team17.Online;
using UnityEngine;

public class PlayerLobbyFlowroutine
{
	private Generic<LobbyUIController, GameSession.GameType> m_uiBuilder;

	private SceneDirectoryData m_sceneDirectory;

	private AmbiControlsMappingData m_unsidedAmbiMapping;

	private MapAvatarControls m_avatar;

	private int m_levelIndex;

	private LobbyUIController m_lobby;

	private PauseMenuManager m_pauseManager;

	private bool m_active = true;

	public PlayerLobbyFlowroutine(Generic<LobbyUIController, GameSession.GameType> _uiBuilder, AmbiControlsMappingData _sidedAmbiMapping, AmbiControlsMappingData _unsidedAmbiMapping, PauseMenuManager _pauseManager, MapAvatarControls _controls, int _levelIndex, bool skipLobby = false)
	{
		m_uiBuilder = _uiBuilder;
		m_unsidedAmbiMapping = _unsidedAmbiMapping;
		m_sceneDirectory = GameUtils.GetGameSession().Progress.GetSceneDirectory();
		if (skipLobby || GameUtils.GetDebugConfig().m_skipPlayerJoining)
		{
			SkipPlayerLobby(_levelIndex);
		}
		else
		{
			StartPlayerLobbySession(_pauseManager, _controls, _levelIndex);
		}
	}

	public IEnumerator Run()
	{
		while (m_active)
		{
			yield return null;
		}
	}

	private void StartPlayerLobbySession(PauseMenuManager _pauseManager, MapAvatarControls _controls, int _levelIndex)
	{
		m_pauseManager = _pauseManager;
		m_avatar = _controls;
		m_levelIndex = _levelIndex;
		if (m_pauseManager != null)
		{
			m_pauseManager.enabled = false;
		}
		if (m_avatar != null)
		{
			m_avatar.enabled = false;
		}
		SceneDirectoryData.SceneDirectoryEntry sceneDirectoryEntry = m_sceneDirectory.Scenes[_levelIndex];
		GameSession gameSession = GameUtils.GetGameSession();
		m_lobby = m_uiBuilder(gameSession.TypeSettings.Type);
		GameProgress.GameProgressData.LevelProgress progress = gameSession.Progress.GetProgress(_levelIndex);
		m_lobby.SetSceneData(gameSession.TypeSettings.Type, sceneDirectoryEntry, progress);
		m_lobby.RegisterForCompletedMessage(OnLobbyCompleted);
		m_lobby.RegisterForCanceledMessage(OnPlayerSelectUIClosed);
	}

	private void SkipPlayerLobby(int _levelIndex)
	{
		SceneDirectoryData.SceneDirectoryEntry sceneDirectoryEntry = m_sceneDirectory.Scenes[_levelIndex];
		int num = sceneDirectoryEntry.SceneVarients.FindIndex_Predicate((SceneDirectoryData.PerPlayerCountDirectoryEntry x) => x.PlayerCount == 4);
		SceneDirectoryData.PerPlayerCountDirectoryEntry perPlayerCountDirectoryEntry = sceneDirectoryEntry.SceneVarients[num];
		if (perPlayerCountDirectoryEntry != null)
		{
			GameInputConfig inputConfig = ConstructDebugInputConfig();
			Dictionary<PlayerInputLookup.Player, GameSession.SelectedChefData> dictionary = new Dictionary<PlayerInputLookup.Player, GameSession.SelectedChefData>();
			AvatarDirectoryData avatarDirectory = GameUtils.GetGameSession().Progress.GetAvatarDirectory();
			ChefAvatarData[] avatars = avatarDirectory.Avatars;
			ChefColourData[] colours = avatarDirectory.Colours;
			GameSession.SelectedChefData value = new GameSession.SelectedChefData(avatars.TryAtIndex(0, avatars[0]), colours.TryAtIndex(0, colours[0]));
			GameSession.SelectedChefData value2 = new GameSession.SelectedChefData(avatars.TryAtIndex(1, avatars[0]), colours.TryAtIndex(1, colours[0]));
			GameSession.SelectedChefData value3 = new GameSession.SelectedChefData(avatars.TryAtIndex(2, avatars[0]), colours.TryAtIndex(2, colours[0]));
			GameSession.SelectedChefData value4 = new GameSession.SelectedChefData(avatars.TryAtIndex(3, avatars[0]), colours.TryAtIndex(3, colours[0]));
			dictionary.Add(PlayerInputLookup.Player.One, value);
			dictionary.Add(PlayerInputLookup.Player.Two, value2);
			dictionary.Add(PlayerInputLookup.Player.Three, value3);
			dictionary.Add(PlayerInputLookup.Player.Four, value4);
			LoadKitchen(_levelIndex, num, perPlayerCountDirectoryEntry.SceneName, sceneDirectoryEntry.LoadScreenOverride, inputConfig, dictionary, perPlayerCountDirectoryEntry.LevelConfig);
		}
	}

	private GameInputConfig ConstructDebugInputConfig()
	{
		GameInputConfig.ConfigEntry[] entries = new GameInputConfig.ConfigEntry[0];
		switch (GameUtils.GetDebugConfig().m_skipJoiningConfig)
		{
		case GameDebugConfig.SkipPlayerJoiningConfig.ControllerEach:
			entries = new GameInputConfig.ConfigEntry[4]
			{
				new GameInputConfig.ConfigEntry(PlayerInputLookup.Player.One, ControlPadInput.PadNum.One, PadSide.Both, ClientUserSystem.s_LocalMachineId, m_unsidedAmbiMapping),
				new GameInputConfig.ConfigEntry(PlayerInputLookup.Player.Two, ControlPadInput.PadNum.Two, PadSide.Both, ClientUserSystem.s_LocalMachineId, m_unsidedAmbiMapping),
				new GameInputConfig.ConfigEntry(PlayerInputLookup.Player.Three, ControlPadInput.PadNum.Three, PadSide.Both, ClientUserSystem.s_LocalMachineId, m_unsidedAmbiMapping),
				new GameInputConfig.ConfigEntry(PlayerInputLookup.Player.Four, ControlPadInput.PadNum.Four, PadSide.Both, ClientUserSystem.s_LocalMachineId, m_unsidedAmbiMapping)
			};
			break;
		case GameDebugConfig.SkipPlayerJoiningConfig.TwoSidedPads:
			entries = new GameInputConfig.ConfigEntry[4]
			{
				new GameInputConfig.ConfigEntry(PlayerInputLookup.Player.One, ControlPadInput.PadNum.One, PadSide.Left, ClientUserSystem.s_LocalMachineId, m_unsidedAmbiMapping),
				new GameInputConfig.ConfigEntry(PlayerInputLookup.Player.Two, ControlPadInput.PadNum.Two, PadSide.Left, ClientUserSystem.s_LocalMachineId, m_unsidedAmbiMapping),
				new GameInputConfig.ConfigEntry(PlayerInputLookup.Player.Three, ControlPadInput.PadNum.One, PadSide.Right, ClientUserSystem.s_LocalMachineId, m_unsidedAmbiMapping),
				new GameInputConfig.ConfigEntry(PlayerInputLookup.Player.Four, ControlPadInput.PadNum.Two, PadSide.Right, ClientUserSystem.s_LocalMachineId, m_unsidedAmbiMapping)
			};
			break;
		}
		return new GameInputConfig(entries);
	}

	private void OnPlayerSelectUIClosed()
	{
		UnityEngine.Object.Destroy(m_lobby.gameObject);
		if (m_avatar != null)
		{
			m_avatar.enabled = true;
		}
		if (m_pauseManager != null)
		{
			m_pauseManager.enabled = true;
		}
		m_active = false;
	}

	private void OnLobbyCompleted()
	{
		LobbyUIController lobby = m_lobby;
		Dictionary<PlayerInputLookup.Player, LobbyUIController.AvatarCardData> selectedAvatars = lobby.GetSelectedAvatars();
		int levelIndex = m_levelIndex;
		int playerCount = selectedAvatars.Count;
		if (GameUtils.GetGameSession().TypeSettings.Type == GameSession.GameType.Competitive)
		{
			playerCount = 4;
		}
		SceneDirectoryData.SceneDirectoryEntry sceneDirectoryEntry = m_sceneDirectory.Scenes[levelIndex];
		SceneDirectoryData.PerPlayerCountDirectoryEntry sceneVarient = sceneDirectoryEntry.GetSceneVarient(playerCount);
		if (sceneVarient != null)
		{
			string sceneName = sceneVarient.SceneName;
			if (sceneName != string.Empty)
			{
				GameSession.GameLevelSettings gameLevelSettings = new GameSession.GameLevelSettings();
				gameLevelSettings.SceneDirectoryVarientEntry = sceneVarient;
				LoadKitchen(levelIndex, sceneName, sceneDirectoryEntry.LoadScreenOverride, gameLevelSettings);
				return;
			}
		}
		Vector2 vector = new Vector2(0.5f * (float)Camera.main.pixelWidth, 0.5f * (float)Camera.main.pixelHeight);
	}

	private void LoadKitchen(int _levelIndex, int _varientIndex, string _actualScene, Sprite _loadingScreen, GameInputConfig _inputConfig, Dictionary<PlayerInputLookup.Player, GameSession.SelectedChefData> _chef, LevelConfigBase _levelConfig)
	{
		SceneDirectoryData sceneDirectory = GameUtils.GetGameSession().Progress.GetSceneDirectory();
		GameSession.GameLevelSettings gameLevelSettings = new GameSession.GameLevelSettings();
		gameLevelSettings.SceneDirectoryVarientEntry = sceneDirectory.Scenes[_levelIndex].SceneVarients[_varientIndex];
		LoadKitchen(_levelIndex, _actualScene, _loadingScreen, gameLevelSettings);
	}

	public static void LoadKitchen(int _levelIndex, string _actualScene, Sprite _loadingScreen, GameSession.GameLevelSettings _levelSettings)
	{
		GameSession gameSession = GameUtils.GetGameSession();
		gameSession.LevelSettings = _levelSettings;
		gameSession.Progress.SaveData.LastLevelEntered = _levelIndex;
		GameUtils.GetGameSession().SaveSession();
		LoadingScreenFlow.LoadScene(_actualScene, GameState.RunKitchen);
	}

	private GameInputConfig BuildInputConfig(Dictionary<PlayerInputLookup.Player, LobbyUIController.AvatarCardData> _avatars)
	{
		GameInputConfig.ConfigEntry[] array = new GameInputConfig.ConfigEntry[_avatars.Count];
		PlayerInputLookup.Player[] array2 = new PlayerInputLookup.Player[_avatars.Count];
		_avatars.Keys.CopyTo(array2, 0);
		Array.Sort(array2);
		for (int i = 0; i < array2.Length; i++)
		{
			LobbyUIController.AvatarCardData avatarCardData = _avatars[array2[i]];
			PlayerInputLookup.Player player = (PlayerInputLookup.Player)i;
			array[i] = new GameInputConfig.ConfigEntry(player, avatarCardData.PlayerInput.Pad, avatarCardData.PlayerInput.Side, ClientUserSystem.s_LocalMachineId, avatarCardData.PlayerInput.AmbiControlsMapping);
		}
		return new GameInputConfig(array);
	}
}
