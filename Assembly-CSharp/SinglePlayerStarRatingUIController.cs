using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SinglePlayerStarRatingUIController : StarRatingUIController
{
	public class ScoreData
	{
		public int StarRating;

		public int LevelTime;
	}

	[SerializeField]
	private DisplayTimeUIController m_levelTimer;

	[SerializeField]
	private DisplayTimeUIController m_bestTime;

	[SerializeField]
	private Text m_bestTimeLabel;

	public override void SetScoreData(object _scoreData)
	{
		ScoreData scoreData = _scoreData as ScoreData;
		SetScoreData(scoreData.StarRating);
		m_levelTimer.Value = scoreData.LevelTime;
		GameSession gameSession = GameUtils.GetGameSession();
		GameProgress.GameProgressData.LevelProgress progress = gameSession.Progress.GetProgress(SceneManager.GetActiveScene().name);
		if (progress.Completed)
		{
			m_bestTime.Value = Mathf.Min(-progress.HighScore, scoreData.LevelTime);
			return;
		}
		m_bestTime.gameObject.SetActive(false);
		m_bestTimeLabel.gameObject.SetActive(false);
	}
}
