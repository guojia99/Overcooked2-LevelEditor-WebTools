using System;
using UnityEngine;

namespace InControl
{
	public abstract class InputControlBase : IInputControl
	{
		private float sensitivity = 1f;

		private float lowerDeadZone;

		private float upperDeadZone = 1f;

		private float stateThreshold;

		public float FirstRepeatDelay = 0.8f;

		public float RepeatDelay = 0.1f;

		public bool Raw;

		internal bool Enabled = true;

		private ulong pendingTick;

		private bool pendingCommit;

		private float nextRepeatTime;

		private float lastPressedTime;

		private bool wasRepeated;

		private bool clearInputState;

		private InputControlState lastState;

		private InputControlState nextState;

		private InputControlState thisState;

		public ulong UpdateTick { get; protected set; }

		public bool State
		{
			get
			{
				return Enabled && thisState.State;
			}
		}

		public bool LastState
		{
			get
			{
				return Enabled && lastState.State;
			}
		}

		public float Value
		{
			get
			{
				return (!Enabled) ? 0f : thisState.Value;
			}
		}

		public float LastValue
		{
			get
			{
				return (!Enabled) ? 0f : lastState.Value;
			}
		}

		public float RawValue
		{
			get
			{
				return (!Enabled) ? 0f : thisState.RawValue;
			}
		}

		internal float NextRawValue
		{
			get
			{
				return (!Enabled) ? 0f : nextState.RawValue;
			}
		}

		public bool HasChanged
		{
			get
			{
				return Enabled && thisState != lastState;
			}
		}

		public bool IsPressed
		{
			get
			{
				return Enabled && thisState.State;
			}
		}

		public bool WasPressed
		{
			get
			{
				return Enabled && (bool)thisState && !lastState;
			}
		}

		public bool WasReleased
		{
			get
			{
				return Enabled && !thisState && (bool)lastState;
			}
		}

		public bool WasRepeated
		{
			get
			{
				return Enabled && wasRepeated;
			}
		}

		public float Sensitivity
		{
			get
			{
				return sensitivity;
			}
			set
			{
				sensitivity = Mathf.Clamp01(value);
			}
		}

		public float LowerDeadZone
		{
			get
			{
				return lowerDeadZone;
			}
			set
			{
				lowerDeadZone = Mathf.Clamp01(value);
			}
		}

		public float UpperDeadZone
		{
			get
			{
				return upperDeadZone;
			}
			set
			{
				upperDeadZone = Mathf.Clamp01(value);
			}
		}

		public float StateThreshold
		{
			get
			{
				return stateThreshold;
			}
			set
			{
				stateThreshold = Mathf.Clamp01(value);
			}
		}

		private void PrepareForUpdate(ulong updateTick)
		{
			if (updateTick < pendingTick)
			{
				throw new InvalidOperationException("Cannot be updated with an earlier tick.");
			}
			if (pendingCommit && updateTick != pendingTick)
			{
				throw new InvalidOperationException("Cannot be updated for a new tick until pending tick is committed.");
			}
			if (updateTick > pendingTick)
			{
				lastState = thisState;
				nextState.Reset();
				pendingTick = updateTick;
				pendingCommit = true;
			}
		}

		public bool UpdateWithState(bool state, ulong updateTick, float deltaTime)
		{
			PrepareForUpdate(updateTick);
			nextState.Set(state || nextState.State);
			return state;
		}

		public bool UpdateWithValue(float value, ulong updateTick, float deltaTime)
		{
			PrepareForUpdate(updateTick);
			if (Utility.Abs(value) > Utility.Abs(nextState.RawValue))
			{
				nextState.RawValue = value;
				if (!Raw)
				{
					value = Utility.ApplyDeadZone(value, lowerDeadZone, upperDeadZone);
				}
				nextState.Set(value, stateThreshold);
				return true;
			}
			return false;
		}

		internal bool UpdateWithRawValue(float value, ulong updateTick, float deltaTime)
		{
			PrepareForUpdate(updateTick);
			if (Utility.Abs(value) > Utility.Abs(nextState.RawValue))
			{
				nextState.RawValue = value;
				nextState.Set(value, stateThreshold);
				return true;
			}
			return false;
		}

		internal void SetValue(float value, ulong updateTick)
		{
			if (updateTick > pendingTick)
			{
				lastState = thisState;
				nextState.Reset();
				pendingTick = updateTick;
				pendingCommit = true;
			}
			nextState.RawValue = value;
			nextState.Set(value, StateThreshold);
		}

		public void ClearInputState()
		{
			lastState.Reset();
			thisState.Reset();
			nextState.Reset();
			wasRepeated = false;
			clearInputState = true;
		}

		public void Commit()
		{
			pendingCommit = false;
			thisState = nextState;
			if (clearInputState)
			{
				lastState = nextState;
				UpdateTick = pendingTick;
				clearInputState = false;
				return;
			}
			bool state = lastState.State;
			bool state2 = thisState.State;
			wasRepeated = false;
			if (state && !state2)
			{
				nextRepeatTime = 0f;
			}
			else if (state2)
			{
				if (state != state2)
				{
					nextRepeatTime = Time.realtimeSinceStartup + FirstRepeatDelay;
				}
				else if (Time.realtimeSinceStartup >= nextRepeatTime)
				{
					wasRepeated = true;
					nextRepeatTime = Time.realtimeSinceStartup + RepeatDelay;
				}
			}
			if (thisState != lastState)
			{
				UpdateTick = pendingTick;
			}
		}

		public void CommitWithState(bool state, ulong updateTick, float deltaTime)
		{
			UpdateWithState(state, updateTick, deltaTime);
			Commit();
		}

		public void CommitWithValue(float value, ulong updateTick, float deltaTime)
		{
			UpdateWithValue(value, updateTick, deltaTime);
			Commit();
		}

		public static implicit operator bool(InputControlBase instance)
		{
			return instance.State;
		}

		public static implicit operator float(InputControlBase instance)
		{
			return instance.Value;
		}
	}
}
