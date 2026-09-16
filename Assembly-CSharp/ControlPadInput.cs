public static class ControlPadInput
{
	public enum Button
	{
		Invalid = 0,
		A = 1,
		X = 2,
		B = 3,
		Y = 4,
		LB = 5,
		RB = 6,
		LTrigger = 7,
		RTrigger = 8,
		DPadLeft = 9,
		DPadRight = 10,
		DPadUp = 11,
		DPadDown = 12,
		Back = 13,
		Start = 14,
		LeftAnalog = 15,
		RightAnalog = 16
	}

	public enum Value
	{
		Invalid = 0,
		LStickX = 1,
		LStickY = 2,
		RStickX = 3,
		RStickY = 4,
		DPadX = 5,
		DPadY = 6,
		LTrigger = 7,
		RTrigger = 8
	}

	public enum PadNum
	{
		One = 0,
		Two = 1,
		Three = 2,
		Four = 3,
		Five = 4,
		Six = 5,
		Seven = 6,
		Eight = 7,
		Nine = 8,
		Ten = 9,
		Eleven = 10,
		Twelve = 11,
		Thriteen = 12,
		Forteen = 13,
		Fifteen = 14,
		Count = 15,
		Invalid = 15
	}

	public struct ButtonIdentifier
	{
		public PadNum pad;

		public Button button;

		public ButtonIdentifier(PadNum _pad, Button _button)
		{
			pad = _pad;
			button = _button;
		}
	}

	public struct ValueIdentifier
	{
		public PadNum pad;

		public Value value;

		public ValueIdentifier(PadNum _pad, Value _value)
		{
			pad = _pad;
			value = _value;
		}
	}
}
