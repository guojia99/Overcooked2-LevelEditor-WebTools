using System;
using System.Collections;
using Team17.Online;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CompetitiveScoreboardUIController : UIControllerBase
{
	public class ScoreData
	{
		public TeamMonitor.TeamScoreStats TeamOneData;

		public TeamMonitor.TeamScoreStats TeamTwoData;
	}

	[Serializable]
	private class PerTeamUI
	{
		public T17Text m_SuccessfulDeliveriesTitle;

		public DisplayIntUIController m_SuccessfulDeliveriesScore;

		public T17Text m_FailedDeliveriesTitle;

		public DisplayIntUIController m_FailedDeliveriesScore;

		public DisplayIntUIController m_TipsScore;

		public DisplayIntUIController m_TotalScore;

		private static readonly string m_OrdersDeliveredText = "Text.Menu.OrdersDelivered";

		private static readonly string m_OrdersFailedText = "Text.Menu.OrdersFailed";

		[SerializeField]
		[Range(0.001f, 10f)]
		private float m_tickUpDuration = 1f;

		private float m_tickUpProgress;

		private CallbackVoid m_finishedCallback = delegate
		{
		};

		public void ApplyScoreToUI(TeamMonitor.TeamScoreStats _scoreData, CompetitiveScoreboardUIController _uiController)
		{
			GameConfig gameConfig = GameUtils.GetGameConfig();
			m_SuccessfulDeliveriesScore.Value = 0;
			m_FailedDeliveriesScore.Value = 0;
			m_TipsScore.Value = 0;
			m_TotalScore.Value = 0;
			string nonLocalizedText = Localization.Get(m_OrdersDeliveredText, new LocToken("Count", "0"));
			m_SuccessfulDeliveriesTitle.SetNonLocalizedText(nonLocalizedText);
			string nonLocalizedText2 = Localization.Get(m_OrdersFailedText, new LocToken("Count", "0"));
			m_FailedDeliveriesTitle.SetNonLocalizedText(nonLocalizedText2);
			_uiController.StartCoroutine(TickUpScore(_scoreData));
		}

		private IEnumerator TickUpScore(TeamMonitor.TeamScoreStats _scoreData)
		{
			GameConfig gameConfig = GameUtils.GetGameConfig();
			float timePerElement = m_tickUpDuration / 4f;
			int nSuccessfulDeliveries = _scoreData.TotalSuccessfulDeliveries;
			for (int it = 1; it <= nSuccessfulDeliveries; it++)
			{
				m_SuccessfulDeliveriesScore.Value = _scoreData.TotalBaseScore / nSuccessfulDeliveries * it;
				string strText = Localization.Get(m_OrdersDeliveredText, new LocToken("Count", it.ToString()));
				m_SuccessfulDeliveriesTitle.SetNonLocalizedText(strText);
				yield return null;
			}
			m_tickUpProgress = 0f;
			while (m_tickUpProgress < 1f)
			{
				m_tickUpProgress = Mathf.Min(m_tickUpProgress + Time.deltaTime / timePerElement, 1f);
				m_TipsScore.Value = Mathf.RoundToInt((float)_scoreData.TotalTipsScore * m_tickUpProgress);
				yield return null;
			}
			int nFailedDeliveries = _scoreData.TotalTimeExpireDeductions / gameConfig.RecipeTimeOutPointLoss;
			for (int i = 1; i <= nFailedDeliveries; i++)
			{
				m_FailedDeliveriesScore.Value = -(_scoreData.TotalTimeExpireDeductions / nFailedDeliveries) * i;
				string strText2 = Localization.Get(m_OrdersFailedText, new LocToken("Count", i.ToString()));
				m_FailedDeliveriesTitle.SetNonLocalizedText(strText2);
				yield return null;
			}
			m_tickUpProgress = 0f;
			int totalScore = _scoreData.GetTotalScore();
			while (m_tickUpProgress < 1f)
			{
				m_tickUpProgress = Mathf.Min(m_tickUpProgress + Time.deltaTime / timePerElement, 1f);
				m_TotalScore.Value = Mathf.RoundToInt((float)totalScore * m_tickUpProgress);
				yield return null;
			}
			m_SuccessfulDeliveriesScore.Value = _scoreData.TotalBaseScore;
			m_TipsScore.Value = _scoreData.TotalTipsScore;
			m_TotalScore.Value = totalScore;
			string strSuccesfulDeliveriesText = Localization.Get(m_OrdersDeliveredText, new LocToken("Count", nSuccessfulDeliveries.ToString()));
			m_SuccessfulDeliveriesTitle.SetNonLocalizedText(strSuccesfulDeliveriesText);
			string strFailedDeliveriesText = Localization.Get(m_OrdersFailedText, new LocToken("Count", nFailedDeliveries.ToString()));
			m_FailedDeliveriesTitle.SetNonLocalizedText(strFailedDeliveriesText);
			if (m_finishedCallback != null)
			{
				m_finishedCallback();
			}
		}

		public void RegisterFinished(CallbackVoid _callback)
		{
			m_finishedCallback = (CallbackVoid)Delegate.Combine(m_finishedCallback, _callback);
		}

		public void UnregisterFinished(CallbackVoid _callback)
		{
			m_finishedCallback = (CallbackVoid)Delegate.Remove(m_finishedCallback, _callback);
		}
	}

	[SerializeField]
	private T17Text m_levelTitleText;

	[SerializeField]
	private Text m_winneris_noone;

	[SerializeField]
	private Text m_winneris_noone_Blue;

	[SerializeField]
	private Text m_winneris_one;

	[SerializeField]
	private Text m_winneris_two;

	[SerializeField]
	private UIPlayerRootMenu m_uiPlayers;

	[SerializeField]
	private PerTeamUI m_teamOne;

	[SerializeField]
	private PerTeamUI m_teamTwo;

	[SerializeField]
	private Image m_buttonIcon;

	[SerializeField]
	private T17Text m_LegendText;

	[SerializeField]
	public GameObject m_waitingForPlayers;

	public string m_LegendText_NoEmote_NoRestart = "Text.Menu.Legend03NoEmote";

	public string m_LegendText_Emote_NoRestart = "Text.Menu.Legend03";

	private static readonly string m_FocusedLegendText = "Text.Menu.RoundResultsCancel";

	private bool m_bteamOneDone;

	private bool m_bteamTwoDone;

	private ILogicalButton m_FocusPlayersButton;

	private ILogicalButton m_BackButton;

	protected void Awake()
	{
		m_FocusPlayersButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UIResultsToggleProfile);
		m_BackButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UICancel);
		m_uiPlayers.AllowSettingFocus = false;
		m_waitingForPlayers.SetActive(false);
	}

	protected void Start()
	{
		UpdateLegend();
		if (T17EventSystemsManager.Instance != null)
		{
			T17EventSystem eventSystemForEngagementSlot = T17EventSystemsManager.Instance.GetEventSystemForEngagementSlot(EngagementSlot.One);
			if (eventSystemForEngagementSlot != null)
			{
				eventSystemForEngagementSlot.SetSelectedGameObject(null);
				((EventSystem)eventSystemForEngagementSlot).SetSelectedGameObject((GameObject)null);
			}
		}
	}

	protected void Update()
	{
		if (!(m_uiPlayers != null))
		{
			return;
		}
		if (m_FocusPlayersButton.JustPressed())
		{
			m_FocusPlayersButton.ClaimPressEvent();
			if (T17EventSystemsManager.Instance != null)
			{
				T17EventSystem eventSystemForEngagementSlot = T17EventSystemsManager.Instance.GetEventSystemForEngagementSlot(EngagementSlot.One);
				if (eventSystemForEngagementSlot != null && eventSystemForEngagementSlot.currentSelectedGameObject == null)
				{
					m_uiPlayers.FocusOnFirstPlayer(true);
					m_LegendText.SetLocalisedTextCatchAll(m_FocusedLegendText);
				}
			}
		}
		if (!m_BackButton.JustPressed())
		{
			return;
		}
		m_BackButton.ClaimPressEvent();
		if (T17EventSystemsManager.Instance != null)
		{
			T17EventSystem eventSystemForEngagementSlot2 = T17EventSystemsManager.Instance.GetEventSystemForEngagementSlot(EngagementSlot.One);
			if (eventSystemForEngagementSlot2 != null && eventSystemForEngagementSlot2.currentSelectedGameObject != null)
			{
				m_uiPlayers.CloseAllPlayerMenus();
				eventSystemForEngagementSlot2.SetSelectedGameObject(null);
				((EventSystem)eventSystemForEngagementSlot2).SetSelectedGameObject((GameObject)null);
				UpdateLegend();
			}
		}
	}

	private void UpdateLegend()
	{
		if (!UserSystemUtils.AnySplitPadUsers())
		{
			m_LegendText.SetLocalisedTextCatchAll(m_LegendText_Emote_NoRestart);
		}
		else
		{
			m_LegendText.SetLocalisedTextCatchAll(m_LegendText_NoEmote_NoRestart);
		}
	}

	public void SetScoreData(object _scoreData)
	{
		ScoreData scoreData = _scoreData as ScoreData;
		GameSession gameSession = GameUtils.GetGameSession();
		SceneDirectoryData sceneDirectory = gameSession.Progress.GetSceneDirectory();
		int levelID = GameUtils.GetLevelID();
		GameProgress.GameProgressData.LevelProgress progress = gameSession.Progress.GetProgress(levelID);
		SceneDirectoryData.SceneDirectoryEntry sceneDirectoryEntry = sceneDirectory.Scenes[levelID];
		SceneDirectoryData.PerPlayerCountDirectoryEntry sceneDirectoryVarientEntry = gameSession.LevelSettings.SceneDirectoryVarientEntry;
		m_levelTitleText.SetLocalisedTextCatchAll(sceneDirectoryEntry.Label);
		m_winneris_noone.enabled = false;
		m_winneris_noone_Blue.enabled = false;
		m_winneris_one.enabled = false;
		m_winneris_two.enabled = false;
		if (m_uiPlayers != null)
		{
			GamepadUser user = GameUtils.RequestManager<PlayerManager>().GetUser(EngagementSlot.One);
			m_uiPlayers.m_canKickUsers = true;
			m_uiPlayers.Show(user, null, null);
		}
		m_teamOne.RegisterFinished(delegate
		{
			m_bteamOneDone = true;
			if (m_bteamTwoDone)
			{
				ShowWinner(scoreData.TeamOneData, scoreData.TeamTwoData);
			}
		});
		m_teamTwo.RegisterFinished(delegate
		{
			m_bteamTwoDone = true;
			if (m_bteamOneDone)
			{
				ShowWinner(scoreData.TeamOneData, scoreData.TeamTwoData);
			}
		});
		m_teamOne.ApplyScoreToUI(scoreData.TeamOneData, this);
		m_teamTwo.ApplyScoreToUI(scoreData.TeamTwoData, this);
	}

	protected void ShowWinner(TeamMonitor.TeamScoreStats _score1, TeamMonitor.TeamScoreStats _score2)
	{
		float num = _score1.GetTotalScore();
		float num2 = _score2.GetTotalScore();
		m_winneris_noone.enabled = num == num2;
		m_winneris_noone_Blue.enabled = num == num2;
		m_winneris_one.enabled = num > num2;
		m_winneris_two.enabled = num < num2;
	}

	public bool IsButtonActive()
	{
		return m_buttonIcon.enabled;
	}

	public bool AllowedToSkip()
	{
		if (T17EventSystemsManager.Instance != null)
		{
			T17EventSystem eventSystemForEngagementSlot = T17EventSystemsManager.Instance.GetEventSystemForEngagementSlot(EngagementSlot.One);
			if (eventSystemForEngagementSlot != null)
			{
				return eventSystemForEngagementSlot.currentSelectedGameObject == null;
			}
		}
		return true;
	}
}
