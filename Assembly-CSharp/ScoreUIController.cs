using UnityEngine;

public class ScoreUIController : DisplayIntUIController
{
	[SerializeField]
	private TeamID m_team;

	[SerializeField]
	private Animator m_scoreAnimator;

	private static readonly int m_scoreAnimatorHash = Animator.StringToHash("Score");

	[Header("Floating Text")]
	[SerializeField]
	[AssignResource("AddPointsFloatingNumberUI", Editorbility.Editable)]
	private GameObject m_addPointsFloatingTextPrefab;

	[SerializeField]
	[AssignResource("RemovePointsFloatingNumberUI", Editorbility.Editable)]
	private GameObject m_removePointsFloatingTextPrefab;

	[SerializeField]
	private Vector2 m_floatingTextOffset = new Vector2(0.5f, 0.5f);

	[Header("Multiplier")]
	[SerializeField]
	private DisplayIntUIController m_multiplierUIController;

	[SerializeField]
	private T17Text m_multiplierText;

	[SerializeField]
	private Sprite[] m_barSprites;

	[SerializeField]
	private T17Image m_barImage;

	[SerializeField]
	private T17Image m_flameImage;

	private int m_totalScore;

	private DataStore m_dataStore;

	private static readonly DataStore.Id k_scoreTeamId = new DataStore.Id("score.team");

	protected override void Awake()
	{
		base.Awake();
		m_multiplierUIController.Value = 0;
		m_multiplierText.enabled = false;
		m_barImage.sprite = m_barSprites[0];
		m_dataStore = GameUtils.RequireManager<DataStore>();
		m_dataStore.Register(k_scoreTeamId, OnTeamScoreNotification);
	}

	private void OnDestroy()
	{
		if (m_dataStore != null)
		{
			m_dataStore.Unregister(k_scoreTeamId, OnTeamScoreNotification);
		}
	}

	private void OnTeamScoreNotification(DataStore.Id id, object data)
	{
		TeamScore teamScore = (TeamScore)data;
		ScoreUpdate(teamScore.m_team, teamScore.m_score);
	}

	public void ScoreUpdate(TeamID team, TeamMonitor.TeamScoreStats score)
	{
		if (team == m_team)
		{
			int num = (base.Value = score.GetTotalScore());
			m_multiplierText.enabled = score.TotalMultiplier > 0;
			m_multiplierUIController.Value = score.TotalMultiplier;
			int num2 = Mathf.Max(score.TotalMultiplier, 1);
			if (m_barSprites[num2 - 1] != null)
			{
				m_barImage.sprite = m_barSprites[num2 - 1];
			}
			m_flameImage.gameObject.SetActive(num2 == 4);
			int num3 = num - m_totalScore;
			if (num3 != 0 && base.isActiveAndEnabled)
			{
				GameObject obj = GameUtils.InstantiateUIControllerOnScalingHUDCanvas((num3 <= 0) ? m_removePointsFloatingTextPrefab : m_addPointsFloatingTextPrefab);
				RectTransform rectTransform = (RectTransform)base.transform;
				RectTransformExtension rectTransformExtension = obj.RequireComponent<RectTransformExtension>();
				Vector2 anchorOffset = m_floatingTextOffset.MultipliedBy(rectTransform.anchorMin + rectTransform.anchorMax);
				rectTransformExtension.AnchorOffset = anchorOffset;
				obj.RequireComponent<DisplayIntUIController>().Value = Mathf.Abs(num3);
				m_scoreAnimator.SetTrigger(m_scoreAnimatorHash);
			}
			m_totalScore = num;
		}
	}
}
