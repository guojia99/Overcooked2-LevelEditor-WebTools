using UnityEngine;

public class HeatValueUIController : HoverIconUIController
{
	[SerializeField]
	private ProgressGaugeUI m_progressBar;

	[SerializeField]
	private float m_progressLerpTime = 0.1f;

	[SerializeField]
	private bool m_autoHide = true;

	[SerializeField]
	[HideInInspectorTest("m_autoHide", true)]
	private float m_autoHideTime = 1f;

	private float m_progress;

	private float m_hideTimer;

	private float m_lerpTimer;

	protected override void OnEnable()
	{
		base.OnEnable();
		if (m_autoHide && m_autoHideTime > 0f)
		{
			m_progressBar.gameObject.SetActive(false);
		}
	}

	public override void LateUpdate()
	{
		base.LateUpdate();
		if (m_autoHide && m_hideTimer > 0f)
		{
			m_hideTimer -= TimeManager.GetDeltaTime(base.gameObject);
			if (m_hideTimer <= 0f)
			{
				m_progressBar.gameObject.SetActive(false);
			}
		}
		if (m_lerpTimer > 0f)
		{
			m_lerpTimer -= TimeManager.GetDeltaTime(base.gameObject);
			m_lerpTimer = Mathf.Max(m_lerpTimer, 0f);
			float value = Mathf.Lerp(m_progressBar.Value, m_progress, 1f - m_lerpTimer / m_progressLerpTime);
			m_progressBar.SetValue(value);
		}
	}

	public void SetProgress(float _value)
	{
		float progress = m_progress;
		if (progress != _value)
		{
			m_progress = _value;
			m_lerpTimer = m_progressLerpTime;
			if (m_autoHide && m_autoHideTime > 0f)
			{
				m_progressBar.gameObject.SetActive(true);
				m_hideTimer = m_autoHideTime;
			}
		}
	}
}
