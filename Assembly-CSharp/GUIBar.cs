using UnityEngine;

[AddComponentMenu("Scripts/Game/GUI/GUIBar")]
public class GUIBar : MonoBehaviour
{
	[SerializeField]
	[Range(0f, 1f)]
	private float m_value;

	public GUIBarWidgetConfig m_guiBarConfig = new GUIBarWidgetConfig();

	private GUIBarWidget m_guiBar;

	private GUIStyle m_blockColourStyle;

	private float m_croppedSize = 1f;

	protected void Awake()
	{
		SetGUIBarWidgetConfig(m_guiBarConfig);
	}

	public void SetGUIBarWidgetConfig(GUIBarWidgetConfig _guiBarConfig)
	{
		m_guiBarConfig = _guiBarConfig;
		if (!m_guiBarConfig.m_transform)
		{
			m_guiBarConfig.m_transform = base.transform;
		}
		m_guiBar = new GUIBarWidget(m_guiBarConfig);
	}

	public virtual void SetValue(float _value)
	{
		m_value = _value;
		m_guiBar.SetValue(m_value);
	}

	public virtual float GetValue()
	{
		return m_value;
	}

	public void SetCroppedWidth(float _cropWidth)
	{
		m_guiBar.SetCroppedWidth(_cropWidth);
	}

	private void OnGUI()
	{
		m_guiBar.SetValue(m_value);
		m_guiBar.Draw();
	}
}
