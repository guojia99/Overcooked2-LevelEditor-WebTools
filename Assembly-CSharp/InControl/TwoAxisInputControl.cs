using UnityEngine;

namespace InControl
{
	public class TwoAxisInputControl : IInputControl
	{
		public float LowerDeadZone;

		public float UpperDeadZone = 1f;

		public bool Raw;

		private bool thisState;

		private bool lastState;

		private Vector2 thisValue;

		private Vector2 lastValue;

		private bool clearInputState;

		private float stateThreshold;

		public float X { get; protected set; }

		public float Y { get; protected set; }

		public OneAxisInputControl Left { get; protected set; }

		public OneAxisInputControl Right { get; protected set; }

		public OneAxisInputControl Up { get; protected set; }

		public OneAxisInputControl Down { get; protected set; }

		public ulong UpdateTick { get; protected set; }

		public float StateThreshold
		{
			get
			{
				return stateThreshold;
			}
			set
			{
				stateThreshold = value;
				Left.StateThreshold = value;
				Right.StateThreshold = value;
				Up.StateThreshold = value;
				Down.StateThreshold = value;
			}
		}

		public bool State
		{
			get
			{
				return thisState;
			}
		}

		public bool LastState
		{
			get
			{
				return lastState;
			}
		}

		public Vector2 Value
		{
			get
			{
				return thisValue;
			}
		}

		public Vector2 LastValue
		{
			get
			{
				return lastValue;
			}
		}

		public Vector2 Vector
		{
			get
			{
				return thisValue;
			}
		}

		public bool HasChanged { get; protected set; }

		public bool IsPressed
		{
			get
			{
				return thisState;
			}
		}

		public bool WasPressed
		{
			get
			{
				return thisState && !lastState;
			}
		}

		public bool WasReleased
		{
			get
			{
				return !thisState && lastState;
			}
		}

		public float Angle
		{
			get
			{
				return Utility.VectorToAngle(thisValue);
			}
		}

		public TwoAxisInputControl()
		{
			Left = new OneAxisInputControl();
			Right = new OneAxisInputControl();
			Up = new OneAxisInputControl();
			Down = new OneAxisInputControl();
		}

		public void ClearInputState()
		{
			Left.ClearInputState();
			Right.ClearInputState();
			Up.ClearInputState();
			Down.ClearInputState();
			lastState = false;
			lastValue = Vector2.zero;
			thisState = false;
			thisValue = Vector2.zero;
			clearInputState = true;
		}

		public void Filter(TwoAxisInputControl twoAxisInputControl, float deltaTime)
		{
			UpdateWithAxes(twoAxisInputControl.X, twoAxisInputControl.Y, InputManager.CurrentTick, deltaTime);
		}

		internal void UpdateWithAxes(float x, float y, ulong updateTick, float deltaTime)
		{
			lastState = thisState;
			lastValue = thisValue;
			thisValue = ((!Raw) ? Utility.ApplyCircularDeadZone(x, y, LowerDeadZone, UpperDeadZone) : new Vector2(x, y));
			X = thisValue.x;
			Y = thisValue.y;
			Left.CommitWithValue(Mathf.Max(0f, 0f - X), updateTick, deltaTime);
			Right.CommitWithValue(Mathf.Max(0f, X), updateTick, deltaTime);
			if (InputManager.InvertYAxis)
			{
				Up.CommitWithValue(Mathf.Max(0f, 0f - Y), updateTick, deltaTime);
				Down.CommitWithValue(Mathf.Max(0f, Y), updateTick, deltaTime);
			}
			else
			{
				Up.CommitWithValue(Mathf.Max(0f, Y), updateTick, deltaTime);
				Down.CommitWithValue(Mathf.Max(0f, 0f - Y), updateTick, deltaTime);
			}
			thisState = Up.State || Down.State || Left.State || Right.State;
			if (clearInputState)
			{
				lastState = thisState;
				lastValue = thisValue;
				clearInputState = false;
				HasChanged = false;
			}
			else if (thisValue != lastValue)
			{
				UpdateTick = updateTick;
				HasChanged = true;
			}
			else
			{
				HasChanged = false;
			}
		}

		public static implicit operator bool(TwoAxisInputControl instance)
		{
			return instance.thisState;
		}

		public static implicit operator Vector2(TwoAxisInputControl instance)
		{
			return instance.thisValue;
		}

		public static implicit operator Vector3(TwoAxisInputControl instance)
		{
			return instance.thisValue;
		}
	}
}
