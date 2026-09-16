using UnityEngine;

public class ProgressUIController : HoverIconUIController
{
	[SerializeField]
	private ProgressBarUI m_progressBar;

	[SerializeField]
	[HideInInspectorTest("m_autoHide", true)]
	private float m_hideAftertime = 1f;

	[SerializeField]
	private bool m_autoHide = true;

	private float m_timer;

	public bool AutoHide
	{
		get
		{
			return m_autoHide;
		}
		set
		{
			m_autoHide = value;
		}
	}

	protected override void Awake()
	{
		base.Awake();
		m_progressBar.gameObject.SetActive(false);
	}

	public void SetProgress(float _value)
	{
		m_progressBar.SetValue(_value);
		m_progressBar.gameObject.SetActive(true);
		m_timer = m_hideAftertime;
	}

	public override void LateUpdate()
	{
		base.LateUpdate();
		float timer = m_timer;
		m_timer -= TimeManager.GetDeltaTime(base.gameObject);
		if (m_autoHide && timer >= 0f && m_timer < 0f)
		{
			m_progressBar.gameObject.SetActive(false);
		}
	}
}
