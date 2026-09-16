using System;
using System.Collections.Generic;
using InControl;

public class StandardActionSet : PlayerActionSet
{
	private class ButtonComparer : IEqualityComparer<ControlPadInput.Button>
	{
		public bool Equals(ControlPadInput.Button x, ControlPadInput.Button y)
		{
			return x == y;
		}

		public int GetHashCode(ControlPadInput.Button obj)
		{
			return (int)obj;
		}
	}

	private class ValueComparer : IEqualityComparer<ControlPadInput.Value>
	{
		public bool Equals(ControlPadInput.Value x, ControlPadInput.Value y)
		{
			return x == y;
		}

		public int GetHashCode(ControlPadInput.Value obj)
		{
			return (int)obj;
		}
	}

	private ControlPadInput.Button[] m_buttons;

	private ControlPadInput.Value[] m_values;

	public PlayerAction EngagementButton;

	public Dictionary<ControlPadInput.Button, PlayerAction> ButtonActions = new Dictionary<ControlPadInput.Button, PlayerAction>(new ButtonComparer());

	public Dictionary<ControlPadInput.Value, PlayerOneAxisAction> ValueActions = new Dictionary<ControlPadInput.Value, PlayerOneAxisAction>(new ValueComparer());

	private Dictionary<ControlPadInput.Value, PlayerAction> m_pveValueActions = new Dictionary<ControlPadInput.Value, PlayerAction>(new ValueComparer());

	private Dictionary<ControlPadInput.Value, PlayerAction> m_nveValueActions = new Dictionary<ControlPadInput.Value, PlayerAction>(new ValueComparer());

	private bool m_isSplit;

	private StandardActionSet()
	{
		m_buttons = (ControlPadInput.Button[])Enum.GetValues(typeof(ControlPadInput.Button));
		m_values = (ControlPadInput.Value[])Enum.GetValues(typeof(ControlPadInput.Value));
		ResetActions();
	}

	private void ResetActions()
	{
		ClearActions();
		EngagementButton = CreatePlayerAction("Engagment");
		ButtonActions.Clear();
		for (int i = 0; i < m_buttons.Length; i++)
		{
			PlayerAction value = CreatePlayerAction(m_buttons[i].ToString());
			ButtonActions.Add(m_buttons[i], value);
		}
		m_pveValueActions.Clear();
		m_nveValueActions.Clear();
		ValueActions.Clear();
		for (int j = 0; j < m_values.Length; j++)
		{
			PlayerAction playerAction = CreatePlayerAction(m_values[j].ToString() + " Plus");
			PlayerAction playerAction2 = CreatePlayerAction(m_values[j].ToString() + " Minus");
			m_pveValueActions.Add(m_values[j], playerAction);
			m_nveValueActions.Add(m_values[j], playerAction2);
			PlayerOneAxisAction value2 = CreateOneAxisPlayerAction(playerAction2, playerAction);
			ValueActions.Add(m_values[j], value2);
		}
	}

	public static StandardActionSet CreateForJoystick(InputDevice _device)
	{
		StandardActionSet standardActionSet = new StandardActionSet();
		standardActionSet.EngagementButton.AddDefaultBinding(InputControlType.Action1);
		standardActionSet.ButtonActions[ControlPadInput.Button.A].AddDefaultBinding(InputControlType.Action1);
		standardActionSet.ButtonActions[ControlPadInput.Button.B].AddDefaultBinding(InputControlType.Action2);
		standardActionSet.ButtonActions[ControlPadInput.Button.X].AddDefaultBinding(InputControlType.Action3);
		standardActionSet.ButtonActions[ControlPadInput.Button.Y].AddDefaultBinding(InputControlType.Action4);
		standardActionSet.ButtonActions[ControlPadInput.Button.LB].AddDefaultBinding(InputControlType.LeftBumper);
		standardActionSet.ButtonActions[ControlPadInput.Button.RB].AddDefaultBinding(InputControlType.RightBumper);
		standardActionSet.ButtonActions[ControlPadInput.Button.LTrigger].AddDefaultBinding(InputControlType.LeftTrigger);
		standardActionSet.ButtonActions[ControlPadInput.Button.RTrigger].AddDefaultBinding(InputControlType.RightTrigger);
		standardActionSet.ButtonActions[ControlPadInput.Button.DPadLeft].AddDefaultBinding(InputControlType.DPadLeft);
		standardActionSet.ButtonActions[ControlPadInput.Button.DPadRight].AddDefaultBinding(InputControlType.DPadRight);
		standardActionSet.ButtonActions[ControlPadInput.Button.DPadUp].AddDefaultBinding(InputControlType.DPadUp);
		standardActionSet.ButtonActions[ControlPadInput.Button.DPadDown].AddDefaultBinding(InputControlType.DPadDown);
		standardActionSet.ButtonActions[ControlPadInput.Button.Back].AddDefaultBinding(InputControlType.Back);
		standardActionSet.ButtonActions[ControlPadInput.Button.Start].AddDefaultBinding(InputControlType.Start);
		standardActionSet.ButtonActions[ControlPadInput.Button.Start].AddDefaultBinding(InputControlType.Options);
		standardActionSet.ButtonActions[ControlPadInput.Button.LeftAnalog].AddDefaultBinding(InputControlType.LeftStickButton);
		standardActionSet.ButtonActions[ControlPadInput.Button.RightAnalog].AddDefaultBinding(InputControlType.RightStickButton);
		standardActionSet.m_pveValueActions[ControlPadInput.Value.DPadX].AddDefaultBinding(InputControlType.DPadRight);
		standardActionSet.m_nveValueActions[ControlPadInput.Value.DPadX].AddDefaultBinding(InputControlType.DPadLeft);
		standardActionSet.m_pveValueActions[ControlPadInput.Value.DPadY].AddDefaultBinding(InputControlType.DPadDown);
		standardActionSet.m_nveValueActions[ControlPadInput.Value.DPadY].AddDefaultBinding(InputControlType.DPadUp);
		standardActionSet.m_pveValueActions[ControlPadInput.Value.LStickX].AddDefaultBinding(InputControlType.LeftStickRight);
		standardActionSet.m_nveValueActions[ControlPadInput.Value.LStickX].AddDefaultBinding(InputControlType.LeftStickLeft);
		standardActionSet.m_pveValueActions[ControlPadInput.Value.LStickY].AddDefaultBinding(InputControlType.LeftStickDown);
		standardActionSet.m_nveValueActions[ControlPadInput.Value.LStickY].AddDefaultBinding(InputControlType.LeftStickUp);
		standardActionSet.m_pveValueActions[ControlPadInput.Value.RStickX].AddDefaultBinding(InputControlType.RightStickRight);
		standardActionSet.m_nveValueActions[ControlPadInput.Value.RStickX].AddDefaultBinding(InputControlType.RightStickLeft);
		standardActionSet.m_pveValueActions[ControlPadInput.Value.RStickY].AddDefaultBinding(InputControlType.RightStickDown);
		standardActionSet.m_nveValueActions[ControlPadInput.Value.RStickY].AddDefaultBinding(InputControlType.RightStickUp);
		standardActionSet.m_pveValueActions[ControlPadInput.Value.LTrigger].AddDefaultBinding(InputControlType.LeftTrigger);
		standardActionSet.m_pveValueActions[ControlPadInput.Value.RTrigger].AddDefaultBinding(InputControlType.RightTrigger);
		standardActionSet.Device = _device;
		return standardActionSet;
	}

	public static bool IsKeyboardSplit()
	{
		GameInputConfig baseInputConfig = PlayerInputLookup.GetBaseInputConfig();
		if (baseInputConfig == null)
		{
			return false;
		}
		for (int i = 0; i < baseInputConfig.m_playerConfigs.Length; i++)
		{
			GameInputConfig.ConfigEntry configEntry = baseInputConfig.m_playerConfigs[i];
			if (PCPadInputProvider.IsKeyboard(configEntry.Pad) && configEntry.Side != PadSide.Both)
			{
				return true;
			}
		}
		return false;
	}

	private static void RefreshKeyboardActions(StandardActionSet actionSet, KeyboardBindings bindings, bool bForce)
	{
		bool isSplit = actionSet.m_isSplit;
		actionSet.m_isSplit = IsKeyboardSplit();
		if (bForce || isSplit != actionSet.m_isSplit)
		{
			actionSet.ResetActions();
			if (actionSet.m_isSplit)
			{
				ModifyForSplitKeyboard(actionSet, bindings);
			}
			else
			{
				ModifyForCombinedKeyboard(actionSet, bindings);
			}
		}
	}

	public static StandardActionSet CreateForKeyboard(KeyboardBindings bindings)
	{
		StandardActionSet actionSet = new StandardActionSet();
		PCPadInputProvider.OnUpdateKeyboardButtons = (CallbackBool)Delegate.Combine(PCPadInputProvider.OnUpdateKeyboardButtons, (CallbackBool)delegate(bool bForce)
		{
			RefreshKeyboardActions(actionSet, bindings, bForce);
		});
		ModifyForCombinedKeyboard(actionSet, bindings);
		RefreshKeyboardActions(actionSet, bindings, false);
		return actionSet;
	}

	public static void ModifyForCombinedKeyboard(StandardActionSet actionSet, KeyboardBindings bindings)
	{
		SetBindings(actionSet, bindings.m_CombinedKeyboard);
	}

	public static void ModifyForSplitKeyboard(StandardActionSet actionSet, KeyboardBindings bindings)
	{
		SetBindings(actionSet, bindings.m_SplitKeyboard);
	}

	private static void SetBindings(StandardActionSet actionSet, KeyboardBindingSet bindingSet)
	{
		actionSet.EngagementButton.AddDefaultBinding(Key.Space);
		foreach (KeyValuePair<ControlPadInput.Button, List<Key>> buttonBinding in bindingSet.m_ButtonBindings)
		{
			foreach (Key item in buttonBinding.Value)
			{
				actionSet.ButtonActions[buttonBinding.Key].AddDefaultBinding(item);
			}
		}
		foreach (KeyValuePair<ControlPadInput.Value, List<Key>> positiveValueBinding in bindingSet.m_PositiveValueBindings)
		{
			foreach (Key item2 in positiveValueBinding.Value)
			{
				actionSet.m_pveValueActions[positiveValueBinding.Key].AddDefaultBinding(item2);
			}
		}
		foreach (KeyValuePair<ControlPadInput.Value, List<Key>> negativeValueBinding in bindingSet.m_NegativeValueBindings)
		{
			foreach (Key item3 in negativeValueBinding.Value)
			{
				actionSet.m_nveValueActions[negativeValueBinding.Key].AddDefaultBinding(item3);
			}
		}
	}
}
