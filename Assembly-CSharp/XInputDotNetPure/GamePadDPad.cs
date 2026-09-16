namespace XInputDotNetPure
{
	public struct GamePadDPad
	{
		private ButtonState up;

		private ButtonState down;

		private ButtonState left;

		private ButtonState right;

		public ButtonState Up
		{
			get
			{
				return up;
			}
		}

		public ButtonState Down
		{
			get
			{
				return down;
			}
		}

		public ButtonState Left
		{
			get
			{
				return left;
			}
		}

		public ButtonState Right
		{
			get
			{
				return right;
			}
		}

		internal GamePadDPad(ButtonState up, ButtonState down, ButtonState left, ButtonState right)
		{
			this.up = up;
			this.down = down;
			this.left = left;
			this.right = right;
		}
	}
}
