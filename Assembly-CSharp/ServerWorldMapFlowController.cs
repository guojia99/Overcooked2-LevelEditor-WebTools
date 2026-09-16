#define ANALYTICS
using System.Collections;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerWorldMapFlowController : ServerSynchroniserBase
{
	private WorldMapFlowController m_baseObject;

	private GameState m_State;

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_baseObject = (WorldMapFlowController)synchronisedObject;
		m_State = GameState.RunMapUnfoldRoutine;
		RegisterPopups();
	}

	public override void UpdateSynchronising()
	{
		GameState state = m_State;
		if (state == GameState.RunMapUnfoldRoutine && UserSystemUtils.AreAllUsersInGameState(ServerUserSystem.m_Users, GameState.RanMapUnfoldRoutine))
		{
			SpawnPopups();
			ChangeGameState(GameState.InMap);
		}
	}

	public void ChangeGameState(GameState state)
	{
		UserSystemUtils.ChangeGameState(state);
		m_State = state;
	}

	private void RegisterPopups()
	{
		if (m_baseObject.m_newGamePlusDialogPrefab != null)
		{
			NetworkUtils.RegisterSpawnablePrefab(base.gameObject, m_baseObject.m_newGamePlusDialogPrefab.gameObject);
		}
		if (m_baseObject.m_practiceModeDialogPrefab != null)
		{
			NetworkUtils.RegisterSpawnablePrefab(base.gameObject, m_baseObject.m_practiceModeDialogPrefab.gameObject);
		}
		if (m_baseObject.m_hordeModeDialogPrefab != null)
		{
			NetworkUtils.RegisterSpawnablePrefab(base.gameObject, m_baseObject.m_hordeModeDialogPrefab.gameObject);
		}
	}

	private void SpawnPopups()
	{
		if (m_baseObject.m_newGamePlusDialogPrefab != null)
		{
			NetworkUtils.ServerSpawnPrefab(base.gameObject, m_baseObject.m_newGamePlusDialogPrefab.gameObject);
		}
		if (m_baseObject.m_practiceModeDialogPrefab != null)
		{
			NetworkUtils.ServerSpawnPrefab(base.gameObject, m_baseObject.m_practiceModeDialogPrefab.gameObject);
		}
		if (m_baseObject.m_hordeModeDialogPrefab != null)
		{
			NetworkUtils.ServerSpawnPrefab(base.gameObject, m_baseObject.m_hordeModeDialogPrefab.gameObject);
		}
	}

	public void OnSelectLevelPortal(MapAvatarControls _controls, LevelPortalMapNode _levelNode)
	{
		if (InviteMonitor.CheckStatus(InviteMonitor.StatusFlags.HandlerIsValid | InviteMonitor.StatusFlags.HandlerIsIdle))
		{
			int layer = LayerMask.NameToLayer("Administration");
			GameUtils.TriggerAudio(GameOneShotAudioTag.UISelect, layer);
			int levelIndex = _levelNode.LevelIndex;
			int playerCount = ClientUserSystem.m_Users.Count;
			if (GameUtils.GetGameSession().TypeSettings.Type == GameSession.GameType.Competitive)
			{
				playerCount = 4;
			}
			SceneDirectoryData.SceneDirectoryEntry sceneDirectoryEntry = m_baseObject.GetSceneDirectory().Scenes[levelIndex];
			LoadLevel(levelIndex, sceneDirectoryEntry, sceneDirectoryEntry.GetSceneVarient(playerCount), _controls);
		}
	}

	public void OnSelectMiniLevelPortal(MapAvatarControls _controls, MiniLevelPortalMapNode _levelNode, int _varient)
	{
		if (InviteMonitor.CheckStatus(InviteMonitor.StatusFlags.HandlerIsValid | InviteMonitor.StatusFlags.HandlerIsIdle))
		{
			int layer = LayerMask.NameToLayer("Administration");
			GameUtils.TriggerAudio(GameOneShotAudioTag.UISelect, layer);
			int levelIndex = _levelNode.LevelIndex;
			SceneDirectoryData.SceneDirectoryEntry sceneDirectoryEntry = m_baseObject.GetSceneDirectory().Scenes[levelIndex];
			LoadLevel(levelIndex, sceneDirectoryEntry, sceneDirectoryEntry.GetSceneVarient(_varient), _controls);
		}
	}

	private void LoadLevel(int _levelIndex, SceneDirectoryData.SceneDirectoryEntry _entry, SceneDirectoryData.PerPlayerCountDirectoryEntry _varient, MapAvatarControls _controls)
	{
		_controls.gameObject.SetActive(false);
		if (_varient != null)
		{
			string sceneName = _varient.SceneName;
			if (sceneName != string.Empty)
			{
				GameSession.GameLevelSettings levelSettings = GameUtils.GetGameSession().LevelSettings;
				levelSettings.SceneDirectoryVarientEntry = _varient;
				GameSession gameSession = GameUtils.GetGameSession();
				gameSession.LevelSettings = levelSettings;
				gameSession.Progress.SetLastLevelEntered(_levelIndex);
				GameUtils.GetMetaGameProgress().SetLastPlayedTheme(_entry.Theme);
				GameProgress.GameProgressData.LevelProgress levelProgress = gameSession.Progress.SaveData.GetLevelProgress(_levelIndex);
				if (levelProgress != null && levelProgress.ScoreStars >= 3)
				{
					Analytics.LogEvent("Replayed", 0L, Analytics.Flags.LevelName | Analytics.Flags.PlayerCount);
				}
				StartCoroutine(SaveBeforeLoading(_levelIndex, levelSettings.SceneDirectoryVarientEntry.PlayerCount, _controls));
				return;
			}
		}
		Vector2 vector = new Vector2(0.5f * (float)Camera.main.pixelWidth, 0.5f * (float)Camera.main.pixelHeight);
	}

	private IEnumerator SaveBeforeLoading(int _levelIndex, int _playerCount, MapAvatarControls _controls)
	{
		TimeManager timeManager = GameUtils.RequestManager<TimeManager>();
		TimeManager.PauseLayer layerToPause = (ConnectionStatus.IsInSession() ? TimeManager.PauseLayer.Network : TimeManager.PauseLayer.Main);
		if (timeManager != null)
		{
			timeManager.SetPaused(layerToPause, true, this);
		}
		SaveLoadResult? result = null;
		ScreenTransitionManager transitionManager = GameUtils.RequireManager<ScreenTransitionManager>();
		transitionManager.StartTransitionUp(delegate
		{
			GameUtils.GetGameSession().SaveSession(delegate(SaveSystemStatus _status)
			{
				if (_status.Status == SaveSystemStatus.SaveStatus.Complete)
				{
					if (_status.Result == SaveLoadResult.Exists)
					{
						ServerMessenger.LoadLevel((uint)_levelIndex, (uint)_playerCount, GameState.LoadKitchen, GameState.RunKitchen);
					}
					result = _status.Result;
				}
			});
		});
		while (!result.HasValue)
		{
			if (T17DialogBoxManager.HasAnyOpenDialogs())
			{
				transitionManager.StartTransitionDown();
				break;
			}
			yield return null;
		}
		_controls.gameObject.SetActive(true);
		if (timeManager == null)
		{
			timeManager = GameUtils.RequestManager<TimeManager>();
		}
		if (timeManager != null)
		{
			if (!TimeManager.IsPaused(layerToPause))
			{
				timeManager.SetPaused(layerToPause, true, this);
			}
			timeManager.SetPaused(layerToPause, false, this);
		}
	}
}
