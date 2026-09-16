using UnityEngine;

[AddComponentMenu("Scripts/Game/GUI/WarningGUIBar")]
public class WarningGUIBar : GUIBar
{
	[SerializeField]
	private Color m_flashBorderColor = Color.red;

	[SerializeField]
	private Color m_flashFillColor = Color.red;

	[SerializeField]
	private Color m_burningFillColor = Color.red;

	[SerializeField]
	private float m_flashTime = 1f;

	private Color m_preFlashBorderColor = Color.black;

	private Color m_preFlashFillColor = Color.black;

	public Color m_preFlashEmptyColor = Color.black;

	private float m_flashTimer;

	private bool m_flashing;

	public void StartWarning()
	{
		if (!m_flashing)
		{
			m_flashTimer = 0f;
			m_flashing = true;
		}
	}

	public void StopWarning()
	{
		m_flashing = false;
	}

	public override void SetValue(float _value)
	{
		if ((double)_value <= 1.0)
		{
			base.SetValue(_value);
		}
		else
		{
			base.SetValue(_value - 1f);
		}
	}

	private new void Awake()
	{
		base.Awake();
		m_preFlashBorderColor = m_guiBarConfig.m_borderColor;
		m_preFlashFillColor = m_guiBarConfig.m_fillColor;
		m_preFlashEmptyColor = m_guiBarConfig.m_emptyColor;
	}

	private void Update()
	{
		if (m_flashing)
		{
			m_guiBarConfig.m_borderColor = Color.Lerp(m_flashBorderColor, m_preFlashBorderColor, m_flashTimer / m_flashTime);
			m_guiBarConfig.m_emptyColor = Color.Lerp(m_flashFillColor, m_preFlashFillColor, m_flashTimer / m_flashTime);
			m_guiBarConfig.m_fillColor = m_burningFillColor;
			float deltaTime = TimeManager.GetDeltaTime(base.gameObject);
			m_flashTimer = (m_flashTimer + deltaTime) % m_flashTime;
		}
		else
		{
			m_flashTimer = 0f;
			m_guiBarConfig.m_borderColor = m_preFlashBorderColor;
			m_guiBarConfig.m_fillColor = m_preFlashFillColor;
			m_guiBarConfig.m_emptyColor = m_preFlashEmptyColor;
		}
	}
}
