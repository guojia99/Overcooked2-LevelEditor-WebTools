using System;
using GameModes;
using GameModes.Horde;
using Team17.Online;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Animator))]
public class WorldMapKitchenLevelIconUI : WorldMapLevelIconUI
{
	[Serializable]
	public enum ScoreFormat
	{
		Number = 0,
		Time = 1
	}

	[Serializable]
	private class UserScoreUI
	{
		[SerializeField]
		private GameObject m_container;

		[SerializeField]
		private Text m_name;

		[SerializeField]
		private Text m_score;

		public void SetActive(bool _active)
		{
			m_container.SetActive(_active);
		}

		public void SetName(string _name)
		{
			string text = _name;
			if (_name.Length > 15)
			{
				text = text.Substring(0, 12) + "…";
			}
			m_name.text = text;
		}

		public void SetScoreText(string _text)
		{
			if (!(m_score == null))
			{
				m_score.text = _text;
			}
		}
	}

	public enum State
	{
		UnAffordable = 0,
		Affordable = 1,
		Purchased = 2,
		UnSupported = 3
	}

	[SerializeField]
	private Text[] m_costText;

	private static readonly int m_iPurchaseState = Animator.StringToHash("PurchaseState");

	[HideInInspector]
	private ScoreBoundaryStar[] m_stars;

	private SceneDirectoryData.SceneDirectoryEntry m_sceneData;

	[SerializeField]
	private Image[] m_levelImages;

	[SerializeField]
	private string m_noScoreText = "----";

	[SerializeField]
	private UserScoreUI[] m_userScoreUis;

	protected override void Awake()
	{
		base.Awake();
		m_stars = base.gameObject.RequestComponentsRecursive<ScoreBoundaryStar>();
	}

	public void Setup(SceneDirectoryData.SceneDirectoryEntry _sceneData, GameProgress.GameProgressData.LevelProgress _levelProgress, State _state)
	{
		m_sceneData = _sceneData;
		SetState(_state);
		UpdateStarVisibility(_levelProgress);
		SetUserScores(_levelProgress.LevelId);
		if (_sceneData == null)
		{
			return;
		}
		SetTitle(_sceneData.Label);
		SetCost(_sceneData.StarCost);
		for (int i = 0; i < m_levelImages.Length; i++)
		{
			Image image = m_levelImages[i];
			if (image != null)
			{
				image.sprite = _sceneData.LoadScreenOverride;
			}
		}
	}

	public void SetCost(int _cost)
	{
		for (int i = 0; i < m_costText.Length; i++)
		{
			Text text = m_costText[i];
			if (!(text == null))
			{
				text.text = _cost.ToString();
			}
		}
	}

	public void UpdateStarVisibility(GameProgress.GameProgressData.LevelProgress _levelProgress)
	{
		ScoreBoundaryStar uIStar = GetUIStar(1);
		ScoreBoundaryStar uIStar2 = GetUIStar(2);
		ScoreBoundaryStar uIStar3 = GetUIStar(3);
		if (uIStar == null || uIStar2 == null || uIStar3 == null)
		{
			return;
		}
		ScoreBoundaryStar uIStar4 = GetUIStar(4);
		if (uIStar4 == null)
		{
			return;
		}
		SceneDirectoryData.PerPlayerCountDirectoryEntry sceneVarient = m_sceneData.GetSceneVarient(ClientUserSystem.m_Users.Count);
		if (sceneVarient == null)
		{
			return;
		}
		uIStar.Score = sceneVarient.OneStarScore;
		uIStar2.Score = sceneVarient.TwoStarScore;
		uIStar3.Score = sceneVarient.ThreeStarScore;
		for (int i = 0; i < m_stars.Length; i++)
		{
			ScoreBoundaryStar scoreBoundaryStar = m_stars[i];
			if (scoreBoundaryStar != null)
			{
				scoreBoundaryStar.SetUnlocked(scoreBoundaryStar.m_star <= _levelProgress.ScoreStars);
			}
		}
		GameSession gameSession = GameUtils.GetGameSession();
		if (!(gameSession != null))
		{
			return;
		}
		GameProgress progress = gameSession.Progress;
		if (progress != null)
		{
			uIStar4.gameObject.SetActive(_levelProgress.NGPEnabled);
			if (_levelProgress.NGPEnabled)
			{
				uIStar4.Score = sceneVarient.FourStarScore;
			}
		}
	}

	private void SetUserScores(int _levelID)
	{
		int count = ClientUserSystem.m_Users.Count;
		GameSession gameSession = GameUtils.GetGameSession();
		HighScoreRepository highScoreRepository = gameSession.HighScoreRepository;
		for (int i = 0; i < m_userScoreUis.Length; i++)
		{
			UserScoreUI userScoreUI = m_userScoreUis[i];
			if (i < count)
			{
				User user = ClientUserSystem.m_Users._items[i];
				userScoreUI.SetName(user.DisplayName);
				if (user.Engagement != EngagementSlot.One || user.Split == User.SplitStatus.SplitPadGuest)
				{
					userScoreUI.SetScoreText(m_noScoreText);
				}
				else
				{
					LevelConfigBase levelConfig = m_sceneData.SceneVarients[0].LevelConfig;
					GameProgress.HighScores.Score score = null;
					if (highScoreRepository.GetScore(user.Machine, _levelID, ref score))
					{
						switch (gameSession.GameModeKind)
						{
						case Kind.Campaign:
							if (score.iHighScore != 65535 && score.iHighScore != int.MinValue)
							{
								if (levelConfig.GetType() == typeof(HordeLevelConfig))
								{
									int requiredBitCount = GameUtils.GetRequiredBitCount(65535);
									float num = FloatUtils.FromUnorm(score.iHighScore, requiredBitCount);
									userScoreUI.SetScoreText(string.Format("{0:P0}", num));
								}
								else
								{
									userScoreUI.SetScoreText(FormatScore(score.iHighScore, ScoreFormat.Number));
								}
							}
							else
							{
								userScoreUI.SetScoreText(m_noScoreText);
							}
							break;
						case Kind.Survival:
							if (score.iSurvivalModeTime != 0)
							{
								userScoreUI.SetScoreText(FormatScore(score.iSurvivalModeTime, ScoreFormat.Time));
							}
							else
							{
								userScoreUI.SetScoreText(m_noScoreText);
							}
							break;
						}
					}
					else
					{
						userScoreUI.SetScoreText(m_noScoreText);
					}
				}
			}
			userScoreUI.SetActive(i < count);
		}
	}

	protected virtual string FormatScore(int _score, ScoreFormat _format)
	{
		switch (_format)
		{
		case ScoreFormat.Number:
			return _score.ToString();
		case ScoreFormat.Time:
			return _score.ToTimeString();
		default:
			return string.Empty;
		}
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

	public void SetState(State _state)
	{
		m_animator.SetInteger(m_iPurchaseState, (int)_state);
	}
}
