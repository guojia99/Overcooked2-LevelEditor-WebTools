using System.Collections;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerStoryLevelFlowController : ServerSynchroniserBase, IServerFlowController, IFlowController
{
	private StoryLevelFlowController m_flowController;

	private StoryLevelConfig m_levelConfig;

	private GameState m_state;

	private IEnumerator m_levelRoutine;

	private bool m_isFinished;

	public bool InRound
	{
		get
		{
			return false;
		}
	}

	public event CallbackVoid RoundActivatedCallback = delegate
	{
	};

	public event CallbackVoid RoundDeactivatedCallback = delegate
	{
	};

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_flowController = (StoryLevelFlowController)synchronisedObject;
	}

	private void Awake()
	{
		GameSession.GameLevelSettings levelSettings = GameUtils.GetGameSession().LevelSettings;
		if (levelSettings != null && levelSettings.SceneDirectoryVarientEntry != null)
		{
			m_levelConfig = levelSettings.SceneDirectoryVarientEntry.LevelConfig as StoryLevelConfig;
		}
	}

	public void StartFlow()
	{
		ChangeGameState(GameState.RunLevelIntro);
	}

	private void Update()
	{
		switch (m_state)
		{
		case GameState.RunLevelIntro:
			if (AreAllUsersInGameState(GameState.RanLevelIntro))
			{
				m_levelRoutine = RunLevel();
				ChangeGameState(GameState.InLevel);
			}
			break;
		case GameState.InLevel:
			if (m_levelRoutine == null || !m_levelRoutine.MoveNext())
			{
				ChangeGameState(GameState.RunLevelOutro);
			}
			break;
		case GameState.RunLevelOutro:
			if (AreAllUsersInGameState(GameState.RanLevelOutro))
			{
				m_state = GameState.RanLevelOutro;
				MultiplayerController multiplayerController = GameUtils.RequireManager<MultiplayerController>();
				multiplayerController.StopSynchronisation();
				GameSession gameSession = GameUtils.GetGameSession();
				gameSession.FillShownMetaDialogStatus();
				ServerMessenger.GameProgressData(gameSession.Progress.SaveData, gameSession.m_shownMetaDialogs);
				ServerMessenger.LoadLevel(gameSession.TypeSettings.WorldMapScene, GameState.CampaignMap, true, GameState.RunMapUnfoldRoutine);
			}
			break;
		}
	}

	private IEnumerator RunLevel()
	{
		this.RoundActivatedCallback();
		while (!base.enabled || !HasFinished())
		{
			yield return null;
		}
		this.RoundDeactivatedCallback();
	}

	private bool HasFinished()
	{
		return m_isFinished;
	}

	private void ChangeGameState(GameState state)
	{
		UserSystemUtils.ChangeGameState(state);
		m_state = state;
	}

	private bool AreAllUsersInGameState(GameState state)
	{
		return UserSystemUtils.AreAllUsersInGameState(ServerUserSystem.m_Users, state);
	}

	public void SkipToEnd()
	{
		m_isFinished = true;
	}

	public LevelConfigBase GetLevelConfig()
	{
		return m_levelConfig;
	}

	public GameConfig GetGameConfig()
	{
		return null;
	}
}
