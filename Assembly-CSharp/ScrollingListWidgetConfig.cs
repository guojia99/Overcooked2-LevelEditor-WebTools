using System;
using UnityEngine;

[Serializable]
public class ScrollingListWidgetConfig
{
	public string[] m_names = new string[0];

	public GUIRect m_displayArea = new GUIRect(new Rect(0.2f, 0.3f, 0.6f, 0.6f), GUIAnchor.TopLeft, GUICoordSystem.Normalised);

	public float m_textSize = 0.1f;

	public Font m_font;

	public TextAnchor m_textAnchor = TextAnchor.MiddleRight;

	public Color m_selectedText = Color.black;

	public Color m_regularText = Color.black;
}
