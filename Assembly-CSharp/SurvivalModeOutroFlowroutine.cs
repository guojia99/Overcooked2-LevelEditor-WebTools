using System.Collections;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class SurvivalModeOutroFlowroutine : OutroFlowroutineBase
{
	private SurvivalModeOutroFlowroutineData m_flowroutineData;

	private PauseMenuManager m_pauseManager;

	private CampaignAudioManager m_campaignAudioManager;

	private GameObject m_timesUpUIInstance;

	private SurvivalModeRatingUIController m_survivalModeRatingController;

	private ILogicalButton m_selectButton;

	private ILogicalButton m_restartButton;

	private GenericVoid m_onRestartRequest;

	public GenericVoid OnRestartRequest
	{
		set
		{
			m_onRestartRequest = value;
		}
	}

	protected override void Setup(FlowroutineData flowroutineData)
	{
		m_flowroutineData = (SurvivalModeOutroFlowroutineData)flowroutineData;
		m_pauseManager = GameUtils.RequireManager<PauseMenuManager>();
		m_campaignAudioManager = GameUtils.RequireManager<CampaignAudioManager>();
		m_selectButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UISelect);
		m_restartButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UIRestartLevel);
		if (m_flowroutineData.m_timesUpUIPrefab != null)
		{
			m_timesUpUIInstance = GameUtils.InstantiateUIControllerOnScalingHUDCanvas(m_flowroutineData.m_timesUpUIPrefab);
			m_timesUpUIInstance.SetActive(false);
		}
		GameObject obj = GameUtils.InstantiateUIController(m_flowroutineData.m_survivalModeRatingUIController.gameObject, "UICanvas");
		m_survivalModeRatingController = obj.RequireComponent<SurvivalModeRatingUIController>();
		m_survivalModeRatingController.gameObject.SetActive(false);
	}

	protected override void Shutdown()
	{
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
			IEnumerator timeUpDelay = CoroutineUtils.TimerRoutine(m_flowroutineData.m_minTimesUpDuration, layer);
			while (timeUpDelay.MoveNext())
			{
				yield return null;
			}
		}
		GameUtils.TriggerAudio(GameOneShotAudioTag.LevelEnd, layer);
		m_survivalModeRatingController.gameObject.SetActive(true);
		m_survivalModeRatingController.SetScoreData(m_flowroutineData.m_scoreData);
		IEnumerator minTimeDelay = CoroutineUtils.TimerRoutine(m_flowroutineData.m_minRatingDuration, layer);
		while (minTimeDelay.MoveNext())
		{
			yield return null;
		}
		IEnumerator timeoutRoutine = CoroutineUtils.TimerRoutine(m_flowroutineData.m_minOutroDuration, layer);
		while (timeoutRoutine.MoveNext())
		{
			if (m_survivalModeRatingController.AllowedToSkip())
			{
				if (m_selectButton.JustPressed())
				{
					break;
				}
				if (m_survivalModeRatingController.AllowedToRestart() && m_restartButton.JustPressed())
				{
					m_restartButton.ClaimPressEvent();
					m_restartButton.ClaimReleaseEvent();
					m_onRestartRequest();
					break;
				}
			}
			yield return null;
		}
		if (ConnectionStatus.IsInSession())
		{
			m_survivalModeRatingController.m_waitingForPlayers.SetActive(true);
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
}
