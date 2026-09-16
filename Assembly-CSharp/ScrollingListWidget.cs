using UnityEngine;

public class ScrollingListWidget
{
	private ScrollingListWidgetConfig m_config;

	private Camera m_camera;

	private int m_anchor;

	private int m_selected;

	public ScrollingListWidget(ScrollingListWidgetConfig _config)
	{
		m_config = _config;
		m_camera = Camera.main;
	}

	public void SetNames(string[] _names)
	{
		m_config.m_names = _names;
	}

	public void MoveDown()
	{
		if (m_selected + 1 < m_config.m_names.Length)
		{
			m_selected++;
			if (m_selected >= m_anchor + (int)(m_config.m_displayArea.m_rect.height / m_config.m_textSize))
			{
				m_anchor++;
			}
		}
	}

	public void MoveUp()
	{
		if (m_selected > 0)
		{
			m_selected--;
			if (m_selected < m_anchor)
			{
				m_anchor--;
			}
		}
	}

	public int GetSelection()
	{
		return m_selected;
	}

	public void Draw(Vector3? _position)
	{
		GUIRect gUIRect = m_config.m_displayArea;
		if (_position.HasValue)
		{
			gUIRect = GUIUtils.ConvertToScreenSpace(gUIRect, Camera.main, _position.Value);
		}
		Rect inPixels = gUIRect.GetInPixels(Camera.main);
		GUIRect gUIRect2 = new GUIRect(new Rect(0f, 0f, m_config.m_displayArea.m_rect.width, m_config.m_textSize), m_config.m_displayArea.m_anchor, m_config.m_displayArea.m_coordSystem);
		Rect inPixels2 = gUIRect2.GetInPixels(Camera.main);
		GUIStyle textStyle = GUIUtils.GetTextStyle(inPixels2, m_config.m_textAnchor, m_config.m_font, 10);
		int num = (int)(m_config.m_displayArea.m_rect.height / m_config.m_textSize);
		GUI.BeginGroup(inPixels);
		for (int i = 0; i < num; i++)
		{
			int num2 = m_anchor + i;
			if (num2 < m_config.m_names.Length)
			{
				Rect position = new Rect(0f, (float)i * inPixels2.height, inPixels.width, inPixels2.height);
				GUI.color = ((num2 != m_selected) ? m_config.m_regularText : m_config.m_selectedText);
				GUI.Label(position, m_config.m_names[num2], textStyle);
			}
		}
		GUI.EndGroup();
	}
}
