using System;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class LevelTimerUIController : DisplayTimeUIController
{
	[SerializeField]
	private int m_alertInterval = 30;

	[SerializeField]
	private int m_pulseStart = 5;

	private ProgressBarUI m_ProgressBar;

	private Animator m_animator;

	private static readonly int m_iAlert = Animator.StringToHash("Alert");

	private static readonly int m_iPulse = Animator.StringToHash("Pulse");

	private DataStore m_dataStore;

	private static readonly DataStore.Id k_timeUpdatedId = new DataStore.Id("time.updated");

	public override int Value
	{
		get
		{
			return base.Value;
		}
		set
		{
			KitchenLevelConfigBase kitchenLevelConfigBase = GameUtils.GetLevelConfig() as KitchenLevelConfigBase;
			float timeLimit = kitchenLevelConfigBase.GetTimeLimit();
			if (value != Value && m_pulseStart != 0 && value <= m_pulseStart && m_animator != null && value > 0)
			{
				m_animator.SetTrigger(m_iPulse);
				GameUtils.TriggerAudio(GameOneShotAudioTag.LevelTimerBeep, base.gameObject.layer);
			}
			else if (value != Value && (float)value != timeLimit && m_alertInterval != 0 && value % m_alertInterval == 0 && m_animator != null)
			{
				m_animator.SetTrigger(m_iAlert);
			}
			if (m_ProgressBar != null)
			{
				m_ProgressBar.Value = (float)value / timeLimit;
			}
			base.Value = value;
		}
	}

	protected override void Awake()
	{
		m_animator = base.gameObject.RequireComponentRecursive<Animator>();
		m_ProgressBar = base.gameObject.RequestComponentRecursive<ProgressBarUI>();
		m_dataStore = GameUtils.RequireManager<DataStore>();
		m_dataStore.Register(k_timeUpdatedId, OnTimeUpdatedNotification);
		base.Awake();
	}

	private void OnDestroy()
	{
		if (m_dataStore != null)
		{
			m_dataStore.Unregister(k_timeUpdatedId, OnTimeUpdatedNotification);
		}
	}

	private void OnTimeUpdatedNotification(DataStore.Id id, object data)
	{
		Value = Convert.ToInt32(data);
	}
}
