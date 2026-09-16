using UnityEngine;

[AddComponentMenu("Scripts/Core/Components/Trigger Timer On Animator")]
public class TriggerTimerOnAnimator : MonoBehaviour
{
	[SerializeField]
	private string m_startTimerTrigger;

	[SerializeField]
	private string m_onCompleteTrigger;

	[SerializeField]
	private Animator m_targetAnimator;

	[SerializeField]
	private float m_time;

	[SerializeField]
	private bool m_startTimerOnAwake;

	[SerializeField]
	private bool m_sendCompleteOnAwake;

	private float m_timer;

	private int m_onCompleteTriggerHash;

	private void Awake()
	{
		if (m_startTimerOnAwake)
		{
			m_timer = m_time;
		}
		m_onCompleteTriggerHash = Animator.StringToHash(m_onCompleteTrigger);
	}

	private void OnTrigger(string _trigger)
	{
		if (m_startTimerTrigger == _trigger)
		{
			m_timer = m_time;
		}
	}

	private void Update()
	{
		if (m_sendCompleteOnAwake)
		{
			if (m_targetAnimator != null)
			{
				m_targetAnimator.SetTrigger(m_onCompleteTriggerHash);
			}
			m_sendCompleteOnAwake = false;
		}
		if (m_timer > 0f)
		{
			m_timer -= TimeManager.GetDeltaTime(base.gameObject);
			if (m_timer <= 0f && m_targetAnimator != null)
			{
				m_targetAnimator.SetTrigger(m_onCompleteTriggerHash);
			}
		}
	}
}
