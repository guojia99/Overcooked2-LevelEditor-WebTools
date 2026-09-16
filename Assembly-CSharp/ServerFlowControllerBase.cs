using System.Collections;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public abstract class ServerFlowControllerBase : ServerSynchroniserBase, IServerFlowController, IFlowController
{
	private FlowControllerBase m_flowControllerBase;

	private GameState m_State;

	private IEnumerator m_levelRoutine;

	private bool m_inRound;

	private bool m_skipToEnd;

	protected LevelConfigBase LevelConfig { get; set; }

	public bool InRound
	{
		get
		{
			return m_inRound;
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
		m_flowControllerBase = (FlowControllerBase)synchronisedObject;
	}

	protected virtual void Awake()
	{
		GameSession.GameLevelSettings levelSettings = GameUtils.GetGameSession().LevelSettings;
		if (levelSettings != null && levelSettings.SceneDirectoryVarientEntry != null)
		{
			LevelConfig = levelSettings.SceneDirectoryVarientEntry.LevelConfig;
		}
	}

	protected virtual void Start()
	{
	}

	public void StartFlow()
	{
		ChangeGameState(GameState.RunLevelIntro);
	}

	public override void UpdateSynchronising()
	{
		switch (m_State)
		{
		case GameState.RunLevelIntro:
			if (AreAllUsersInGameState(GameState.RanLevelIntro))
			{
				m_levelRoutine = RunRound();
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
				m_State = GameState.RanLevelOutro;
				MultiplayerController multiplayerController = GameUtils.RequireManager<MultiplayerController>();
				multiplayerController.StopSynchronisation();
				GameState o_loadState = GameState.NotSet;
				GameState o_loadEndState = GameState.NotSet;
				bool o_useLoadingScreen = true;
				string nextScene = GetNextScene(out o_loadState, out o_loadEndState, out o_useLoadingScreen);
				if (!string.IsNullOrEmpty(nextScene))
				{
					GameSession gameSession = GameUtils.GetGameSession();
					gameSession.FillShownMetaDialogStatus();
					ServerMessenger.GameProgressData(gameSession.Progress.SaveData, gameSession.m_shownMetaDialogs);
					ServerMessenger.LoadLevel(nextScene, o_loadState, o_useLoadingScreen, o_loadEndState);
				}
			}
			break;
		}
	}

	private void ChangeGameState(GameState state)
	{
		UserSystemUtils.ChangeGameState(state);
		m_State = state;
	}

	private bool AreAllUsersInGameState(GameState state)
	{
		return UserSystemUtils.AreAllUsersInGameState(ServerUserSystem.m_Users, state);
	}

	private IEnumerator RunRound()
	{
		SetRoundBehaviourActivation(true);
		while (true)
		{
			if (base.enabled)
			{
				OnUpdateInRound();
				if (HasFinished())
				{
					break;
				}
			}
			yield return null;
		}
		SetRoundBehaviourActivation(false);
	}

	protected void SetRoundBehaviourActivation(bool _enabled)
	{
		if (m_inRound != _enabled)
		{
			if (_enabled)
			{
				this.RoundActivatedCallback();
			}
			else
			{
				this.RoundDeactivatedCallback();
			}
			m_inRound = _enabled;
		}
	}

	public virtual void SkipToEnd()
	{
		m_skipToEnd = true;
	}

	public LevelConfigBase GetLevelConfig()
	{
		return LevelConfig;
	}

	public GameConfig GetGameConfig()
	{
		return (!(m_flowControllerBase != null)) ? null : m_flowControllerBase.m_gameConfig;
	}

	protected virtual bool HasFinished()
	{
		return m_skipToEnd;
	}

	protected virtual void OnUpdateInRound()
	{
	}

	protected abstract string GetNextScene(out GameState o_loadState, out GameState o_loadEndState, out bool o_useLoadingScreen);
}
