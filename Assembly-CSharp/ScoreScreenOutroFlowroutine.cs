using System.Collections;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ScoreScreenOutroFlowroutine : OutroFlowroutineBase
{
	private const float c_minScreenDuration = 1f;

	private CampaignAudioManager m_campaignAudioManager;

	private PauseMenuManager m_pauseManager;

	private ILogicalButton m_selectButton;

	private ILogicalButton m_restartButton;

	private GameObject m_timesUpUIInstance;

	private StarRatingUIController m_starRatingController;

	private AwardAvatarUIController m_awardAvatarController;

	private ScoreScreenFlowroutineData m_flowroutineData;

	private GenericVoid m_onRestartRequest;

	public GenericVoid OnRestartRequest
	{
		set
		{
			m_onRestartRequest = value;
		}
	}

	protected override void Setup(FlowroutineData _setupData)
	{
		m_flowroutineData = (ScoreScreenFlowroutineData)_setupData;
		m_pauseManager = GameUtils.RequireManager<PauseMenuManager>();
		m_campaignAudioManager = GameUtils.RequireManager<CampaignAudioManager>();
		m_selectButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UISelect);
		m_restartButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UIRestartLevel);
		GameObject obj = GameUtils.InstantiateUIController(m_flowroutineData.m_starRatingUIController.gameObject, "UICanvas");
		m_starRatingController = obj.RequireComponent<StarRatingUIController>();
		m_starRatingController.gameObject.SetActive(false);
		GameObject obj2 = GameUtils.InstantiateUIController(m_flowroutineData.m_awardAvatarUIController.gameObject, "UICanvas");
		m_awardAvatarController = obj2.RequireComponent<AwardAvatarUIController>();
		m_awardAvatarController.gameObject.SetActive(false);
		if (m_flowroutineData.TimesUpUIPrefab != null)
		{
			m_timesUpUIInstance = GameUtils.InstantiateUIControllerOnScalingHUDCanvas(m_flowroutineData.TimesUpUIPrefab);
			m_timesUpUIInstance.SetActive(false);
		}
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
		m_campaignAudioManager.SetAudioState(CampaignAudioManager.AudioState.SummaryScreen);
		m_pauseManager.enabled = false;
		TimeManager timeManger = GameUtils.RequireManager<TimeManager>();
		timeManger.SetPaused(TimeManager.PauseLayer.Main, true, this);
		int layer = LayerMask.NameToLayer("Administration");
		if (m_timesUpUIInstance != null)
		{
			m_timesUpUIInstance.SetActive(true);
			GameUtils.TriggerAudio(GameOneShotAudioTag.TimesUp, layer);
			IEnumerator timeUpDelay = CoroutineUtils.TimerRoutine(m_flowroutineData.TimesUpUILifetime, layer);
			while (timeUpDelay.MoveNext())
			{
				yield return null;
			}
		}
		GameUtils.TriggerAudio(GameOneShotAudioTag.LevelEnd, layer);
		m_starRatingController.gameObject.SetActive(true);
		m_starRatingController.SetScoreData(m_flowroutineData.m_scoreData);
		IEnumerator minTimeDelay = CoroutineUtils.TimerRoutine(1f, layer);
		while (minTimeDelay.MoveNext())
		{
			yield return null;
		}
		while (!m_starRatingController.HasAnimationSettled())
		{
			yield return null;
		}
		m_starRatingController.SetButtonActive();
		IEnumerator timeoutRoutine = CoroutineUtils.TimerRoutine(m_flowroutineData.m_fTimeout, layer);
		while (timeoutRoutine.MoveNext())
		{
			if (m_starRatingController.AllowedToSkip())
			{
				bool flag = m_starRatingController.AllowedToRestart() && m_restartButton.JustPressed();
				bool flag2 = flag || m_selectButton.JustPressed();
				if (flag && m_onRestartRequest != null)
				{
					m_restartButton.ClaimPressEvent();
					m_restartButton.ClaimReleaseEvent();
					m_onRestartRequest();
				}
				if (flag2)
				{
					break;
				}
			}
			yield return null;
		}
		if (ConnectionStatus.IsInSession())
		{
			CoopStarRatingUIController coopStarRatingUIController = (CoopStarRatingUIController)m_starRatingController;
			if (coopStarRatingUIController != null)
			{
				coopStarRatingUIController.m_waitingForPlayers.SetActive(true);
			}
		}
		if (m_flowroutineData.m_unlocks == null || m_flowroutineData.m_unlocks.Length <= 0)
		{
			yield break;
		}
		m_starRatingController.gameObject.SetActive(false);
		m_awardAvatarController.gameObject.SetActive(true);
		m_awardAvatarController.EnableButton();
		GameSession gameSession = GameUtils.GetGameSession();
		for (int i = 0; i < m_flowroutineData.m_unlocks.Length; i++)
		{
			GameProgress.UnlockData unlock = m_flowroutineData.m_unlocks[i];
			if (unlock.Type == GameProgress.UnlockData.UnlockType.Avatar)
			{
				AvatarDirectoryData avatarDirectory = gameSession.Progress.GetAvatarDirectory();
				m_awardAvatarController.SetData(unlock.AvatarData.GetAvatarData(avatarDirectory));
				timeoutRoutine = CoroutineUtils.TimerRoutine(m_awardAvatarController.m_awardTimeout, layer);
				while (timeoutRoutine.MoveNext() && !m_selectButton.JustPressed() && !m_restartButton.JustPressed())
				{
					yield return null;
				}
			}
		}
		if (ConnectionStatus.IsInSession())
		{
			m_awardAvatarController.m_WaitingForPlayersText.gameObject.SetActive(true);
		}
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
