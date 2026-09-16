using UnityEngine;

public class ExceptionNotificationHandler : IExceptionDisplayer
{
	private Rect m_rect;

	private GUIStyle m_style;

	private string m_displayString = string.Empty;

	public void Initialize()
	{
		float num = (float)Screen.width / 2.5f;
		m_rect = new Rect((float)Screen.width - num, 0f, num, 0f);
		m_style = new GUIStyle();
		m_style.alignment = TextAnchor.LowerRight;
		m_style.fontSize = (int)((float)Screen.height * 0.025f);
		m_style.normal.textColor = new Color(1f, 0f, 0f, 1f);
		m_style.wordWrap = true;
	}

	public void OnGUI()
	{
		if (!string.IsNullOrEmpty(m_displayString))
		{
			GUI.Box(m_rect, string.Empty);
			GUI.Label(m_rect, m_displayString, m_style);
		}
	}

	public void Display(string exceptionString, string stackTrace, bool bJustOccured)
	{
		m_displayString = exceptionString + "\n" + stackTrace;
		float num = m_style.CalcHeight(new GUIContent(m_displayString), m_rect.width);
		m_rect.height = num;
		m_rect.y = (float)Screen.height - num;
	}
}
