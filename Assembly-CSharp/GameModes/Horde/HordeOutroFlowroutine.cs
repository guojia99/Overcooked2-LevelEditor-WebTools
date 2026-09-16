using System.Collections;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

namespace GameModes.Horde
{
	public class HordeOutroFlowroutine : OutroFlowroutineBase
	{
		private HordeOutroFlowroutineData m_data;

		private PauseMenuManager m_pauseManager;

		private CampaignAudioManager m_campaignAudioManager;

		private Canvas m_hoverIconCanvas;

		private ILogicalButton m_selectButton;

		private ILogicalButton m_restartButton;

		private GameObject m_outroInstance;

		private GameObject m_successFailInstance;

		private HordeRatingUIController m_ratingController;

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
			m_data = (HordeOutroFlowroutineData)flowroutineData;
			m_pauseManager = GameUtils.RequireManager<PauseMenuManager>();
			m_campaignAudioManager = GameUtils.RequireManager<CampaignAudioManager>();
			m_selectButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UISelect);
			m_restartButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UIRestartLevel);
			m_hoverIconCanvas = GameUtils.GetNamedCanvas("HoverIconCanvas").RequireComponent<Canvas>();
			if (!m_data.m_success && m_data.m_failureOutroPrefab != null)
			{
				Camera main = Camera.main;
				m_outroInstance = m_data.m_failureOutroPrefab.InstantiateOnParent(main.transform, false);
				m_outroInstance.SetActive(false);
			}
			if (m_data.m_success)
			{
				m_successFailInstance = GameUtils.InstantiateUIControllerOnScalingHUDCanvas(m_data.m_successUIPrefab);
			}
			else
			{
				m_successFailInstance = GameUtils.InstantiateUIControllerOnScalingHUDCanvas(m_data.m_failedUIPrefab);
			}
			m_successFailInstance.SetActive(false);
			GameObject obj = GameUtils.InstantiateUIController(m_data.m_hordeRatingUIController.gameObject, "UICanvas");
			m_ratingController = obj.RequireComponent<HordeRatingUIController>();
			m_ratingController.gameObject.SetActive(false);
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
			if (m_outroInstance != null)
			{
				m_hoverIconCanvas.enabled = false;
				m_outroInstance.SetActive(true);
				GameUtils.TriggerAudio(GameOneShotAudioTag.DLC_07_Mist, layer);
				IEnumerator timer = CoroutineUtils.TimerRoutine(m_data.m_failureOutroDelaySeconds, layer);
				while (timer != null && timer.MoveNext())
				{
					yield return null;
				}
				if (m_successFailInstance != null)
				{
					m_successFailInstance.SetActive(true);
					GameUtils.TriggerAudio(GameOneShotAudioTag.DLC_07_Failed, layer);
					timer = CoroutineUtils.TimerRoutine(m_data.m_successFailPrefabDelaySeconds, layer);
					while (timer != null && timer.MoveNext())
					{
						yield return null;
					}
				}
				m_hoverIconCanvas.enabled = true;
			}
			else
			{
				if (m_successFailInstance != null)
				{
					m_successFailInstance.SetActive(true);
					GameUtils.TriggerAudio(GameOneShotAudioTag.DLC_07_Success, layer);
					IEnumerator timer2 = CoroutineUtils.TimerRoutine(m_data.m_successFailPrefabDelaySeconds, layer);
					while (timer2 != null && timer2.MoveNext())
					{
						yield return null;
					}
				}
				GameUtils.TriggerAudio(GameOneShotAudioTag.LevelEnd, layer);
			}
			m_ratingController.gameObject.SetActive(true);
			m_ratingController.SetScoreData(m_data.m_scoreData);
			IEnumerator minTimeDelay = CoroutineUtils.TimerRoutine(1f, layer);
			while (minTimeDelay.MoveNext())
			{
				yield return null;
			}
			while (!m_ratingController.HasAnimationSettled())
			{
				yield return null;
			}
			IEnumerator timeoutRoutine = CoroutineUtils.TimerRoutine(m_data.m_minOutroDelaySeconds, layer);
			while (timeoutRoutine.MoveNext())
			{
				if (m_ratingController.AllowedToSkip())
				{
					if (m_selectButton.JustPressed())
					{
						break;
					}
					if (m_ratingController.AllowedToRestart() && m_restartButton.JustPressed())
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
				m_ratingController.m_waitingForPlayers.SetActive(true);
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
}
