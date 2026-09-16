using System;
using UnityEngine;

[Serializable]
public class GUIRect
{
	public Rect m_rect;

	public GUIAnchor m_anchor;

	public GUICoordSystem m_coordSystem;

	public GUIRect(Rect _rect, GUIAnchor _anchor, GUICoordSystem _coords)
	{
		m_rect = _rect;
		m_anchor = _anchor;
		m_coordSystem = _coords;
	}

	public GUIRect DeepCopy()
	{
		return MemberwiseClone() as GUIRect;
	}

	public Rect GetInPixels(Camera _camera)
	{
		Vector2 vector = GUIUtils.GUIToPixels(_camera, new Vector2(m_rect.x, m_rect.y), m_anchor, m_coordSystem);
		Vector2 vector2 = GUIUtils.GUIToPixels(_camera, new Vector2(m_rect.x + m_rect.width, m_rect.y + m_rect.height), m_anchor, m_coordSystem);
		float x = Mathf.Min(vector.x, vector2.x);
		float y = Mathf.Min(vector.y, vector2.y);
		float width = Mathf.Abs(vector.x - vector2.x);
		float height = Mathf.Abs(vector.y - vector2.y);
		return new Rect(x, y, width, height);
	}
}
