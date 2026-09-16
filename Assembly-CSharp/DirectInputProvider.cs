using System;
using System.Collections.Generic;
using UnityEngine;

public class DirectInputProvider : Singleton<DirectInputProvider>, IGamepadInputProvider
{
	public const float sc_axisDeadZone = 0.4f;

	public static int ControllerCount;

	private static string[] sc_orderedJoysticks;

	private static Dictionary<ControlPadInput.Button, ButtonCode> sc_buttonsLookup;

	private static Dictionary<ControlPadInput.Value, AxisCode> sc_axisLookup;

	private static string[,] sc_keyNames;

	private static string[,] sc_valueNames;

	static DirectInputProvider()
	{
		ControllerCount = 11;
		sc_buttonsLookup = new Dictionary<ControlPadInput.Button, ButtonCode>();
		sc_buttonsLookup.Add(ControlPadInput.Button.A, ButtonCode.Button0);
		sc_buttonsLookup.Add(ControlPadInput.Button.B, ButtonCode.Button1);
		sc_buttonsLookup.Add(ControlPadInput.Button.X, ButtonCode.Button2);
		sc_buttonsLookup.Add(ControlPadInput.Button.Y, ButtonCode.Button3);
		sc_buttonsLookup.Add(ControlPadInput.Button.LB, ButtonCode.Button4);
		sc_buttonsLookup.Add(ControlPadInput.Button.RB, ButtonCode.Button5);
		sc_buttonsLookup.Add(ControlPadInput.Button.Back, ButtonCode.Button6);
		sc_buttonsLookup.Add(ControlPadInput.Button.Start, ButtonCode.Button7);
		sc_buttonsLookup.Add(ControlPadInput.Button.LeftAnalog, ButtonCode.Button8);
		sc_buttonsLookup.Add(ControlPadInput.Button.RightAnalog, ButtonCode.Button9);
		sc_axisLookup = new Dictionary<ControlPadInput.Value, AxisCode>();
		sc_axisLookup.Add(ControlPadInput.Value.LStickX, AxisCode.Axis1);
		sc_axisLookup.Add(ControlPadInput.Value.LStickY, AxisCode.Axis2);
		sc_axisLookup.Add(ControlPadInput.Value.RStickX, AxisCode.Axis4);
		sc_axisLookup.Add(ControlPadInput.Value.RStickY, AxisCode.Axis5);
		sc_axisLookup.Add(ControlPadInput.Value.DPadX, AxisCode.Axis6);
		sc_axisLookup.Add(ControlPadInput.Value.DPadY, AxisCode.Axis7);
		sc_axisLookup.Add(ControlPadInput.Value.LTrigger, AxisCode.Axis9);
		sc_axisLookup.Add(ControlPadInput.Value.RTrigger, AxisCode.Axis10);
		sc_keyNames = new string[12, 10];
		sc_valueNames = new string[12, 10];
		for (int i = 0; i < 12; i++)
		{
			for (int j = 0; j < 10; j++)
			{
				sc_keyNames[i, j] = "joystick " + (i + 1) + " button " + j;
			}
			for (int k = 0; k < 10; k++)
			{
				sc_valueNames[i, k] = "joystick " + (i + 1) + " analog " + k;
			}
		}
		sc_orderedJoysticks = new string[Enum.GetValues(typeof(ControlPadInput.PadNum)).Length];
		UpdateAssignedJoysticks();
	}

	public bool IsPadAttached(ControlPadInput.PadNum _pad)
	{
		string[] joystickNames = Input.GetJoystickNames();
		string joystickName = GetJoystickName(_pad);
		if (joystickName != string.Empty)
		{
			return joystickNames.Contains(joystickName);
		}
		return false;
	}

	private string GetJoystickName(ControlPadInput.PadNum _padNum)
	{
		return sc_orderedJoysticks[(int)_padNum];
	}

	private static void UpdateAssignedJoysticks()
	{
		string[] joystickNames = Input.GetJoystickNames();
		if (joystickNames == null)
		{
			return;
		}
		for (int i = 0; i < sc_orderedJoysticks.Length; i++)
		{
			if (sc_orderedJoysticks[i] == null)
			{
				sc_orderedJoysticks[i] = string.Empty;
				continue;
			}
			bool flag = false;
			for (int j = 0; j < joystickNames.Length; j++)
			{
				if (joystickNames[j] != null && joystickNames[j].Equals(sc_orderedJoysticks[i]))
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				sc_orderedJoysticks[i] = string.Empty;
			}
		}
		for (int k = 0; k < joystickNames.Length; k++)
		{
			if (joystickNames[k] == null)
			{
				continue;
			}
			bool flag2 = false;
			for (int l = 0; l < sc_orderedJoysticks.Length; l++)
			{
				if (sc_orderedJoysticks[l].Equals(joystickNames[k]))
				{
					flag2 = true;
					break;
				}
			}
			if (flag2)
			{
				continue;
			}
			for (int m = 0; m < sc_orderedJoysticks.Length; m++)
			{
				if (sc_orderedJoysticks[m].Equals(string.Empty))
				{
					sc_orderedJoysticks[m] = joystickNames[k];
					break;
				}
			}
		}
	}

	public bool IsDown(ControlPadInput.PadNum _pad, ControlPadInput.Button _button)
	{
		switch (_button)
		{
		case ControlPadInput.Button.LTrigger:
			return GetValue(_pad, ControlPadInput.Value.LTrigger) > 0.5f;
		case ControlPadInput.Button.RTrigger:
			return GetValue(_pad, ControlPadInput.Value.RTrigger) > 0.5f;
		case ControlPadInput.Button.DPadLeft:
			return GetValue(_pad, ControlPadInput.Value.DPadX) < -0.5f;
		case ControlPadInput.Button.DPadRight:
			return GetValue(_pad, ControlPadInput.Value.DPadX) > 0.5f;
		case ControlPadInput.Button.DPadUp:
			return GetValue(_pad, ControlPadInput.Value.DPadY) < -0.5f;
		case ControlPadInput.Button.DPadDown:
			return GetValue(_pad, ControlPadInput.Value.DPadY) > 0.5f;
		default:
		{
			string unityKeyName = GetUnityKeyName(_button, _pad);
			return Input.GetKey(unityKeyName);
		}
		}
	}

	public float GetValue(ControlPadInput.PadNum _pad, ControlPadInput.Value _value)
	{
		switch (_value)
		{
		case ControlPadInput.Value.LStickX:
			return GetDeadenedAxis(_pad, ControlPadInput.Value.LStickX, ControlPadInput.Value.LStickY).x;
		case ControlPadInput.Value.LStickY:
			return GetDeadenedAxis(_pad, ControlPadInput.Value.LStickX, ControlPadInput.Value.LStickY).y;
		case ControlPadInput.Value.RStickX:
			return GetDeadenedAxis(_pad, ControlPadInput.Value.RStickX, ControlPadInput.Value.RStickY).x;
		case ControlPadInput.Value.RStickY:
			return GetDeadenedAxis(_pad, ControlPadInput.Value.RStickX, ControlPadInput.Value.RStickY).y;
		case ControlPadInput.Value.DPadY:
			return 0f - GetDeadenedAxis(_pad, ControlPadInput.Value.DPadX, ControlPadInput.Value.DPadY).y;
		default:
		{
			string unityAxisName = GetUnityAxisName(_value, _pad);
			return Input.GetAxis(unityAxisName);
		}
		}
	}

	private static string GetUnityKeyName(ControlPadInput.Button _button, ControlPadInput.PadNum _pad)
	{
		return sc_keyNames[(int)_pad, (int)sc_buttonsLookup[_button]];
	}

	private static string GetUnityAxisName(ControlPadInput.Value _value, ControlPadInput.PadNum _pad)
	{
		return sc_valueNames[(int)_pad, (int)sc_axisLookup[_value]];
	}

	private static Vector2 GetDeadenedAxis(ControlPadInput.PadNum _pad, ControlPadInput.Value _valuex, ControlPadInput.Value _valuey)
	{
		string unityAxisName = GetUnityAxisName(_valuex, _pad);
		string unityAxisName2 = GetUnityAxisName(_valuey, _pad);
		Vector2 result = new Vector2(Input.GetAxis(unityAxisName), Input.GetAxis(unityAxisName2));
		if (result.sqrMagnitude > 0.16000001f)
		{
			return result;
		}
		return Vector2.zero;
	}

	public static void Update()
	{
		UpdateAssignedJoysticks();
	}
}
