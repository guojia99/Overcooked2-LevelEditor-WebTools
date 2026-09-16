using System.Collections;
using Team17.Online;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CoopStarRatingUIController : StarRatingUIController
{
	public class ScoreData
	{
		public int StarRating;

		public bool StarRatingIncreased;

		public int SuccessPoints;

		public int FailDeductions;

		public int Tips;

		public int Score;

		public int TotalSuccessfulDeliveries;

		public bool JustUnlockedNGP;
	}

	[SerializeField]
	private T17Text m_levelTitleText;

	[SerializeField]
	private DisplayIntUIController m_successfulDeliveriesScore;

	[SerializeField]
	private DisplayIntUIController m_failedDeliveriesScore;

	[SerializeField]
	private DisplayIntUIController m_tipsScore;

	[SerializeField]
	private DisplayIntUIController m_totalScore;

	[SerializeField]
	private Text m_highScoreLabel;

	[SerializeField]
	private DisplayIntUIController m_highScore;

	[SerializeField]
	private Text m_nextStarLabel;

	[SerializeField]
	private DisplayIntUIController m_nextStarScore;

	[SerializeField]
	[Range(0.001f, 10f)]
	private float m_tickUpDuration = 1f;

	private float m_tickUpProgress;

	private CallbackVoid m_finishedCallback = delegate
	{
	};

	[SerializeField]
	private UIPlayerRootMenu m_uiPlayers;

	[SerializeField]
	private Animator m_OnionKingAnimator;

	[SerializeField]
	private Animator m_KevinAnimator;

	[SerializeField]
	private T17Text m_SuccessfulDeliveriesTitle;

	[SerializeField]
	private T17Text m_FailedDeliveriesTitle;

	[SerializeField]
	private T17Text m_LegendText;

	[SerializeField]
	public GameObject m_waitingForPlayers;

	[SerializeField]
	[AssignChild("ContentBacker", Editorbility.Editable)]
	private Transform m_contentBacker;

	private ScoreBoundaryStar[] m_stars;

	[SerializeField]
	[AssignResource("NGPScoreBoardStars", Editorbility.Editable)]
	private GameObject m_scoreboardStarsPrefab;

	private static readonly string m_OrdersDeliveredText = "Text.Menu.OrdersDelivered";

	private static readonly string m_OrdersFailedText = "Text.Menu.OrdersFailed";

	public string m_LegendText_Emote_NoRestart = "Text.Menu.Legend03";

	public string m_LegendText_Emote_Restart = "Text.Menu.Legend03Restart";

	public string m_LegendText_NoEmote_NoRestart = "Text.Menu.Legend03NoEmote";

	public string m_LegendText_NoEmote_Restart = "Text.Menu.Legend03RestartNoEmote";

	private static readonly string m_FocusedLegendText = "Text.Menu.RoundResultsCancel";

	private static readonly int m_iScore = Animator.StringToHash("Score");

	private ILogicalButton m_FocusPlayersButton;

	private ILogicalButton m_BackButton;

	private bool m_focusedOnPlayers;

	protected override void Awake()
	{
		base.Awake();
		m_FocusPlayersButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UIResultsToggleProfile);
		m_BackButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UICancel);
		m_uiPlayers.AllowSettingFocus = false;
		m_waitingForPlayers.SetActive(false);
		if (m_scoreboardStarsPrefab != null && m_contentBacker != null)
		{
			GameObject gameObject = Object.Instantiate(m_scoreboardStarsPrefab, m_contentBacker);
			gameObject.transform.localScale = Vector3.one;
			m_stars = gameObject.gameObject.RequestComponentsRecursive<ScoreBoundaryStar>();
			while (gameObject.transform.childCount > 0)
			{
				Transform child = gameObject.transform.GetChild(0);
				child.SetParent(m_contentBacker);
			}
			Object.Destroy(gameObject);
		}
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

	public override void SetScoreData(object _scoreData)
	{
		ScoreData scoreData = _scoreData as ScoreData;
		GameSession gameSession = GameUtils.GetGameSession();
		SceneDirectoryData sceneDirectory = gameSession.Progress.GetSceneDirectory();
		int levelID = GameUtils.GetLevelID();
		GameProgress.GameProgressData.LevelProgress progress = gameSession.Progress.GetProgress(levelID);
		SceneDirectoryData.SceneDirectoryEntry sceneDirectoryEntry = sceneDirectory.Scenes[levelID];
		SceneDirectoryData.PerPlayerCountDirectoryEntry sceneDirectoryVarientEntry = gameSession.LevelSettings.SceneDirectoryVarientEntry;
		m_levelTitleText.SetLocalisedTextCatchAll(sceneDirectoryEntry.Label);
		GameConfig gameConfig = GameUtils.GetGameConfig();
		m_failedDeliveriesScore.Value = 0;
		for (int i = 1; i <= 4; i++)
		{
			ScoreBoundaryStar uIStar = GetUIStar(i);
			if (!(uIStar != null))
			{
				continue;
			}
			switch (i)
			{
			case 1:
				uIStar.Score = sceneDirectoryVarientEntry.OneStarScore;
				break;
			case 2:
				uIStar.Score = sceneDirectoryVarientEntry.TwoStarScore;
				break;
			case 3:
				uIStar.Score = sceneDirectoryVarientEntry.ThreeStarScore;
				break;
			case 4:
			{
				bool flag = !scoreData.JustUnlockedNGP && gameSession.Progress.SaveData.IsNGPEnabledForLevel(GameUtils.GetLevelID()) && gameSession.Progress.SaveData.NewGamePlusDialogShown;
				uIStar.gameObject.SetActive(flag);
				if (flag)
				{
					uIStar.Score = sceneDirectoryVarientEntry.FourStarScore;
				}
				break;
			}
			}
		}
		if (m_highScoreLabel != null && m_highScore != null)
		{
			if (progress.Completed)
			{
				m_highScore.Value = Mathf.Max(progress.HighScore, scoreData.Score);
			}
			else
			{
				m_highScore.gameObject.SetActive(false);
				m_highScoreLabel.gameObject.SetActive(false);
			}
		}
		if (m_nextStarLabel != null && m_nextStarScore != null)
		{
			if (scoreData.StarRating < 4 && scoreData.StarRating >= 0)
			{
				m_nextStarScore.Value = sceneDirectoryVarientEntry.GetPointsForStar(scoreData.StarRating + 1);
			}
			else
			{
				m_nextStarLabel.gameObject.SetActive(false);
				m_nextStarScore.gameObject.SetActive(false);
			}
		}
		if (m_uiPlayers != null)
		{
			GamepadUser user = GameUtils.RequestManager<PlayerManager>().GetUser(EngagementSlot.One);
			m_uiPlayers.Show(user, null, null);
		}
		if (m_OnionKingAnimator != null)
		{
			m_OnionKingAnimator.SetInteger(m_iScore, scoreData.StarRating);
			m_OnionKingAnimator.Update(0f);
		}
		if (m_KevinAnimator != null)
		{
			m_KevinAnimator.SetInteger(m_iScore, scoreData.StarRating);
			m_KevinAnimator.Update(0f);
		}
		string nonLocalizedText = Localization.Get(m_OrdersDeliveredText, new LocToken("Count", "0"));
		m_SuccessfulDeliveriesTitle.SetNonLocalizedText(nonLocalizedText);
		string nonLocalizedText2 = Localization.Get(m_OrdersFailedText, new LocToken("Count", "0"));
		m_FailedDeliveriesTitle.SetNonLocalizedText(nonLocalizedText2);
		StartCoroutine(TickUpScore(scoreData));
	}

	public override bool AllowedToSkip()
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

	public override bool AllowedToRestart()
	{
		return !m_focusedOnPlayers;
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
					m_focusedOnPlayers = true;
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

	private void UpdateLegend()
	{
		if (!ConnectionStatus.IsHost() && ConnectionStatus.IsInSession())
		{
			m_LegendText.SetLocalisedTextCatchAll(m_LegendText_Emote_NoRestart);
		}
		else if (!UserSystemUtils.AnySplitPadUsers())
		{
			m_LegendText.SetLocalisedTextCatchAll((ClientGameSetup.Mode != GameMode.Campaign) ? m_LegendText_Emote_NoRestart : m_LegendText_Emote_Restart);
		}
		else
		{
			m_LegendText.SetLocalisedTextCatchAll((ClientGameSetup.Mode != GameMode.Campaign) ? m_LegendText_NoEmote_NoRestart : m_LegendText_NoEmote_Restart);
		}
	}

	private IEnumerator TickUpScore(ScoreData _scoreData)
	{
		GameConfig gameConfig = GameUtils.GetGameConfig();
		float timePerElement = m_tickUpDuration / 4f;
		int nSuccessfulDeliveries = _scoreData.TotalSuccessfulDeliveries;
		for (int it = 1; it <= nSuccessfulDeliveries; it++)
		{
			m_successfulDeliveriesScore.Value = _scoreData.SuccessPoints / nSuccessfulDeliveries * it;
			string strText = Localization.Get(m_OrdersDeliveredText, new LocToken("Count", it.ToString()));
			m_SuccessfulDeliveriesTitle.SetNonLocalizedText(strText);
			yield return null;
		}
		m_tickUpProgress = 0f;
		while (m_tickUpProgress < 1f)
		{
			m_tickUpProgress = Mathf.Min(m_tickUpProgress + Time.deltaTime / timePerElement, 1f);
			m_tipsScore.Value = Mathf.RoundToInt((float)_scoreData.Tips * m_tickUpProgress);
			yield return null;
		}
		int nFailedDeliveries = _scoreData.FailDeductions / gameConfig.RecipeTimeOutPointLoss;
		for (int i = 1; i <= nFailedDeliveries; i++)
		{
			m_failedDeliveriesScore.Value = -(_scoreData.FailDeductions / nFailedDeliveries) * i;
			string strText2 = Localization.Get(m_OrdersFailedText, new LocToken("Count", i.ToString()));
			m_FailedDeliveriesTitle.SetNonLocalizedText(strText2);
			yield return null;
		}
		m_tickUpProgress = 0f;
		while (m_tickUpProgress < 1f)
		{
			m_tickUpProgress = Mathf.Min(m_tickUpProgress + Time.deltaTime / timePerElement, 1f);
			m_totalScore.Value = Mathf.RoundToInt((float)_scoreData.Score * m_tickUpProgress);
			yield return null;
		}
		SetScoreData(_scoreData.StarRating);
		m_successfulDeliveriesScore.Value = _scoreData.SuccessPoints;
		m_tipsScore.Value = _scoreData.Tips;
		m_totalScore.Value = _scoreData.Score;
		string strSuccesfulDeliveriesText = Localization.Get(m_OrdersDeliveredText, new LocToken("Count", nSuccessfulDeliveries.ToString()));
		m_SuccessfulDeliveriesTitle.SetNonLocalizedText(strSuccesfulDeliveriesText);
		string strFailedDeliveriesText = Localization.Get(m_OrdersFailedText, new LocToken("Count", nFailedDeliveries.ToString()));
		m_FailedDeliveriesTitle.SetNonLocalizedText(strFailedDeliveriesText);
	}

	private ScoreBoundaryStar GetUIStar(int _star)
	{
		if (m_stars != null)
		{
			for (int i = 0; i < m_stars.Length; i++)
			{
				ScoreBoundaryStar scoreBoundaryStar = m_stars[i];
				if (scoreBoundaryStar != null && scoreBoundaryStar.m_star == _star)
				{
					return scoreBoundaryStar;
				}
			}
		}
		return null;
	}
}
