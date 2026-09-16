using UnityEngine;

public class ProgressUI : MonoBehaviour
{
	private GUIBar m_guiBar;

	private float m_hideAfterTime;

	public void SetProgress(float _value)
	{
		m_hideAfterTime = 1f;
		m_guiBar.enabled = true;
		m_guiBar.SetValue(_value);
	}

	private void Awake()
	{
		m_guiBar = base.gameObject.AddComponent<GUIBar>();
		GUIBarWidgetConfig gUIBarWidgetConfig = new GUIBarWidgetConfig();
		gUIBarWidgetConfig.m_width = 40f;
		gUIBarWidgetConfig.m_height = 10f;
		gUIBarWidgetConfig.m_border = 1f;
		gUIBarWidgetConfig.m_offset = new Vector2(0f, 25f);
		gUIBarWidgetConfig.m_fillColor = new Color(0.11764706f, 0.99215686f, 0f);
		gUIBarWidgetConfig.m_emptyColor = Color.black;
		gUIBarWidgetConfig.m_borderColor = new Color(8f / 51f, 0.42745098f, 0f);
		m_guiBar.SetGUIBarWidgetConfig(gUIBarWidgetConfig);
		m_guiBar.enabled = false;
	}

	private void Update()
	{
		float hideAfterTime = m_hideAfterTime;
		m_hideAfterTime -= TimeManager.GetDeltaTime(base.gameObject);
		if (hideAfterTime >= 0f && m_hideAfterTime < 0f)
		{
			m_guiBar.enabled = false;
		}
	}
}
