using System;
using Team17.Online;
using UnityEngine;

public class SurvivalModeLevelTimerUIController : UIControllerBase
{
	[Header("Timers")]
	[SerializeField]
	private DisplayTimeUIController m_roundTimerController;

	[SerializeField]
	private DisplayTimeUIController m_survivalTimerController;

	[SerializeField]
	private T17Text m_survivalTimerText;

	[Header("Floating Text")]
	[SerializeField]
	[AssignResource("NewRecordFloatingText", Editorbility.Editable)]
	private GameObject m_newRecordFloatingTextPrefab;

	[SerializeField]
	private Color m_newRecordTextColour = new Color(1f, 0.682f, 0f, 1f);

	[SerializeField]
	[AssignResource("AddPointsFloatingNumberUI", Editorbility.Editable)]
	private GameObject m_addTimeFloatingTextPrefab;

	[SerializeField]
	[AssignResource("RemovePointsFloatingNumberUI", Editorbility.Editable)]
	private GameObject m_removeTimeFloatingTextPrefab;

	[SerializeField]
	private Vector2 m_floatingTextOffset = new Vector2(0f, 0f);

	private DataStore m_dataStore;

	private static readonly DataStore.Id k_timeAddedId = new DataStore.Id("time.added");

	private static readonly DataStore.Id k_timeUpdatedId = new DataStore.Id("time.updated");

	private static readonly DataStore.Id k_timeSurvivedId = new DataStore.Id("time.survived");

	private bool m_newSurvivalTimeRecord;

	private int m_survivalTimeRecord;

	protected void Awake()
	{
		m_dataStore = GameUtils.RequireManager<DataStore>();
		m_dataStore.Register(k_timeAddedId, OnTimeAddedNotification);
		m_dataStore.Register(k_timeUpdatedId, OnTimeUpdatedNotification);
		m_dataStore.Register(k_timeSurvivedId, OnTimeSurvivedNotification);
		GameSession gameSession = GameUtils.GetGameSession();
		HighScoreRepository highScoreRepository = gameSession.HighScoreRepository;
		GameProgress.HighScores.Score score = new GameProgress.HighScores.Score();
		if (highScoreRepository != null && highScoreRepository.GetScore(ClientUserSystem.s_LocalMachineId, GameUtils.GetLevelID(), ref score))
		{
			m_survivalTimeRecord = score.iSurvivalModeTime;
		}
	}

	protected void OnDestroy()
	{
		if (m_dataStore != null)
		{
			m_dataStore.Unregister(k_timeAddedId, OnTimeAddedNotification);
			m_dataStore.Unregister(k_timeUpdatedId, OnTimeUpdatedNotification);
			m_dataStore.Unregister(k_timeSurvivedId, OnTimeSurvivedNotification);
		}
	}

	private void OnTimeAddedNotification(DataStore.Id id, object data)
	{
		int num = Convert.ToInt32(data);
		GameObject obj = GameUtils.InstantiateUIControllerOnScalingHUDCanvas((num <= 0) ? m_removeTimeFloatingTextPrefab : m_addTimeFloatingTextPrefab);
		RectTransform rectTransform = (RectTransform)base.transform;
		RectTransformExtension rectTransformExtension = obj.RequireComponent<RectTransformExtension>();
		Vector2 anchorOffset = m_floatingTextOffset.MultipliedBy(rectTransform.anchorMin + rectTransform.anchorMax);
		rectTransformExtension.AnchorOffset = anchorOffset;
		obj.RequireComponent<DisplayIntUIController>().Value = Math.Abs(num);
	}

	private void OnTimeUpdatedNotification(DataStore.Id id, object data)
	{
		m_roundTimerController.Value = Convert.ToInt32(data);
	}

	private void OnTimeSurvivedNotification(DataStore.Id id, object data)
	{
		int num = Convert.ToInt32(data);
		m_survivalTimerController.Value = num;
		if (!m_newSurvivalTimeRecord && m_survivalTimeRecord != 0 && num > m_survivalTimeRecord)
		{
			m_newSurvivalTimeRecord = true;
			m_survivalTimeRecord = num;
			m_survivalTimerText.color = m_newRecordTextColour;
			GameObject obj = GameUtils.InstantiateUIControllerOnScalingHUDCanvas(m_newRecordFloatingTextPrefab);
			RectTransform rectTransform = (RectTransform)base.transform;
			RectTransformExtension rectTransformExtension = obj.RequireComponent<RectTransformExtension>();
			Vector2 anchorOffset = m_floatingTextOffset.MultipliedBy(rectTransform.anchorMin + rectTransform.anchorMax);
			rectTransformExtension.AnchorOffset = anchorOffset;
		}
	}
}
