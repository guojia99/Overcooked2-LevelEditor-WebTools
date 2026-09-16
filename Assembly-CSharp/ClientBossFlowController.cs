using GameModes;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;

public class ClientBossFlowController : ClientDynamicFlowController
{
	private bool m_bHasSucceeded;

	private Kind m_gameModeKind;

	private GameSession m_gameSession;

	protected override void Awake()
	{
		base.Awake();
		Mailbox.Client.RegisterForMessageType(MessageType.BossLevel, OnBossLevelMessage);
		m_gameSession = GameUtils.GetGameSession();
		m_gameModeKind = m_gameSession.GameModeKind;
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		Mailbox.Client.UnregisterForMessageType(MessageType.BossLevel, OnBossLevelMessage);
	}

	protected void OnBossLevelMessage(IOnlineMultiplayerSessionUserId _sessionId, Serialisable _serialisable)
	{
		BossLevelMessage bossLevelMessage = (BossLevelMessage)_serialisable;
		m_bHasSucceeded = m_gameModeKind != Kind.Practice;
	}

	protected override ClientOrderControllerBase BuildOrderController(RecipeFlowGUI _recipeUI)
	{
		ClientBossOrderController clientBossOrderController = new ClientBossOrderController(_recipeUI);
		clientBossOrderController.SetRoundTimer(base.RoundTimer);
		return clientBossOrderController;
	}

	protected override void FinaliseRoundTimer()
	{
	}

	protected override int GetStarRating(int _points)
	{
		if (DebugManager.Instance.GetOption("Skip Level 4 star"))
		{
			return 4;
		}
		if (DebugManager.Instance.GetOption("Skip Level 1 star") || DebugManager.Instance.GetOption("Skip Level 2 star") || DebugManager.Instance.GetOption("Skip Level 3 star"))
		{
			return 3;
		}
		if (DebugManager.Instance.GetOption("Skip Level"))
		{
			return 0;
		}
		int num = ((!m_gameSession.Progress.SaveData.IsNGPEnabledForLevel(GameUtils.GetLevelID()) || !m_gameSession.Progress.SaveData.NewGamePlusDialogShown) ? 3 : 4);
		return m_bHasSucceeded ? num : 0;
	}
}
