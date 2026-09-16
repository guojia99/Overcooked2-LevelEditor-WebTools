using UnityEngine;

public class GUIBarWidget
{
	public GUIBarWidgetConfig m_config;

	private float m_value;

	private GUIStyle m_blockColourStyle;

	private float m_croppedSize = 1f;

	public GUIBarWidget(GUIBarWidgetConfig _config)
	{
		m_config = _config;
		Texture2D texture2D = new Texture2D(1, 1);
		texture2D.SetPixel(0, 0, Color.white);
		texture2D.wrapMode = TextureWrapMode.Repeat;
		texture2D.Apply();
		m_blockColourStyle = new GUIStyle();
		m_blockColourStyle.normal.background = texture2D;
	}

	public virtual void SetValue(float _value)
	{
		m_value = Mathf.Clamp01(_value);
	}

	public float GetValue()
	{
		return m_value;
	}

	public void SetCroppedWidth(float _cropWidth)
	{
		m_croppedSize = _cropWidth / m_config.m_width;
	}

	private Rect GetGUIRect(Camera _camera)
	{
		Transform transform = m_config.m_transform;
		if (transform != null)
		{
			Vector3 vector = _camera.WorldToViewportPoint(transform.position);
			vector.y = 1f - vector.y;
			float width = m_config.m_width;
			float height = m_config.m_height;
			Vector2 offset = m_config.m_offset;
			float x = _camera.pixelRect.width * vector.x - 0.5f * width;
			float y = _camera.pixelRect.height * vector.y - 0.5f * height;
			Vector2 vector2 = new Vector2(x, y) + offset;
			return new Rect(vector2.x, vector2.y, width, height);
		}
		float width2 = m_config.m_width;
		float height2 = m_config.m_height;
		Vector2 offset2 = m_config.m_offset;
		return new Rect(offset2.x, offset2.y, width2, height2);
	}

	public void Draw(Vector2? _offset = null, float _widthScale = 1f, float _heightScale = 1f)
	{
		Vector2 vector = ((!_offset.HasValue) ? Vector2.zero : _offset.Value);
		Camera[] allCameras = Camera.allCameras;
		foreach (Camera camera in allCameras)
		{
			Rect position = new Rect(camera.pixelRect.x, (float)Screen.height - (camera.pixelRect.height + camera.pixelRect.y), camera.pixelRect.width, camera.pixelRect.height);
			GUI.BeginGroup(position);
			Rect gUIRect = GetGUIRect(camera);
			gUIRect.x = _widthScale * gUIRect.x + vector.x;
			gUIRect.y = _heightScale * gUIRect.y + vector.y;
			gUIRect.width *= _widthScale;
			gUIRect.height *= _heightScale;
			float num = gUIRect.width * m_croppedSize;
			float border = m_config.m_border;
			Rect position2 = new Rect(gUIRect.x - border, gUIRect.y - border, num + 2f * border, gUIRect.height + 2f * border);
			GUI.BeginGroup(position2);
			Texture2D image = null;
			Color color = GUI.color;
			GUI.color = m_config.m_borderColor;
			GUI.Box(new Rect(0f, 0f, position2.width, position2.height), image, m_blockColourStyle);
			Rect position3 = new Rect(border, border, num, gUIRect.height);
			GUI.BeginGroup(position3);
			GUI.color = m_config.m_emptyColor;
			GUI.Box(new Rect(gUIRect.width * m_value, 0f, position3.width, position3.height), image, m_blockColourStyle);
			GUI.color = m_config.m_fillColor;
			GUI.Box(new Rect(0f, 0f, gUIRect.width * m_value, position3.height), image, m_blockColourStyle);
			GUI.EndGroup();
			GUI.color = color;
			GUI.EndGroup();
			GUI.EndGroup();
		}
	}
}
