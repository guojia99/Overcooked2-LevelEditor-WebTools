namespace XInputDotNetPure
{
	public struct GamePadTriggers
	{
		private float left;

		private float right;

		public float Left
		{
			get
			{
				return left;
			}
		}

		public float Right
		{
			get
			{
				return right;
			}
		}

		internal GamePadTriggers(float left, float right)
		{
			this.left = left;
			this.right = right;
		}
	}
}
