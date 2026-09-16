using System.Diagnostics;
using UnityEngine;

public static class DebugUtils
{
	public static DebugDrawManager debugDrawManager;

	[Conditional("DEBUG")]
	public static void Assert(bool condition)
	{
		if (UnityEngine.Debug.isDebugBuild && !condition)
		{
			UnityEngine.Debug.Log("Assert Failed");
			throw new UnityException();
		}
	}

	[Conditional("DEBUG")]
	public static void Assert(bool condition, string msg)
	{
		if (UnityEngine.Debug.isDebugBuild && !condition)
		{
			UnityEngine.Debug.Log("Assert Failed: " + msg);
			throw new UnityException(msg);
		}
	}

	[Conditional("DEBUG")]
	public static void AssertPopup(bool condition, string msg)
	{
		if (UnityEngine.Debug.isDebugBuild && !condition)
		{
			UnityEngine.Debug.Log("Assert Failed: " + msg);
		}
	}

	[Conditional("DEBUG")]
	public static void Unreachable(string msg)
	{
		if (UnityEngine.Debug.isDebugBuild)
		{
			UnityEngine.Debug.Log("Unreachable: " + msg);
			throw new UnityException(msg);
		}
	}

	[Conditional("DEBUG")]
	public static void Error(string msg)
	{
		if (UnityEngine.Debug.isDebugBuild)
		{
			throw new UnityException(msg);
		}
	}

	public static void LogPrint(string _text, Color _color)
	{
		if ((bool)debugDrawManager)
		{
			debugDrawManager.AddLogText(_text, _color);
		}
	}

	[Conditional("DEBUG")]
	public static void Print(string _text, Vector2 _pos)
	{
	}

	[Conditional("DEBUG")]
	public static void Print(string _text, Vector2 _pos, Color _color)
	{
	}

	[Conditional("DEBUG")]
	public static void Print(string _text, Vector2 _pos, Color _color, bool _centred)
	{
	}

	[Conditional("DEBUG")]
	public static void Print(string _text, Vector2 _pos, Color _color, bool _centred, float _lifeTime)
	{
	}

	[Conditional("DEBUG")]
	public static void Print(string _text, Vector2 _pos, Color _color, bool _centred, float _lifeTime, int _fontSize)
	{
		if (UnityEngine.Debug.isDebugBuild && (bool)debugDrawManager)
		{
			DebugDrawManager.TextRequest2d textRequest = default(DebugDrawManager.TextRequest2d);
			textRequest.m_contents = _text;
			textRequest.m_position = _pos;
			textRequest.m_color = _color;
			textRequest.m_centred = _centred;
			textRequest.m_lifeTime = _lifeTime;
			textRequest.m_fontSize = _fontSize;
			debugDrawManager.AddText(textRequest);
		}
	}

	[Conditional("DEBUG")]
	public static void Print(string _text, Vector3 _pos)
	{
	}

	[Conditional("DEBUG")]
	public static void Print(string _text, Vector3 _pos, Color _color)
	{
	}

	[Conditional("DEBUG")]
	public static void Print(string _text, Vector3 _pos, Color _color, bool _centred)
	{
	}

	[Conditional("DEBUG")]
	public static void Print(string _text, Vector3 _pos, Color _color, bool _centred, float _lifeTime)
	{
	}

	[Conditional("DEBUG")]
	public static void Print(string _text, Vector3 _pos, Color _color, bool _centred, float _lifeTime, int _fontSize)
	{
		if (UnityEngine.Debug.isDebugBuild && (bool)debugDrawManager)
		{
			DebugDrawManager.TextRequest3d textRequest = default(DebugDrawManager.TextRequest3d);
			textRequest.m_contents = _text;
			textRequest.m_position = _pos;
			textRequest.m_color = _color;
			textRequest.m_centred = _centred;
			textRequest.m_lifeTime = _lifeTime;
			textRequest.m_fontSize = _fontSize;
			debugDrawManager.AddText(textRequest);
		}
	}

	[Conditional("DEBUG")]
	public static void DrawLabelFlag(string _text, Vector3 _pos)
	{
	}

	[Conditional("DEBUG")]
	public static void DrawCross(Vector3 _pos, float _crossSize)
	{
		if (UnityEngine.Debug.isDebugBuild)
		{
			Camera main = Camera.main;
			if (main != null)
			{
				Vector3 right = main.transform.right;
				Vector3 up = main.transform.up;
				Vector3 vector = right * _crossSize + up * _crossSize;
				Vector3 vector2 = right * _crossSize - up * _crossSize;
				Color white = Color.white;
				UnityEngine.Debug.DrawLine(_pos + vector, _pos - vector, white);
				UnityEngine.Debug.DrawLine(_pos + vector2, _pos - vector2, white);
			}
		}
	}

	[Conditional("DEBUG")]
	public static void DrawLabelFlag(string _text, Vector3 _pos, Color _color)
	{
	}

	[Conditional("DEBUG")]
	public static void DrawLabelFlag(string _text, Vector3 _pos, Color _color, Vector3 _offset)
	{
		if (UnityEngine.Debug.isDebugBuild)
		{
			UnityEngine.Debug.DrawLine(_pos, _pos + _offset, _color);
		}
	}

	[Conditional("DEBUG")]
	public static void DrawShape(PrimitiveType _type, Vector3 _pos, Vector3 _scale, Color _colour, Quaternion _rotation, float _duration = 1f)
	{
		GameObject gameObject = GameObject.CreatePrimitive(_type);
		gameObject.name = "Debug_DrawShape";
		gameObject.transform.position = _pos;
		gameObject.transform.localScale = _scale;
		gameObject.transform.rotation = _rotation;
		Collider[] array = gameObject.RequestComponents<Collider>();
		for (int num = array.Length - 1; num >= 0; num--)
		{
			Object.DestroyImmediate(array[num]);
		}
		Renderer renderer = gameObject.RequestComponent<Renderer>();
		if (renderer != null)
		{
			renderer.material.color = _colour;
		}
		Object.Destroy(gameObject, _duration);
	}

	[Conditional("DEBUG")]
	public static void Break(bool _condition)
	{
		if (_condition)
		{
			int num = 0;
		}
	}

	[Conditional("DEV_LOGGING")]
	public static void Log(object msg)
	{
		UnityEngine.Debug.Log(msg);
	}

	[Conditional("DEBUG")]
	public static void Warn(object msg)
	{
		UnityEngine.Debug.LogWarning(msg);
	}

	[Conditional("DEV_LOGGING")]
	public static void LogError(object msg)
	{
		UnityEngine.Debug.LogError(msg);
	}
}
