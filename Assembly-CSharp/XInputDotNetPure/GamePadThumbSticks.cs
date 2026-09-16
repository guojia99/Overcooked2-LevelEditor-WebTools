using UnityEngine;

namespace XInputDotNetPure
{
	public struct GamePadThumbSticks
	{
		public struct StickValue
		{
			private Vector2 vector;

			public float X
			{
				get
				{
					return vector.x;
				}
			}

			public float Y
			{
				get
				{
					return vector.y;
				}
			}

			public Vector2 Vector
			{
				get
				{
					return vector;
				}
			}

			internal StickValue(float x, float y)
			{
				vector = new Vector2(x, y);
			}
		}

		private StickValue left;

		private StickValue right;

		public StickValue Left
		{
			get
			{
				return left;
			}
		}

		public StickValue Right
		{
			get
			{
				return right;
			}
		}

		internal GamePadThumbSticks(StickValue left, StickValue right)
		{
			this.left = left;
			this.right = right;
		}
	}
}
