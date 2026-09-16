using System;
using UnityEngine;

[AddComponentMenu("Scripts/Core/Input/DebugInputDisplay")]
public class DebugInputDisplay : MonoBehaviour
{
	private enum InputProvider
	{
		DirectInput = 0,
		XInput = 1,
		NX = 2
	}

	[SerializeField]
	private bool m_displayDebug;

	[SerializeField]
	private int m_startIndex;

	[SerializeField]
	private InputProvider m_inputProvider;

	private void OnGUI()
	{
		if (!m_displayDebug)
		{
			return;
		}
		Rect rect = new Rect(0f, 0f, 100f, 22f);
		int num = ((m_inputProvider == InputProvider.DirectInput) ? 11 : ((m_inputProvider != InputProvider.NX) ? 4 : 9));
		for (int i = Mathf.Min(m_startIndex, num - 1); i < num; i++)
		{
			ControlPadInput.PadNum pad = (ControlPadInput.PadNum)i;
			if (m_inputProvider != InputProvider.NX)
			{
				PrintButtons<ControlPadInput.Button>(ControlPadInput.Button.Invalid, pad, ref rect);
				PrintValues<ControlPadInput.Value>(ControlPadInput.Value.Invalid, pad, ref rect);
			}
			rect.x += rect.width;
			rect.y = 0f;
		}
	}

	private void PrintButtons<T>(T? ignore, ControlPadInput.PadNum pad, ref Rect rect) where T : struct, IComparable
	{
		foreach (T value in Enum.GetValues(typeof(T)))
		{
			if (!ignore.HasValue || !ignore.Value.Equals(value))
			{
				bool flag = IsDown(pad, value);
				GUI.TextField(rect, string.Format("{0}:{1}", value.ToString(), flag));
				rect.y += rect.height;
			}
		}
	}

	private void PrintValues<T>(T? ignore, ControlPadInput.PadNum pad, ref Rect rect) where T : struct, IComparable
	{
		foreach (T value2 in Enum.GetValues(typeof(T)))
		{
			if (!ignore.HasValue || !ignore.Value.Equals(value2))
			{
				float value = GetValue(pad, value2);
				GUI.TextField(rect, string.Format("{0}:{1}", value2.ToString(), value));
				rect.y += rect.height;
			}
		}
	}

	private bool IsDown<T>(ControlPadInput.PadNum _pad, T _button)
	{
		if (typeof(T) == typeof(ControlPadInput.Button))
		{
			ControlPadInput.Button button = (ControlPadInput.Button)(object)_button;
			switch (m_inputProvider)
			{
			case InputProvider.DirectInput:
				return Singleton<DirectInputProvider>.Get().IsDown(_pad, button);
			case InputProvider.XInput:
				return Singleton<XInputProvider>.Get().IsDown(_pad, button);
			default:
				return false;
			}
		}
		return false;
	}

	private float GetValue<T>(ControlPadInput.PadNum _pad, T _value)
	{
		if (typeof(T) == typeof(ControlPadInput.Value))
		{
			ControlPadInput.Value value = (ControlPadInput.Value)(object)_value;
			switch (m_inputProvider)
			{
			case InputProvider.DirectInput:
				return Singleton<DirectInputProvider>.Get().GetValue(_pad, value);
			case InputProvider.XInput:
				return Singleton<XInputProvider>.Get().GetValue(_pad, value);
			default:
				return 0f;
			}
		}
		return 0f;
	}
}
