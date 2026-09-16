using System.Collections.Generic;
using UnityEngine;

public class OnScreenDebugDisplay : MonoBehaviour
{
	private List<DebugDisplay> m_Displays;

	private GUIStyle m_GUIStyle;

	public void AddDisplay(DebugDisplay display)
	{
		if (display != null)
		{
			display.OnSetUp();
			m_Displays.Add(display);
		}
	}

	public void RemoveDisplay(DebugDisplay display)
	{
		if (display != null)
		{
			m_Displays.Remove(display);
		}
	}

	private void Awake()
	{
		m_Displays = new List<DebugDisplay>();
		m_GUIStyle = new GUIStyle();
		m_GUIStyle.alignment = TextAnchor.UpperRight;
		m_GUIStyle.fontSize = (int)((float)Screen.height * 0.03f);
		m_GUIStyle.normal.textColor = new Color(1f, 1f, 1f, 1f);
	}

	private void Update()
	{
		if (DebugManager.Instance.GetOption("On Screen Debug Text"))
		{
			for (int i = 0; i < m_Displays.Count; i++)
			{
				m_Displays[i].OnUpdate();
			}
		}
	}

	private void OnGUI()
	{
		if (DebugManager.Instance.GetOption("On Screen Debug Text"))
		{
			Rect rect = new Rect(0f, 0f, Screen.width, m_GUIStyle.fontSize);
			for (int i = 0; i < m_Displays.Count; i++)
			{
				m_Displays[i].OnDraw(ref rect, m_GUIStyle);
			}
		}
	}

	private void OnDestroy()
	{
		for (int i = 0; i < m_Displays.Count; i++)
		{
			if (m_Displays[i] != null)
			{
				m_Displays[i].OnDestroy();
			}
		}
	}
}
