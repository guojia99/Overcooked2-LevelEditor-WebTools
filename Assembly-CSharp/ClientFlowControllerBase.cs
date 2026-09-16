using System.Collections;
using System.Collections.Generic;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;
using UnityEngine.SceneManagement;

public abstract class ClientFlowControllerBase : ClientSynchroniserBase, IFlowController
{
	private FlowControllerBase m_flowControllerBase;

	private IOnlineMultiplayerNotificationCoordinator m_iOnlineMultiplayerNotificationCoordinator;

	private IEnumerator m_levelRoutine;

	private IEnumerator m_outroRoutine;

	protected CampaignAudioManager m_campaignAudioManager;

	private bool m_isFinished;

	private bool m_inRound;

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
		m_iOnlineMultiplayerNotificationCoordinator = GameUtils.RequireManagerInterface<IOnlinePlatformManager>().OnlineMultiplayerNotificationCoordinator();
		GameSession.GameLevelSettings levelSettings = GameUtils.GetGameSession().LevelSettings;
		if (levelSettings != null && levelSettings.SceneDirectoryVarientEntry != null)
		{
			LevelConfig = levelSettings.SceneDirectoryVarientEntry.LevelConfig;
		}
		m_campaignAudioManager = GameUtils.RequireManager<CampaignAudioManager>();
		Mailbox.Client.RegisterForMessageType(MessageType.GameState, OnGameStateChanged);
	}

	protected virtual void Start()
	{
		if (!LoadingScreenFlow.IsLoadingStartScreen())
		{
			GameUtils.LoadScene("InGameMenu", LoadSceneMode.Additive);
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		Mailbox.Client.UnregisterForMessageType(MessageType.GameState, OnGameStateChanged);
	}

	private void OnGameStateChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		GameStateMessage gameStateMessage = (GameStateMessage)message;
		switch (gameStateMessage.m_State)
		{
		case GameState.RunLevelIntro:
		{
			CallbackVoid finishedCallback = delegate
			{
				ClientMessenger.GameState(GameState.RanLevelIntro);
			};
			StartCoroutine(RunLevelIntro(finishedCallback));
			break;
		}
		case GameState.InLevel:
			m_levelRoutine = RunLevel();
			StartCoroutine(m_levelRoutine);
			break;
		case GameState.RunLevelOutro:
			m_isFinished = true;
			m_levelRoutine = null;
			m_outroRoutine = RunLevelEnd();
			break;
		}
	}

	public override void UpdateSynchronising()
	{
		base.UpdateSynchronising();
		if (m_outroRoutine != null && !m_outroRoutine.MoveNext())
		{
			m_outroRoutine = null;
			if (ConnectionStatus.IsInSession() && !ConnectionStatus.IsHost())
			{
				MultiplayerController multiplayerController = GameUtils.RequireManager<MultiplayerController>();
				multiplayerController.StopSynchronisation();
			}
			ClientMessenger.GameState(GameState.RanLevelOutro);
		}
	}

	private IEnumerator RunLevelIntro(CallbackVoid _finishedCallback)
	{
		m_campaignAudioManager.SetAudioState(CampaignAudioManager.AudioState.IntroState);
		IntroFlowroutineBase levelIntroFlowroutine = base.gameObject.RequireComponent<IntroFlowroutineBase>();
		levelIntroFlowroutine.Setup(delegate
		{
		});
		IEnumerator levelIntro = levelIntroFlowroutine.Run();
		while (levelIntro.MoveNext())
		{
			yield return levelIntro.Current;
		}
		_finishedCallback();
	}

	private IEnumerator RunLevel()
	{
		SetRoundBehaviourActivation(true);
		m_campaignAudioManager.SetAudioState(CampaignAudioManager.AudioState.InLevel);
		while (true)
		{
			if (base.enabled)
			{
				OnUpdateInRound();
				UpdateOnlineMultiplayerNotifications();
				if (HasFinished())
				{
					break;
				}
			}
			yield return null;
		}
		SetRoundBehaviourActivation(false);
	}

	private void UpdateOnlineMultiplayerNotifications()
	{
		if (m_iOnlineMultiplayerNotificationCoordinator == null)
		{
			return;
		}
		FastList<User> users = ClientUserSystem.m_Users;
		if (users == null)
		{
			return;
		}
		for (int i = 0; i < users.Count; i++)
		{
			User user = users._items[i];
			if (user != null && user.IsLocal && user.GameState == GameState.InLevel)
			{
				m_iOnlineMultiplayerNotificationCoordinator.InGamePlay();
				break;
			}
		}
	}

	private IEnumerator RunLevelEnd()
	{
		m_campaignAudioManager.SetAudioState(CampaignAudioManager.AudioState.SummaryScreen);
		IEnumerator outro = RunLevelOutro();
		while (outro != null && outro.MoveNext())
		{
			yield return outro.Current;
		}
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

	public LevelConfigBase GetLevelConfig()
	{
		return LevelConfig;
	}

	public GameConfig GetGameConfig()
	{
		return (!(m_flowControllerBase != null)) ? null : m_flowControllerBase.m_gameConfig;
	}

	protected virtual void OnUpdateInRound()
	{
	}

	protected abstract IEnumerator RunLevelOutro();

	private bool HasFinished()
	{
		return m_isFinished;
	}
}
