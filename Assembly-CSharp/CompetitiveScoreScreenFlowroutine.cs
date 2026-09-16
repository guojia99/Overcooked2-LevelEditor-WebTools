using System.Collections;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class CompetitiveScoreScreenFlowroutine : CompetitiveFlowController.OutroFlowroutine
{
	[SerializeField]
	private LevelOutroFlowroutineData m_data;

	[SerializeField]
	private CompetitiveScoreboardUIController m_scoreboardPrefab;

	[SerializeField]
	private float m_fTimeout = 20f;

	private CampaignAudioManager m_campaignAudioManager;

	private PauseMenuManager m_pauseManager;

	private ILogicalButton m_selectButton;

	private GameObject m_timesUpUIInstance;

	private CompetitiveScoreboardUIController m_scoreboardController;

	private object m_scoreData;

	private void Awake()
	{
		m_pauseManager = GameUtils.RequireManager<PauseMenuManager>();
		m_campaignAudioManager = GameUtils.RequireManager<CampaignAudioManager>();
		m_selectButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UISelect);
	}

	protected override void Setup(CompetitiveFlowController.OutroData _setupData)
	{
		GameObject obj = GameUtils.InstantiateUIController(m_scoreboardPrefab.gameObject, "UICanvas");
		m_scoreboardController = obj.RequireComponent<CompetitiveScoreboardUIController>();
		m_scoreboardController.gameObject.SetActive(false);
		if (m_data.TimesUpUIPrefab != null)
		{
			m_timesUpUIInstance = GameUtils.InstantiateUIControllerOnScalingHUDCanvas(m_data.TimesUpUIPrefab);
			m_timesUpUIInstance.transform.SetSiblingIndex(0);
			m_timesUpUIInstance.SetActive(false);
		}
		m_scoreData = _setupData.ScoreData;
	}

	protected override IEnumerator Run()
	{
		if (T17InGameFlow.Instance != null)
		{
			T17InGameFlow.Instance.SetInRoundResults(true);
			Mailbox.Client.RegisterForMessageType(MessageType.LevelLoadByIndex, OnLevelLoad);
			Mailbox.Client.RegisterForMessageType(MessageType.LevelLoadByName, OnLevelLoad);
		}
		PlayerInputLookup.ResetToDefaultInputConfig();
		float startTime = Time.realtimeSinceStartup;
		m_campaignAudioManager.SetAudioState(CampaignAudioManager.AudioState.SummaryScreen);
		m_pauseManager.enabled = false;
		TimeManager timeManger = GameUtils.RequireManager<TimeManager>();
		timeManger.SetPaused(TimeManager.PauseLayer.Main, true, this);
		int layer = LayerMask.NameToLayer("Administration");
		if (m_timesUpUIInstance != null)
		{
			GameUtils.TriggerAudio(GameOneShotAudioTag.TimesUp, layer);
			m_timesUpUIInstance.SetActive(true);
			IEnumerator timeUpDelay = CoroutineUtils.TimerRoutine(m_data.TimesUpUILifetime, layer);
			while (timeUpDelay.MoveNext())
			{
				yield return null;
			}
		}
		GameUtils.TriggerAudio(GameOneShotAudioTag.LevelEnd, layer);
		m_scoreboardController.gameObject.SetActive(true);
		m_scoreboardController.SetScoreData(m_scoreData);
		while (!m_scoreboardController.IsButtonActive())
		{
			yield return null;
		}
		IEnumerator timeoutRoutine = CoroutineUtils.TimerRoutine(m_fTimeout, layer);
		while ((!m_scoreboardController.AllowedToSkip() || !m_selectButton.JustPressed()) && timeoutRoutine.MoveNext())
		{
			yield return null;
		}
		m_scoreboardController.m_waitingForPlayers.SetActive(true);
	}

	private void OnLevelLoad(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		Mailbox.Client.UnregisterForMessageType(MessageType.LevelLoadByIndex, OnLevelLoad);
		Mailbox.Client.UnregisterForMessageType(MessageType.LevelLoadByName, OnLevelLoad);
		if (T17InGameFlow.Instance != null)
		{
			T17InGameFlow.Instance.SetInRoundResults(false);
		}
	}

	protected override void Shutdown()
	{
	}
}
