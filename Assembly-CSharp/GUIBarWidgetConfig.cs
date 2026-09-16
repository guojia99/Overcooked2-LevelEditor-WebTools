using System;
using UnityEngine;

[Serializable]
public class GUIBarWidgetConfig
{
	public float m_width;

	public float m_height;

	public float m_border;

	public Transform m_transform;

	public Vector2 m_offset = new Vector2(0f, -64f);

	public Color m_fillColor = Color.white;

	public Color m_emptyColor = Color.black;

	public Color m_borderColor = Color.black;

	public GUIBarWidgetConfig DeepClone()
	{
		return MemberwiseClone() as GUIBarWidgetConfig;
	}
}
