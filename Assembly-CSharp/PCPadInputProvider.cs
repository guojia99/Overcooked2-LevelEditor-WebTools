using System.Collections.Generic;
using InControl;

public class PCPadInputProvider : Singleton<PCPadInputProvider>, IGamepadInputProvider
{
	private class PadNumComparer : IEqualityComparer<ControlPadInput.PadNum>
	{
		public bool Equals(ControlPadInput.PadNum x, ControlPadInput.PadNum y)
		{
			return x == y;
		}

		public int GetHashCode(ControlPadInput.PadNum obj)
		{
			return (int)obj;
		}
	}

	private static Dictionary<ControlPadInput.PadNum, StandardActionSet> s_engagedPads;

	private static List<StandardActionSet> m_allDevices;

	public static CallbackBool OnUpdateKeyboardButtons;

	private static BindingListener m_BindingListener;

	private static KeyboardBindings m_DefaultKeyboardBindings;

	private static KeyboardBindings m_UserKeyboardBindings;

	static PCPadInputProvider()
	{
		s_engagedPads = new Dictionary<ControlPadInput.PadNum, StandardActionSet>(new PadNumComparer());
		m_allDevices = new List<StandardActionSet>();
		OnUpdateKeyboardButtons = delegate
		{
		};
		m_BindingListener = new BindingListener();
		m_DefaultKeyboardBindings = null;
		m_UserKeyboardBindings = null;
		InitialiseBindings();
		GameDebugConfig debugConfig = GameUtils.GetDebugConfig();
		if (debugConfig.m_keyboardType == GameDebugConfig.KeyboardType.Actual)
		{
			m_allDevices.Add(StandardActionSet.CreateForKeyboard(m_UserKeyboardBindings));
		}
		for (int num = 0; num < InputManager.Devices.Count; num++)
		{
			m_allDevices.Add(StandardActionSet.CreateForJoystick(InputManager.Devices[num]));
		}
		InputManager.OnDeviceAttached += OnDeviceAttached;
		InputManager.OnDeviceDetached += OnDeviceDetached;
	}

	public static KeyboardBindingSet GetDefaultSplitKeyboardBindings()
	{
		KeyboardBindingSet keyboardBindingSet = new KeyboardBindingSet();
		keyboardBindingSet.m_ButtonBindings.Add(ControlPadInput.Button.DPadDown, new List<Key> { Key.LeftShift });
		keyboardBindingSet.m_ButtonBindings.Add(ControlPadInput.Button.DPadLeft, new List<Key> { Key.LeftControl });
		keyboardBindingSet.m_ButtonBindings.Add(ControlPadInput.Button.DPadRight, new List<Key> { Key.LeftAlt });
		keyboardBindingSet.m_ButtonBindings.Add(ControlPadInput.Button.DPadUp, new List<Key> { Key.E });
		keyboardBindingSet.m_ButtonBindings.Add(ControlPadInput.Button.LeftAnalog, new List<Key> { Key.T });
		keyboardBindingSet.m_ButtonBindings.Add(ControlPadInput.Button.LB, new List<Key> { Key.LeftShift });
		keyboardBindingSet.m_ButtonBindings.Add(ControlPadInput.Button.LTrigger, new List<Key> { Key.LeftControl });
		keyboardBindingSet.m_PositiveValueBindings.Add(ControlPadInput.Value.LStickX, new List<Key> { Key.D });
		keyboardBindingSet.m_NegativeValueBindings.Add(ControlPadInput.Value.LStickX, new List<Key> { Key.A });
		keyboardBindingSet.m_NegativeValueBindings.Add(ControlPadInput.Value.LStickY, new List<Key> { Key.W });
		keyboardBindingSet.m_PositiveValueBindings.Add(ControlPadInput.Value.LStickY, new List<Key> { Key.S });
		keyboardBindingSet.m_ButtonBindings.Add(ControlPadInput.Button.A, new List<Key> { Key.RightShift });
		keyboardBindingSet.m_ButtonBindings.Add(ControlPadInput.Button.B, new List<Key> { Key.RightAlt });
		keyboardBindingSet.m_ButtonBindings.Add(ControlPadInput.Button.X, new List<Key> { Key.RightControl });
		keyboardBindingSet.m_ButtonBindings.Add(ControlPadInput.Button.Y, new List<Key> { Key.I });
		keyboardBindingSet.m_ButtonBindings.Add(ControlPadInput.Button.RightAnalog, new List<Key> { Key.P });
		keyboardBindingSet.m_ButtonBindings.Add(ControlPadInput.Button.RB, new List<Key> { Key.RightShift });
		keyboardBindingSet.m_ButtonBindings.Add(ControlPadInput.Button.RTrigger, new List<Key> { Key.RightControl });
		keyboardBindingSet.m_PositiveValueBindings.Add(ControlPadInput.Value.RStickX, new List<Key> { Key.RightArrow });
		keyboardBindingSet.m_NegativeValueBindings.Add(ControlPadInput.Value.RStickX, new List<Key> { Key.LeftArrow });
		keyboardBindingSet.m_NegativeValueBindings.Add(ControlPadInput.Value.RStickY, new List<Key> { Key.UpArrow });
		keyboardBindingSet.m_PositiveValueBindings.Add(ControlPadInput.Value.RStickY, new List<Key> { Key.DownArrow });
		keyboardBindingSet.m_NegativeValueBindings.Add(ControlPadInput.Value.DPadY, new List<Key> { Key.UpArrow });
		keyboardBindingSet.m_PositiveValueBindings.Add(ControlPadInput.Value.DPadY, new List<Key> { Key.DownArrow });
		keyboardBindingSet.m_ButtonBindings.Add(ControlPadInput.Button.Back, new List<Key> { Key.Tab });
		keyboardBindingSet.m_ButtonBindings.Add(ControlPadInput.Button.Start, new List<Key> { Key.PadEnter });
		return keyboardBindingSet;
	}

	public static KeyboardBindingSet GetDefaultCombinedKeyboardBindings()
	{
		KeyboardBindingSet keyboardBindingSet = new KeyboardBindingSet();
		keyboardBindingSet.m_ButtonBindings.Add(ControlPadInput.Button.A, new List<Key> { Key.Space });
		keyboardBindingSet.m_ButtonBindings.Add(ControlPadInput.Button.B, new List<Key> { Key.LeftAlt });
		keyboardBindingSet.m_ButtonBindings.Add(ControlPadInput.Button.X, new List<Key> { Key.LeftControl });
		keyboardBindingSet.m_ButtonBindings.Add(ControlPadInput.Button.Y, new List<Key> { Key.E });
		keyboardBindingSet.m_ButtonBindings.Add(ControlPadInput.Button.LB, new List<Key> { Key.LeftShift });
		keyboardBindingSet.m_ButtonBindings.Add(ControlPadInput.Button.RB, new List<Key> { Key.RightShift });
		keyboardBindingSet.m_ButtonBindings.Add(ControlPadInput.Button.Start, new List<Key> { Key.PadEnter });
		keyboardBindingSet.m_ButtonBindings.Add(ControlPadInput.Button.LeftAnalog, new List<Key> { Key.T });
		keyboardBindingSet.m_PositiveValueBindings.Add(ControlPadInput.Value.LStickX, new List<Key>
		{
			Key.D,
			Key.RightArrow
		});
		keyboardBindingSet.m_NegativeValueBindings.Add(ControlPadInput.Value.LStickX, new List<Key>
		{
			Key.A,
			Key.LeftArrow
		});
		keyboardBindingSet.m_NegativeValueBindings.Add(ControlPadInput.Value.LStickY, new List<Key>
		{
			Key.W,
			Key.UpArrow
		});
		keyboardBindingSet.m_PositiveValueBindings.Add(ControlPadInput.Value.LStickY, new List<Key>
		{
			Key.S,
			Key.DownArrow
		});
		keyboardBindingSet.m_NegativeValueBindings.Add(ControlPadInput.Value.DPadY, new List<Key> { Key.UpArrow });
		keyboardBindingSet.m_PositiveValueBindings.Add(ControlPadInput.Value.DPadY, new List<Key> { Key.DownArrow });
		return keyboardBindingSet;
	}

	public static void InitialiseBindings()
	{
		m_DefaultKeyboardBindings = new KeyboardBindings("DefaultKeyBindings");
		m_DefaultKeyboardBindings.m_SplitKeyboard = GetDefaultSplitKeyboardBindings();
		m_DefaultKeyboardBindings.m_CombinedKeyboard = GetDefaultCombinedKeyboardBindings();
		m_UserKeyboardBindings = new KeyboardBindings("UserKeyBindings");
		m_UserKeyboardBindings.CopyFrom(m_DefaultKeyboardBindings);
	}

	public static void SaveBindings(GlobalSave saveData)
	{
		if (saveData != null)
		{
			m_UserKeyboardBindings.Save(saveData);
		}
		UpdateKeyboardButtons(true);
	}

	public static void LoadBindings(GlobalSave saveData)
	{
		if (saveData != null)
		{
			if (!m_UserKeyboardBindings.Load(saveData))
			{
				RestoreDefaultBindings();
			}
			else
			{
				UpdateKeyboardButtons(true);
			}
		}
	}

	public static void RestoreDefaultBindings()
	{
		m_UserKeyboardBindings.CopyFrom(m_DefaultKeyboardBindings);
		UpdateKeyboardButtons(true);
	}

	public static void RestoreDefaultCombinedBindings()
	{
		m_UserKeyboardBindings.m_CombinedKeyboard.CopyFrom(m_DefaultKeyboardBindings.m_CombinedKeyboard);
		UpdateKeyboardButtons(true);
	}

	public static void RestoreDefaultSplitBindings()
	{
		m_UserKeyboardBindings.m_SplitKeyboard.CopyFrom(m_DefaultKeyboardBindings.m_SplitKeyboard);
		UpdateKeyboardButtons(true);
	}

	public static List<Key> GetBindings(ControlPadInput.Button button, bool bSplit)
	{
		KeyboardBindingSet keyboardBindingSet = ((!bSplit) ? m_UserKeyboardBindings.m_CombinedKeyboard : m_UserKeyboardBindings.m_SplitKeyboard);
		if (keyboardBindingSet.m_ButtonBindings.ContainsKey(button))
		{
			return keyboardBindingSet.m_ButtonBindings[button];
		}
		return null;
	}

	public static void SetBindings(ControlPadInput.Button button, List<Key> keys, bool bSplit)
	{
		KeyboardBindingSet keyboardBindingSet = ((!bSplit) ? m_UserKeyboardBindings.m_CombinedKeyboard : m_UserKeyboardBindings.m_SplitKeyboard);
		if (keyboardBindingSet.m_ButtonBindings.ContainsKey(button))
		{
			keyboardBindingSet.m_ButtonBindings[button] = keys;
		}
	}

	public static List<Key> GetBindings(ControlPadInput.Value value, bool bPositive, bool bSplit)
	{
		KeyboardBindingSet keyboardBindingSet = ((!bSplit) ? m_UserKeyboardBindings.m_CombinedKeyboard : m_UserKeyboardBindings.m_SplitKeyboard);
		Dictionary<ControlPadInput.Value, List<Key>> dictionary = ((!bPositive) ? keyboardBindingSet.m_NegativeValueBindings : keyboardBindingSet.m_PositiveValueBindings);
		if (dictionary.ContainsKey(value))
		{
			return dictionary[value];
		}
		return null;
	}

	public static void SetBindings(ControlPadInput.Value value, List<Key> keys, bool bPositive, bool bSplit)
	{
		KeyboardBindingSet keyboardBindingSet = ((!bSplit) ? m_UserKeyboardBindings.m_CombinedKeyboard : m_UserKeyboardBindings.m_SplitKeyboard);
		Dictionary<ControlPadInput.Value, List<Key>> dictionary = ((!bPositive) ? keyboardBindingSet.m_NegativeValueBindings : keyboardBindingSet.m_PositiveValueBindings);
		if (dictionary.ContainsKey(value))
		{
			dictionary[value] = keys;
		}
	}

	public static void StartListeningForBinding(VoidGeneric<Key> OnBindingReceived)
	{
		m_BindingListener.StartListening(OnBindingReceived);
	}

	public static void StopListeningForBinding()
	{
		m_BindingListener.StopListening();
	}

	private static void OnDeviceAttached(InputDevice _device)
	{
		m_allDevices.Add(StandardActionSet.CreateForJoystick(_device));
	}

	private static void OnDeviceDetached(InputDevice _device)
	{
		m_allDevices.RemoveAll((StandardActionSet x) => x.Device == _device);
	}

	private static StandardActionSet GetActionSet(ControlPadInput.PadNum _pad)
	{
		if (s_engagedPads.ContainsKey(_pad))
		{
			return s_engagedPads[_pad];
		}
		int num = -1;
		for (int i = 0; i <= (int)_pad; i++)
		{
			if (!s_engagedPads.ContainsKey((ControlPadInput.PadNum)i))
			{
				num++;
			}
		}
		int num2 = 0;
		for (int j = 0; j < m_allDevices.Count; j++)
		{
			if (!s_engagedPads.ContainsValue(m_allDevices[j]))
			{
				if (num2 == num)
				{
					return m_allDevices[j];
				}
				num2++;
			}
		}
		return null;
	}

	public static bool IsKeyboard(ControlPadInput.PadNum _padNum)
	{
		StandardActionSet actionSet = GetActionSet(_padNum);
		if (actionSet != null && actionSet.Device == null)
		{
			return true;
		}
		return false;
	}

	public static void UpdateKeyboardButtons(bool bForce = false)
	{
		OnUpdateKeyboardButtons(bForce);
	}

	public static PlayerActionSet EngagePad(ControlPadInput.PadNum _oldPadNum, ControlPadInput.PadNum _newPadNum)
	{
		StandardActionSet actionSet = GetActionSet(_oldPadNum);
		if (actionSet != null)
		{
			s_engagedPads.SafeAdd(_newPadNum, actionSet);
		}
		return actionSet;
	}

	public static void DisengagePad(ControlPadInput.PadNum _padNum)
	{
		s_engagedPads.SafeRemove(_padNum);
	}

	public bool IsPadAttached(ControlPadInput.PadNum _pad)
	{
		StandardActionSet actionSet = GetActionSet(_pad);
		if (actionSet != null)
		{
			if (actionSet.Device != null)
			{
				return actionSet.Device.IsAttached;
			}
			return true;
		}
		return false;
	}

	public bool IsEngagementDown(ControlPadInput.PadNum _pad)
	{
		StandardActionSet actionSet = GetActionSet(_pad);
		if (actionSet != null)
		{
			return actionSet.EngagementButton.State;
		}
		return false;
	}

	public bool IsDown(ControlPadInput.PadNum _pad, ControlPadInput.Button _button)
	{
		StandardActionSet actionSet = GetActionSet(_pad);
		if (actionSet != null)
		{
			return actionSet.ButtonActions[_button].State;
		}
		return false;
	}

	public float GetValue(ControlPadInput.PadNum _pad, ControlPadInput.Value _value)
	{
		StandardActionSet actionSet = GetActionSet(_pad);
		if (actionSet != null)
		{
			return actionSet.ValueActions[_value].Value;
		}
		return 0f;
	}
}
