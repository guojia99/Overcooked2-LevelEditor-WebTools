using System.Collections;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ClientStoryLevelFlowController : ClientSynchroniserBase, IFlowController
{
	private StoryLevelFlowController m_flowController;

	private StoryLevelConfig m_levelConfig;

	protected CampaignAudioManager m_campaignAudioManager;

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
		m_flowController.m_onionKing.DialogueScript = m_levelConfig.OnionScript;
		m_flowController.m_onionKing.AutoStart = m_levelConfig.AutoStartDialogue;
		if (m_levelConfig.OnionReturnScript != null && m_levelConfig.OnionReturnScript.Length > 0)
		{
			m_flowController.m_onionKing.RegisterDialogueFinishedCallback(OnOnionKingDialogueFinished);
		}
	}

	private void Awake()
	{
		GameSession.GameLevelSettings levelSettings = GameUtils.GetGameSession().LevelSettings;
		if (levelSettings != null && levelSettings.SceneDirectoryVarientEntry != null)
		{
			m_levelConfig = levelSettings.SceneDirectoryVarientEntry.LevelConfig as StoryLevelConfig;
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
		if (m_flowController != null && m_flowController.m_onionKing != null)
		{
			m_flowController.m_onionKing.UnregisterDialogueFinishedCallback(OnOnionKingDialogueFinished);
		}
	}

	private void OnGameStateChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		GameStateMessage gameStateMessage = (GameStateMessage)message;
		switch (gameStateMessage.m_State)
		{
		case GameState.RunLevelIntro:
			StartCoroutine(WaitForLoading(delegate
			{
				ClientMessenger.GameState(GameState.RanLevelIntro);
			}));
			break;
		case GameState.InLevel:
			m_levelRoutine = RunLevel();
			StartCoroutine(m_levelRoutine);
			break;
		case GameState.RunLevelOutro:
		{
			m_isFinished = true;
			MultiplayerController multiplayerController = GameUtils.RequireManager<MultiplayerController>();
			multiplayerController.StopSynchronisation();
			ClientMessenger.GameState(GameState.RanLevelOutro);
			break;
		}
		}
	}

	private IEnumerator WaitForLoading(CallbackVoid _callback)
	{
		while (LoadingScreenFlow.IsLoading)
		{
			yield return null;
		}
		if (m_levelConfig.AutoStartDialogue)
		{
			yield return new WaitForSeconds(2f);
		}
		_callback();
	}

	private IEnumerator RunLevel()
	{
		int levelID = GameUtils.GetLevelID();
		if (levelID >= 0)
		{
			GameSession gameSession = GameUtils.GetGameSession();
			GameProgress.UnlockData[] _unlocks = new GameProgress.UnlockData[0];
			gameSession.Progress.RecordLevelProgress(levelID, 0, ref _unlocks);
			gameSession.SaveSession();
		}
		this.RoundActivatedCallback();
		m_campaignAudioManager.SetAudioState(CampaignAudioManager.AudioState.InLevel);
		while (!base.enabled || !HasFinished())
		{
			yield return null;
		}
		m_campaignAudioManager.SetMusic(null);
		this.RoundDeactivatedCallback();
	}

	public bool HasFinished()
	{
		return m_isFinished;
	}

	public void OnOnionKingDialogueFinished(DialogueController.Dialogue _dialogue)
	{
		if (m_levelConfig.OnionReturnScript != null && m_levelConfig.OnionReturnScript.Length > 0)
		{
			m_flowController.m_onionKing.DialogueScript = m_levelConfig.OnionReturnScript;
		}
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
