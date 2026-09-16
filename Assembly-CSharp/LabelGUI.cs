using UnityEngine;

public class LabelGUI : MonoBehaviour
{
	[SerializeField]
	private GUIRect m_rect;

	[SerializeField]
	private Font m_font;

	[SerializeField]
	private string m_message;

	[SerializeField]
	private Color m_textColour = Color.white;

	[SerializeField]
	private bool m_screenSpace = true;

	public void SetText(string _text)
	{
		m_message = _text;
	}

	public string GetText()
	{
		return m_message;
	}

	public GUIRect GetGUIRect()
	{
		return m_rect;
	}

	public void SetGUIRect(GUIRect _rect)
	{
		m_rect = _rect;
	}

	private void OnGUI()
	{
		Rect rect = ((!m_screenSpace) ? GUIUtils.ToPixels(m_rect, Camera.main, base.transform.position) : GUIUtils.ToPixels(m_rect, Camera.main));
		GUIStyle textStyle = GUIUtils.GetTextStyle(rect, TextAnchor.MiddleCenter, m_font, m_message);
		Color color = GUI.color;
		GUI.color = m_textColour;
		GUI.Label(rect, m_message, textStyle);
		GUI.color = color;
	}
}
