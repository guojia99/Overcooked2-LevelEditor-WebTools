using System.Collections;
using Team17.Online;
using UnityEngine;
using UnityEngine.EventSystems;

public class SurvivalModeRatingUIController : UIControllerBase
{
	public struct ScoreData
	{
		public int m_timeSurvived;

		public int m_successPoints;

		public int m_failDeductions;

		public int m_tips;

		public int m_score;

		public int m_totalSuccessfulDeliveries;
	}

	[SerializeField]
	private T17Text m_levelTitleText;

	[SerializeField]
	private UIPlayerRootMenu m_uiPlayers;

	[SerializeField]
	public GameObject m_waitingForPlayers;

	[SerializeField]
	private DisplayTimeUIController m_timeSurvived;

	[SerializeField]
	private DisplayIntUIController m_successfulDeliveriesScore;

	[SerializeField]
	private DisplayIntUIController m_failedDeliveriesScore;

	[SerializeField]
	private DisplayIntUIController m_tipsScore;

	[SerializeField]
	private DisplayIntUIController m_totalScore;

	[SerializeField]
	private Animator m_onionKingAnimator;

	[SerializeField]
	private Animator m_kevinAnimator;

	[SerializeField]
	[Range(0.001f, 10f)]
	private float m_tickUpDuration = 1f;

	private float m_tickUpProgress;

	[SerializeField]
	private T17Text m_successfulDeliveriesTitle;

	[SerializeField]
	private T17Text m_failedDeliveriesTitle;

	[SerializeField]
	private T17Text m_legendText;

	public string m_LegendText_Emote_NoRestart = "Text.Menu.Legend03";

	public string m_LegendText_NoEmote_NoRestart = "Text.Menu.Legend03NoEmote";

	public string m_LegendText_Emote_Restart = "Text.Menu.Legend03Restart";

	public string m_LegendText_NoEmote_Restart = "Text.Menu.Legend03RestartNoEmote";

	private static readonly string m_focusedLegendText = "Text.Menu.RoundResultsCancel";

	private static readonly string m_ordersDeliveredText = "Text.Menu.OrdersDelivered";

	private static readonly string m_ordersFailedText = "Text.Menu.OrdersFailed";

	private ILogicalButton m_focusPlayersButton;

	private ILogicalButton m_backButton;

	private bool m_focusedOnPlayers;

	private void Awake()
	{
		m_focusPlayersButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UIResultsToggleProfile);
		m_backButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UICancel);
		m_uiPlayers.AllowSettingFocus = false;
		m_waitingForPlayers.SetActive(false);
	}

	private void Start()
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

	private void UpdateLegend()
	{
		if (!ConnectionStatus.IsHost() && ConnectionStatus.IsInSession())
		{
			m_legendText.SetLocalisedTextCatchAll(m_LegendText_Emote_NoRestart);
		}
		else if (!UserSystemUtils.AnySplitPadUsers())
		{
			m_legendText.SetLocalisedTextCatchAll((ClientGameSetup.Mode != GameMode.Campaign) ? m_LegendText_Emote_NoRestart : m_LegendText_Emote_Restart);
		}
		else
		{
			m_legendText.SetLocalisedTextCatchAll((ClientGameSetup.Mode != GameMode.Campaign) ? m_LegendText_NoEmote_NoRestart : m_LegendText_NoEmote_Restart);
		}
	}

	public void SetScoreData(object _scoreData)
	{
		ScoreData scoreData = (ScoreData)_scoreData;
		GameSession gameSession = GameUtils.GetGameSession();
		SceneDirectoryData sceneDirectory = gameSession.Progress.GetSceneDirectory();
		int levelID = GameUtils.GetLevelID();
		GameProgress.GameProgressData.LevelProgress progress = gameSession.Progress.GetProgress(levelID);
		SceneDirectoryData.SceneDirectoryEntry sceneDirectoryEntry = sceneDirectory.Scenes[levelID];
		SceneDirectoryData.PerPlayerCountDirectoryEntry sceneDirectoryVarientEntry = gameSession.LevelSettings.SceneDirectoryVarientEntry;
		m_levelTitleText.SetLocalisedTextCatchAll(sceneDirectoryEntry.Label);
		if (m_uiPlayers != null)
		{
			GamepadUser user = GameUtils.RequestManager<PlayerManager>().GetUser(EngagementSlot.One);
			m_uiPlayers.Show(user, null, null);
		}
		string nonLocalizedText = Localization.Get(m_ordersDeliveredText, new LocToken("Count", "0"));
		m_successfulDeliveriesTitle.SetNonLocalizedText(nonLocalizedText);
		string nonLocalizedText2 = Localization.Get(m_ordersFailedText, new LocToken("Count", "0"));
		m_failedDeliveriesTitle.SetNonLocalizedText(nonLocalizedText2);
		StartCoroutine(TickUpScore(scoreData));
	}

	private IEnumerator TickUpScore(ScoreData _scoreData)
	{
		GameConfig gameConfig = GameUtils.GetGameConfig();
		m_timeSurvived.Value = _scoreData.m_timeSurvived;
		float timePerElement = m_tickUpDuration / 4f;
		int nSuccessfulDeliveries = _scoreData.m_totalSuccessfulDeliveries;
		for (int it = 1; it <= nSuccessfulDeliveries; it++)
		{
			m_successfulDeliveriesScore.Value = _scoreData.m_successPoints / nSuccessfulDeliveries * it;
			string strText = Localization.Get(m_ordersDeliveredText, new LocToken("Count", it.ToString()));
			m_successfulDeliveriesTitle.SetNonLocalizedText(strText);
			yield return null;
		}
		m_tickUpProgress = 0f;
		while (m_tickUpProgress < 1f)
		{
			m_tickUpProgress = Mathf.Min(m_tickUpProgress + Time.deltaTime / timePerElement, 1f);
			m_tipsScore.Value = Mathf.RoundToInt((float)_scoreData.m_tips * m_tickUpProgress);
			yield return null;
		}
		int nFailedDeliveries = _scoreData.m_failDeductions / gameConfig.RecipeTimeOutPointLoss;
		for (int i = 1; i <= nFailedDeliveries; i++)
		{
			m_failedDeliveriesScore.Value = -(_scoreData.m_failDeductions / nFailedDeliveries) * i;
			string strText2 = Localization.Get(m_ordersFailedText, new LocToken("Count", i.ToString()));
			m_failedDeliveriesTitle.SetNonLocalizedText(strText2);
			yield return null;
		}
		m_tickUpProgress = 0f;
		while (m_tickUpProgress < 1f)
		{
			m_tickUpProgress = Mathf.Min(m_tickUpProgress + Time.deltaTime / timePerElement, 1f);
			m_totalScore.Value = Mathf.RoundToInt((float)_scoreData.m_score * m_tickUpProgress);
			yield return null;
		}
		m_successfulDeliveriesScore.Value = _scoreData.m_successPoints;
		m_tipsScore.Value = _scoreData.m_tips;
		m_totalScore.Value = _scoreData.m_score;
		string strSuccesfulDeliveriesText = Localization.Get(m_ordersDeliveredText, new LocToken("Count", nSuccessfulDeliveries.ToString()));
		m_successfulDeliveriesTitle.SetNonLocalizedText(strSuccesfulDeliveriesText);
		string strFailedDeliveriesText = Localization.Get(m_ordersFailedText, new LocToken("Count", nFailedDeliveries.ToString()));
		m_failedDeliveriesTitle.SetNonLocalizedText(strFailedDeliveriesText);
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

	public bool AllowedToRestart()
	{
		return !m_focusedOnPlayers;
	}

	private void Update()
	{
		if (!(m_uiPlayers != null))
		{
			return;
		}
		if (m_focusPlayersButton.JustPressed())
		{
			m_focusPlayersButton.ClaimPressEvent();
			if (T17EventSystemsManager.Instance != null)
			{
				T17EventSystem eventSystemForEngagementSlot = T17EventSystemsManager.Instance.GetEventSystemForEngagementSlot(EngagementSlot.One);
				if (eventSystemForEngagementSlot != null && eventSystemForEngagementSlot.currentSelectedGameObject == null)
				{
					m_focusedOnPlayers = true;
					m_uiPlayers.FocusOnFirstPlayer(true);
					m_legendText.SetLocalisedTextCatchAll(m_focusedLegendText);
				}
			}
		}
		if (!m_backButton.JustPressed())
		{
			return;
		}
		m_backButton.ClaimPressEvent();
		if (T17EventSystemsManager.Instance == null)
		{
			return;
		}
		T17EventSystem eventSystemForEngagementSlot2 = T17EventSystemsManager.Instance.GetEventSystemForEngagementSlot(EngagementSlot.One);
		if (eventSystemForEngagementSlot2 != null)
		{
			if (eventSystemForEngagementSlot2.currentSelectedGameObject != null)
			{
				m_uiPlayers.CloseAllPlayerMenus();
				eventSystemForEngagementSlot2.SetSelectedGameObject(null);
				((EventSystem)eventSystemForEngagementSlot2).SetSelectedGameObject((GameObject)null);
				UpdateLegend();
			}
			else
			{
				m_focusedOnPlayers = false;
			}
		}
	}
}
