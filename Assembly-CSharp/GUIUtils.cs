using UnityEngine;

internal class GUIUtils
{
	public static GUIRect ConvertToScreenSpace(GUIRect _guiRect, Camera _camera, Vector3 _worldPoint)
	{
		Vector3 input = _camera.WorldToViewportPoint(_worldPoint);
		input.y = 1f - input.y;
		Vector2 offset = NormalToGUI(_camera, input.XY(), _guiRect.m_anchor, _guiRect.m_coordSystem);
		return new GUIRect(_guiRect.m_rect.Added(offset), _guiRect.m_anchor, _guiRect.m_coordSystem);
	}

	public static Rect ToPixels(GUIRect _guiRect, Camera _camera)
	{
		return _guiRect.GetInPixels(_camera);
	}

	public static Rect ToPixels(GUIRect _guiRect, Camera _camera, Vector3 _worldPoint)
	{
		GUIRect gUIRect = ConvertToScreenSpace(_guiRect, _camera, _worldPoint);
		return gUIRect.GetInPixels(_camera);
	}

	public static Vector2 GUIToPixels(Camera _camera, Vector2 _pos, GUIAnchor _anchor, GUICoordSystem _coords)
	{
		Vector2 result = GUIToNormal(_camera, _pos, _anchor, _coords);
		result.x *= _camera.pixelWidth;
		result.y *= _camera.pixelHeight;
		return result;
	}

	public static Vector2 GUIToNormal(Camera _camera, Vector2 _pos, GUIAnchor _anchor, GUICoordSystem _coords)
	{
		_pos.x = XGUIToNormal(_camera, _pos.x, _coords);
		_pos.y = YGUIToNormal(_camera, _pos.y, _coords);
		switch (_anchor)
		{
		case GUIAnchor.TopLeft:
			return _pos;
		case GUIAnchor.TopRight:
			_pos.x = 1f - _pos.x;
			return _pos;
		case GUIAnchor.BottomLeft:
			_pos.y = 1f - _pos.y;
			return _pos;
		case GUIAnchor.BottomRight:
			_pos.x = 1f - _pos.x;
			_pos.y = 1f - _pos.y;
			return _pos;
		default:
			return Vector2.zero;
		}
	}

	public static Vector2 NormalToGUI(Camera _camera, Vector2 _pos, GUIAnchor _anchor, GUICoordSystem _coords)
	{
		switch (_anchor)
		{
		case GUIAnchor.TopRight:
			_pos.x = 1f - _pos.x;
			break;
		case GUIAnchor.BottomLeft:
			_pos.y = 1f - _pos.y;
			break;
		case GUIAnchor.BottomRight:
			_pos.x = 1f - _pos.x;
			_pos.y = 1f - _pos.y;
			break;
		}
		_pos.x = NormalToXGUI(_camera, _pos.x, _coords);
		_pos.y = NormalToYGUI(_camera, _pos.y, _coords);
		return _pos;
	}

	public static float XGUIToNormal(Camera _camera, float _length, GUICoordSystem _coords)
	{
		if (_coords == GUICoordSystem.X_0ToAspect)
		{
			return _length / _camera.aspect;
		}
		return _length;
	}

	public static float NormalToXGUI(Camera _camera, float _length, GUICoordSystem _coords)
	{
		if (_coords == GUICoordSystem.X_0ToAspect)
		{
			return _length * _camera.aspect;
		}
		return _length;
	}

	public static float YGUIToNormal(Camera _camera, float _length, GUICoordSystem _coords)
	{
		if (_coords == GUICoordSystem.Y_0ToInverseAspect)
		{
			return _length * _camera.aspect;
		}
		return _length;
	}

	public static float NormalToYGUI(Camera _camera, float _length, GUICoordSystem _coords)
	{
		if (_coords == GUICoordSystem.Y_0ToInverseAspect)
		{
			return _length / _camera.aspect;
		}
		return _length;
	}

	public static float LengthXGUIToPixels(Camera _camera, float _length, GUICoordSystem _coords)
	{
		return XGUIToNormal(_camera, _length, _coords) * (float)_camera.pixelWidth;
	}

	public static float LengthYGUIToPixels(Camera _camera, float _length, GUICoordSystem _coords)
	{
		return YGUIToNormal(_camera, _length, _coords) * (float)_camera.pixelHeight;
	}

	public static GUIStyle GetTextStyle(Rect _size, TextAnchor _alignment, Font _font, int _maxLetterCount)
	{
		string text = string.Empty;
		for (int i = 0; i < _maxLetterCount; i++)
		{
			text += ((i % 2 != 0) ? "y" : "t");
		}
		return GetTextStyle(_size, _alignment, _font, text);
	}

	public static GUIStyle GetTextStyle(Rect _size, TextAnchor _alignment, Font _font, string _message)
	{
		GUIStyle gUIStyle = new GUIStyle("label");
		gUIStyle.font = _font;
		gUIStyle.alignment = _alignment;
		gUIStyle.fontSize = (int)_size.height;
		Vector2 vector = gUIStyle.CalcSize(new GUIContent(_message));
		float num = Mathf.Min(_size.width / vector.x, _size.height / vector.y);
		gUIStyle.fontSize = (int)(num * (float)gUIStyle.fontSize);
		return gUIStyle;
	}

	public static void ShadowedLabel(Rect r, string s, GUIStyle style)
	{
		Color color = GUI.color;
		GUI.color = Color.black;
		GUI.Label(r, s, style);
		r.x -= 1f;
		r.y -= 1f;
		GUI.color = Color.white;
		GUI.Label(r, s, style);
		GUI.color = color;
	}
}
