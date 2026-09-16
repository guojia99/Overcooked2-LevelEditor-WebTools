using System;
using UnityEngine;

[Serializable]
public class AmbiControlsMappingData : ScriptableObject
{
	[Serializable]
	public class ButtonMapping
	{
		public AmbiPadButton GamepadButton;

		public ControlPadInput.Button[] RealButtons;

		public ButtonMapping(AmbiPadButton _gamePadButton, ControlPadInput.Button[] _realButtons)
		{
			GamepadButton = _gamePadButton;
			RealButtons = _realButtons;
		}
	}

	[Serializable]
	public class ValueMapping
	{
		public AmbiPadValue GamepadValue;

		public ControlPadInput.Value[] RealValues;

		public ValueMapping(AmbiPadValue _gamePadValue, ControlPadInput.Value[] _realValues)
		{
			GamepadValue = _gamePadValue;
			RealValues = _realValues;
		}
	}

	public ButtonMapping[] m_buttonMapping = new ButtonMapping[8]
	{
		new ButtonMapping(AmbiPadButton.One, new ControlPadInput.Button[3]
		{
			ControlPadInput.Button.A,
			ControlPadInput.Button.RB,
			ControlPadInput.Button.LB
		}),
		new ButtonMapping(AmbiPadButton.Two, new ControlPadInput.Button[3]
		{
			ControlPadInput.Button.X,
			ControlPadInput.Button.RTrigger,
			ControlPadInput.Button.LTrigger
		}),
		new ButtonMapping(AmbiPadButton.Three, new ControlPadInput.Button[3]
		{
			ControlPadInput.Button.B,
			ControlPadInput.Button.DPadLeft,
			ControlPadInput.Button.DPadRight
		}),
		new ButtonMapping(AmbiPadButton.Five, new ControlPadInput.Button[4]
		{
			ControlPadInput.Button.B,
			ControlPadInput.Button.Y,
			ControlPadInput.Button.DPadUp,
			ControlPadInput.Button.DPadDown
		}),
		new ButtonMapping(AmbiPadButton.Start, new ControlPadInput.Button[2]
		{
			ControlPadInput.Button.Start,
			ControlPadInput.Button.Start
		}),
		new ButtonMapping(AmbiPadButton.Back, new ControlPadInput.Button[2]
		{
			ControlPadInput.Button.Back,
			ControlPadInput.Button.Back
		}),
		new ButtonMapping(AmbiPadButton.Horn, new ControlPadInput.Button[2]
		{
			ControlPadInput.Button.Y,
			ControlPadInput.Button.DPadUp
		}),
		new ButtonMapping(AmbiPadButton.DebugMenu, new ControlPadInput.Button[1] { ControlPadInput.Button.Back })
	};

	public ValueMapping[] m_valueMapping = new ValueMapping[2]
	{
		new ValueMapping(AmbiPadValue.StickX, new ControlPadInput.Value[2]
		{
			ControlPadInput.Value.LStickX,
			ControlPadInput.Value.RStickX
		}),
		new ValueMapping(AmbiPadValue.StickY, new ControlPadInput.Value[2]
		{
			ControlPadInput.Value.LStickY,
			ControlPadInput.Value.RStickY
		})
	};

	public ControlPadInput.Button[] GetRealButtons(AmbiPadButton _gamePadButton)
	{
		if (PlayerManagerShared<PCPlayerManager.PCPlayerProfile>.AcceptAndCancelButtonsInverted)
		{
			if (_gamePadButton == AmbiPadButton.Confirm)
			{
				_gamePadButton = AmbiPadButton.Cancel;
			}
			else if (_gamePadButton == AmbiPadButton.Cancel)
			{
				_gamePadButton = AmbiPadButton.Confirm;
			}
		}
		ButtonMapping buttonMapping = Array.Find(m_buttonMapping, (ButtonMapping obj) => obj.GamepadButton == _gamePadButton);
		return buttonMapping.RealButtons;
	}

	public ControlPadInput.Value[] GetRealValues(AmbiPadValue _gamePadValue)
	{
		ValueMapping valueMapping = Array.Find(m_valueMapping, (ValueMapping obj) => obj.GamepadValue == _gamePadValue);
		return valueMapping.RealValues;
	}
}
