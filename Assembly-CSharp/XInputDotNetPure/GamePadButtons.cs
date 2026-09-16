namespace XInputDotNetPure
{
	public struct GamePadButtons
	{
		private ButtonState start;

		private ButtonState back;

		private ButtonState leftStick;

		private ButtonState rightStick;

		private ButtonState leftShoulder;

		private ButtonState rightShoulder;

		private ButtonState a;

		private ButtonState b;

		private ButtonState x;

		private ButtonState y;

		public ButtonState Start
		{
			get
			{
				return start;
			}
		}

		public ButtonState Back
		{
			get
			{
				return back;
			}
		}

		public ButtonState LeftStick
		{
			get
			{
				return leftStick;
			}
		}

		public ButtonState RightStick
		{
			get
			{
				return rightStick;
			}
		}

		public ButtonState LeftShoulder
		{
			get
			{
				return leftShoulder;
			}
		}

		public ButtonState RightShoulder
		{
			get
			{
				return rightShoulder;
			}
		}

		public ButtonState A
		{
			get
			{
				return a;
			}
		}

		public ButtonState B
		{
			get
			{
				return b;
			}
		}

		public ButtonState X
		{
			get
			{
				return x;
			}
		}

		public ButtonState Y
		{
			get
			{
				return y;
			}
		}

		internal GamePadButtons(ButtonState start, ButtonState back, ButtonState leftStick, ButtonState rightStick, ButtonState leftShoulder, ButtonState rightShoulder, ButtonState a, ButtonState b, ButtonState x, ButtonState y)
		{
			this.start = start;
			this.back = back;
			this.leftStick = leftStick;
			this.rightStick = rightStick;
			this.leftShoulder = leftShoulder;
			this.rightShoulder = rightShoulder;
			this.a = a;
			this.b = b;
			this.x = x;
			this.y = y;
		}
	}
}
