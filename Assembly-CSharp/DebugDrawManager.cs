using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Scripts/Core/Debug/DebugDrawManager")]
public class DebugDrawManager : MonoBehaviour
{
	public struct TextRequest2d
	{
		public string m_contents;

		public Vector2 m_position;

		public Color m_color;

		public bool m_centred;

		public float m_lifeTime;

		public int m_fontSize;
	}

	public struct TextRequest3d
	{
		public string m_contents;

		public Vector3 m_position;

		public Color m_color;

		public bool m_centred;

		public float m_lifeTime;

		public int m_fontSize;
	}

	private class Log
	{
		private struct StringEntry
		{
			public string Text;

			public Color Color;
		}

		private List<StringEntry> m_text = new List<StringEntry>();

		public void Add(string _text, Color _color)
		{
			StringEntry item = new StringEntry
			{
				Text = _text,
				Color = _color
			};
			m_text.Add(item);
		}

		public void Draw()
		{
			Camera main = Camera.main;
			if (main == null)
			{
				return;
			}
			Vector2 vector = new Vector2(0f, main.pixelHeight);
			for (int num = m_text.Count - 1; num >= 0; num--)
			{
				if (vector.y < 0f)
				{
					m_text.RemoveAt(num);
				}
				else
				{
					StringEntry stringEntry = m_text[num];
					GUIContent content = new GUIContent(stringEntry.Text);
					GUIStyle label = GUI.skin.label;
					label.fontSize = 14;
					Vector2 vector2 = label.CalcSize(content);
					GUI.color = stringEntry.Color;
					GUI.Label(new Rect(vector.x, vector.y, vector2.x, vector2.y), content, label);
					vector.y -= vector2.y;
				}
			}
		}
	}

	private List<TextRequest2d> m_textRequests2d = new List<TextRequest2d>();

	private List<TextRequest3d> m_textRequests3d = new List<TextRequest3d>();

	private Log m_onScreenLog = new Log();

	public void AddLogText(string _text, Color _color)
	{
		m_onScreenLog.Add(_text, _color);
	}

	public void AddText(TextRequest2d _textRequest)
	{
		m_textRequests2d.Add(_textRequest);
	}

	public void AddText(TextRequest3d _textRequest)
	{
		m_textRequests3d.Add(_textRequest);
	}

	private void Awake()
	{
		DebugUtils.debugDrawManager = this;
	}

	private void Update()
	{
		for (int i = 0; i < m_textRequests2d.Count; i++)
		{
			TextRequest2d value = m_textRequests2d[i];
			value.m_lifeTime -= TimeManager.GetDeltaTime(base.gameObject);
			m_textRequests2d[i] = value;
		}
		for (int j = 0; j < m_textRequests3d.Count; j++)
		{
			TextRequest3d value2 = m_textRequests3d[j];
			value2.m_lifeTime -= TimeManager.GetDeltaTime(base.gameObject);
			m_textRequests3d[j] = value2;
		}
	}

	private void Draw(TextRequest2d text)
	{
		GUIContent content = new GUIContent(text.m_contents);
		GUIStyle label = GUI.skin.label;
		label.fontSize = text.m_fontSize;
		Vector2 vector = label.CalcSize(content);
		GUI.color = text.m_color;
		if (text.m_centred)
		{
			GUI.Label(new Rect(text.m_position.x - 0.5f * vector.x, text.m_position.y - 0.5f * vector.y, vector.x, vector.y), content, label);
		}
		else
		{
			GUI.Label(new Rect(text.m_position.x, text.m_position.y, vector.x, vector.y), content, label);
		}
	}

	private void Draw(TextRequest3d text)
	{
		GUIContent content = new GUIContent(text.m_contents);
		GUIStyle label = GUI.skin.label;
		label.fontSize = text.m_fontSize;
		Vector2 vector = label.CalcSize(content);
		Vector2 vector2 = Camera.main.WorldToScreenPoint(text.m_position);
		vector2.y = (float)Camera.main.pixelHeight - vector2.y;
		GUI.color = text.m_color;
		if (text.m_centred)
		{
			GUI.Label(new Rect(vector2.x - 0.5f * vector.x, vector2.y - 0.5f * vector.y, vector.x, vector.y), content, label);
		}
		else
		{
			GUI.Label(new Rect(vector2.x, vector2.y, vector.x, vector.y), content, label);
		}
	}

	private void OnGUI()
	{
		foreach (TextRequest2d item in m_textRequests2d)
		{
			Draw(item);
		}
		foreach (TextRequest3d item2 in m_textRequests3d)
		{
			Draw(item2);
		}
		m_onScreenLog.Draw();
		GUI.color = Color.white;
		Event current3 = Event.current;
		if (current3.GetTypeForControl(0) == EventType.Repaint)
		{
			m_textRequests2d.RemoveAll((TextRequest2d x) => x.m_lifeTime <= 0f);
			m_textRequests3d.RemoveAll((TextRequest3d x) => x.m_lifeTime <= 0f);
		}
	}
}
