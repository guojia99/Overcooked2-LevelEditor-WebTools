internal static class PadSidednessDefinition
{
	private static ControlPadInput.Button[] LeftSideButtons = new ControlPadInput.Button[8]
	{
		ControlPadInput.Button.LB,
		ControlPadInput.Button.LTrigger,
		ControlPadInput.Button.Back,
		ControlPadInput.Button.LeftAnalog,
		ControlPadInput.Button.DPadLeft,
		ControlPadInput.Button.DPadRight,
		ControlPadInput.Button.DPadUp,
		ControlPadInput.Button.DPadDown
	};

	private static ControlPadInput.Button[] RightSideButtons = new ControlPadInput.Button[8]
	{
		ControlPadInput.Button.RB,
		ControlPadInput.Button.RTrigger,
		ControlPadInput.Button.A,
		ControlPadInput.Button.B,
		ControlPadInput.Button.X,
		ControlPadInput.Button.Y,
		ControlPadInput.Button.Start,
		ControlPadInput.Button.RightAnalog
	};

	private static ControlPadInput.Value[] LeftSideValues = new ControlPadInput.Value[5]
	{
		ControlPadInput.Value.LStickX,
		ControlPadInput.Value.LStickY,
		ControlPadInput.Value.DPadX,
		ControlPadInput.Value.DPadY,
		ControlPadInput.Value.LTrigger
	};

	private static ControlPadInput.Value[] RightSideValues = new ControlPadInput.Value[3]
	{
		ControlPadInput.Value.RStickX,
		ControlPadInput.Value.RStickY,
		ControlPadInput.Value.RTrigger
	};

	public static ControlPadInput.Button[] FilterForSide(PadSide _side, ControlPadInput.Button[] _buttons)
	{
		switch (_side)
		{
		case PadSide.Both:
			return _buttons;
		case PadSide.Left:
			return _buttons.Intersection(LeftSideButtons);
		case PadSide.Right:
			return _buttons.Intersection(RightSideButtons);
		default:
			return null;
		}
	}

	public static ControlPadInput.Value[] FilterForSide(PadSide _side, ControlPadInput.Value[] _values)
	{
		switch (_side)
		{
		case PadSide.Both:
			return _values;
		case PadSide.Left:
			return _values.Intersection(LeftSideValues);
		case PadSide.Right:
			return _values.Intersection(RightSideValues);
		default:
			return null;
		}
	}
}
