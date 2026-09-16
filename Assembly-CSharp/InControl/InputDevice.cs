using System;
using UnityEngine;

namespace InControl
{
	public class InputDevice
	{
		public static readonly InputDevice Null = new InputDevice("None");

		internal int SortOrder = int.MaxValue;

		public string Name { get; protected set; }

		public string Meta { get; protected set; }

		public ulong LastChangeTick { get; protected set; }

		public InputControl[] Controls { get; protected set; }

		public OneAxisInputControl LeftStickX { get; protected set; }

		public OneAxisInputControl LeftStickY { get; protected set; }

		public TwoAxisInputControl LeftStick { get; protected set; }

		public OneAxisInputControl RightStickX { get; protected set; }

		public OneAxisInputControl RightStickY { get; protected set; }

		public TwoAxisInputControl RightStick { get; protected set; }

		public OneAxisInputControl DPadX { get; protected set; }

		public OneAxisInputControl DPadY { get; protected set; }

		public TwoAxisInputControl DPad { get; protected set; }

		public InputControl Command { get; protected set; }

		public bool IsAttached { get; internal set; }

		internal bool RawSticks { get; set; }

		public virtual bool IsSupportedOnThisPlatform
		{
			get
			{
				return true;
			}
		}

		public virtual bool IsKnown
		{
			get
			{
				return true;
			}
		}

		public bool IsUnknown
		{
			get
			{
				return !IsKnown;
			}
		}

		public bool MenuWasPressed
		{
			get
			{
				return GetControl(InputControlType.Command).WasPressed;
			}
		}

		public InputControl AnyButton
		{
			get
			{
				int length = Controls.GetLength(0);
				for (int i = 0; i < length; i++)
				{
					InputControl inputControl = Controls[i];
					if (inputControl != null && inputControl.IsButton && inputControl.IsPressed)
					{
						return inputControl;
					}
				}
				return InputControl.Null;
			}
		}

		public InputControl LeftStickUp
		{
			get
			{
				return GetControl(InputControlType.LeftStickUp);
			}
		}

		public InputControl LeftStickDown
		{
			get
			{
				return GetControl(InputControlType.LeftStickDown);
			}
		}

		public InputControl LeftStickLeft
		{
			get
			{
				return GetControl(InputControlType.LeftStickLeft);
			}
		}

		public InputControl LeftStickRight
		{
			get
			{
				return GetControl(InputControlType.LeftStickRight);
			}
		}

		public InputControl RightStickUp
		{
			get
			{
				return GetControl(InputControlType.RightStickUp);
			}
		}

		public InputControl RightStickDown
		{
			get
			{
				return GetControl(InputControlType.RightStickDown);
			}
		}

		public InputControl RightStickLeft
		{
			get
			{
				return GetControl(InputControlType.RightStickLeft);
			}
		}

		public InputControl RightStickRight
		{
			get
			{
				return GetControl(InputControlType.RightStickRight);
			}
		}

		public InputControl DPadUp
		{
			get
			{
				return GetControl(InputControlType.DPadUp);
			}
		}

		public InputControl DPadDown
		{
			get
			{
				return GetControl(InputControlType.DPadDown);
			}
		}

		public InputControl DPadLeft
		{
			get
			{
				return GetControl(InputControlType.DPadLeft);
			}
		}

		public InputControl DPadRight
		{
			get
			{
				return GetControl(InputControlType.DPadRight);
			}
		}

		public InputControl Action1
		{
			get
			{
				return GetControl(InputControlType.Action1);
			}
		}

		public InputControl Action2
		{
			get
			{
				return GetControl(InputControlType.Action2);
			}
		}

		public InputControl Action3
		{
			get
			{
				return GetControl(InputControlType.Action3);
			}
		}

		public InputControl Action4
		{
			get
			{
				return GetControl(InputControlType.Action4);
			}
		}

		public InputControl LeftTrigger
		{
			get
			{
				return GetControl(InputControlType.LeftTrigger);
			}
		}

		public InputControl RightTrigger
		{
			get
			{
				return GetControl(InputControlType.RightTrigger);
			}
		}

		public InputControl LeftBumper
		{
			get
			{
				return GetControl(InputControlType.LeftBumper);
			}
		}

		public InputControl RightBumper
		{
			get
			{
				return GetControl(InputControlType.RightBumper);
			}
		}

		public InputControl LeftStickButton
		{
			get
			{
				return GetControl(InputControlType.LeftStickButton);
			}
		}

		public InputControl RightStickButton
		{
			get
			{
				return GetControl(InputControlType.RightStickButton);
			}
		}

		public TwoAxisInputControl Direction
		{
			get
			{
				return (DPad.UpdateTick <= LeftStick.UpdateTick) ? LeftStick : DPad;
			}
		}

		public InputDevice(string name)
		{
			Name = name;
			Meta = string.Empty;
			LastChangeTick = 0uL;
			Controls = new InputControl[83];
			LeftStickX = new OneAxisInputControl();
			LeftStickY = new OneAxisInputControl();
			LeftStick = new TwoAxisInputControl();
			RightStickX = new OneAxisInputControl();
			RightStickY = new OneAxisInputControl();
			RightStick = new TwoAxisInputControl();
			DPadX = new OneAxisInputControl();
			DPadY = new OneAxisInputControl();
			DPad = new TwoAxisInputControl();
			Command = AddControl(InputControlType.Command, "Command");
		}

		public bool HasControl(InputControlType inputControlType)
		{
			return Controls[(int)inputControlType] != null;
		}

		public InputControl GetControl(InputControlType inputControlType)
		{
			InputControl inputControl = Controls[(int)inputControlType];
			return inputControl ?? InputControl.Null;
		}

		public static InputControlType GetInputControlTypeByName(string inputControlName)
		{
			return (InputControlType)Enum.Parse(typeof(InputControlType), inputControlName);
		}

		public InputControl GetControlByName(string inputControlName)
		{
			InputControlType inputControlTypeByName = GetInputControlTypeByName(inputControlName);
			return GetControl(inputControlTypeByName);
		}

		public InputControl AddControl(InputControlType inputControlType, string handle)
		{
			InputControl inputControl = new InputControl(handle, inputControlType);
			Controls[(int)inputControlType] = inputControl;
			return inputControl;
		}

		public InputControl AddControl(InputControlType inputControlType, string handle, float lowerDeadZone, float upperDeadZone)
		{
			InputControl inputControl = AddControl(inputControlType, handle);
			inputControl.LowerDeadZone = lowerDeadZone;
			inputControl.UpperDeadZone = upperDeadZone;
			return inputControl;
		}

		public void ClearInputState()
		{
			LeftStickX.ClearInputState();
			LeftStickY.ClearInputState();
			LeftStick.ClearInputState();
			RightStickX.ClearInputState();
			RightStickY.ClearInputState();
			RightStick.ClearInputState();
			DPadX.ClearInputState();
			DPadY.ClearInputState();
			DPad.ClearInputState();
			int num = Controls.Length;
			for (int i = 0; i < num; i++)
			{
				InputControl inputControl = Controls[i];
				if (inputControl != null)
				{
					inputControl.ClearInputState();
				}
			}
		}

		internal void UpdateWithState(InputControlType inputControlType, bool state, ulong updateTick, float deltaTime)
		{
			GetControl(inputControlType).UpdateWithState(state, updateTick, deltaTime);
		}

		internal void UpdateWithValue(InputControlType inputControlType, float value, ulong updateTick, float deltaTime)
		{
			GetControl(inputControlType).UpdateWithValue(value, updateTick, deltaTime);
		}

		internal void UpdateLeftStickWithValue(Vector2 value, ulong updateTick, float deltaTime)
		{
			LeftStickLeft.UpdateWithValue(Mathf.Max(0f, 0f - value.x), updateTick, deltaTime);
			LeftStickRight.UpdateWithValue(Mathf.Max(0f, value.x), updateTick, deltaTime);
			if (InputManager.InvertYAxis)
			{
				LeftStickUp.UpdateWithValue(Mathf.Max(0f, 0f - value.y), updateTick, deltaTime);
				LeftStickDown.UpdateWithValue(Mathf.Max(0f, value.y), updateTick, deltaTime);
			}
			else
			{
				LeftStickUp.UpdateWithValue(Mathf.Max(0f, value.y), updateTick, deltaTime);
				LeftStickDown.UpdateWithValue(Mathf.Max(0f, 0f - value.y), updateTick, deltaTime);
			}
		}

		internal void UpdateLeftStickWithRawValue(Vector2 value, ulong updateTick, float deltaTime)
		{
			LeftStickLeft.UpdateWithRawValue(Mathf.Max(0f, 0f - value.x), updateTick, deltaTime);
			LeftStickRight.UpdateWithRawValue(Mathf.Max(0f, value.x), updateTick, deltaTime);
			if (InputManager.InvertYAxis)
			{
				LeftStickUp.UpdateWithRawValue(Mathf.Max(0f, 0f - value.y), updateTick, deltaTime);
				LeftStickDown.UpdateWithRawValue(Mathf.Max(0f, value.y), updateTick, deltaTime);
			}
			else
			{
				LeftStickUp.UpdateWithRawValue(Mathf.Max(0f, value.y), updateTick, deltaTime);
				LeftStickDown.UpdateWithRawValue(Mathf.Max(0f, 0f - value.y), updateTick, deltaTime);
			}
		}

		internal void CommitLeftStick()
		{
			LeftStickUp.Commit();
			LeftStickDown.Commit();
			LeftStickLeft.Commit();
			LeftStickRight.Commit();
		}

		internal void UpdateRightStickWithValue(Vector2 value, ulong updateTick, float deltaTime)
		{
			RightStickLeft.UpdateWithValue(Mathf.Max(0f, 0f - value.x), updateTick, deltaTime);
			RightStickRight.UpdateWithValue(Mathf.Max(0f, value.x), updateTick, deltaTime);
			if (InputManager.InvertYAxis)
			{
				RightStickUp.UpdateWithValue(Mathf.Max(0f, 0f - value.y), updateTick, deltaTime);
				RightStickDown.UpdateWithValue(Mathf.Max(0f, value.y), updateTick, deltaTime);
			}
			else
			{
				RightStickUp.UpdateWithValue(Mathf.Max(0f, value.y), updateTick, deltaTime);
				RightStickDown.UpdateWithValue(Mathf.Max(0f, 0f - value.y), updateTick, deltaTime);
			}
		}

		internal void UpdateRightStickWithRawValue(Vector2 value, ulong updateTick, float deltaTime)
		{
			RightStickLeft.UpdateWithRawValue(Mathf.Max(0f, 0f - value.x), updateTick, deltaTime);
			RightStickRight.UpdateWithRawValue(Mathf.Max(0f, value.x), updateTick, deltaTime);
			if (InputManager.InvertYAxis)
			{
				RightStickUp.UpdateWithRawValue(Mathf.Max(0f, 0f - value.y), updateTick, deltaTime);
				RightStickDown.UpdateWithRawValue(Mathf.Max(0f, value.y), updateTick, deltaTime);
			}
			else
			{
				RightStickUp.UpdateWithRawValue(Mathf.Max(0f, value.y), updateTick, deltaTime);
				RightStickDown.UpdateWithRawValue(Mathf.Max(0f, 0f - value.y), updateTick, deltaTime);
			}
		}

		internal void CommitRightStick()
		{
			RightStickUp.Commit();
			RightStickDown.Commit();
			RightStickLeft.Commit();
			RightStickRight.Commit();
		}

		public virtual void Update(ulong updateTick, float deltaTime)
		{
		}

		private bool AnyCommandControlIsPressed()
		{
			for (int i = 24; i <= 34; i++)
			{
				InputControl inputControl = Controls[i];
				if (inputControl != null && inputControl.IsPressed)
				{
					return true;
				}
			}
			return false;
		}

		internal void ProcessLeftStick(ulong updateTick, float deltaTime)
		{
			float x = Utility.ValueFromSides(LeftStickLeft.NextRawValue, LeftStickRight.NextRawValue);
			float y = Utility.ValueFromSides(LeftStickDown.NextRawValue, LeftStickUp.NextRawValue, InputManager.InvertYAxis);
			Vector2 vector;
			if (RawSticks)
			{
				vector = new Vector2(x, y);
			}
			else
			{
				float lowerDeadZone = Utility.Max(LeftStickLeft.LowerDeadZone, LeftStickRight.LowerDeadZone, LeftStickUp.LowerDeadZone, LeftStickDown.LowerDeadZone);
				float upperDeadZone = Utility.Min(LeftStickLeft.UpperDeadZone, LeftStickRight.UpperDeadZone, LeftStickUp.UpperDeadZone, LeftStickDown.UpperDeadZone);
				vector = Utility.ApplyCircularDeadZone(x, y, lowerDeadZone, upperDeadZone);
			}
			LeftStick.Raw = true;
			LeftStick.UpdateWithAxes(vector.x, vector.y, updateTick, deltaTime);
			LeftStickX.Raw = true;
			LeftStickX.CommitWithValue(vector.x, updateTick, deltaTime);
			LeftStickY.Raw = true;
			LeftStickY.CommitWithValue(vector.y, updateTick, deltaTime);
			LeftStickLeft.SetValue(LeftStick.Left.Value, updateTick);
			LeftStickRight.SetValue(LeftStick.Right.Value, updateTick);
			LeftStickUp.SetValue(LeftStick.Up.Value, updateTick);
			LeftStickDown.SetValue(LeftStick.Down.Value, updateTick);
		}

		internal void ProcessRightStick(ulong updateTick, float deltaTime)
		{
			float x = Utility.ValueFromSides(RightStickLeft.NextRawValue, RightStickRight.NextRawValue);
			float y = Utility.ValueFromSides(RightStickDown.NextRawValue, RightStickUp.NextRawValue, InputManager.InvertYAxis);
			Vector2 vector;
			if (RawSticks)
			{
				vector = new Vector2(x, y);
			}
			else
			{
				float lowerDeadZone = Utility.Max(RightStickLeft.LowerDeadZone, RightStickRight.LowerDeadZone, RightStickUp.LowerDeadZone, RightStickDown.LowerDeadZone);
				float upperDeadZone = Utility.Min(RightStickLeft.UpperDeadZone, RightStickRight.UpperDeadZone, RightStickUp.UpperDeadZone, RightStickDown.UpperDeadZone);
				vector = Utility.ApplyCircularDeadZone(x, y, lowerDeadZone, upperDeadZone);
			}
			RightStick.Raw = true;
			RightStick.UpdateWithAxes(vector.x, vector.y, updateTick, deltaTime);
			RightStickX.Raw = true;
			RightStickX.CommitWithValue(vector.x, updateTick, deltaTime);
			RightStickY.Raw = true;
			RightStickY.CommitWithValue(vector.y, updateTick, deltaTime);
			RightStickLeft.SetValue(RightStick.Left.Value, updateTick);
			RightStickRight.SetValue(RightStick.Right.Value, updateTick);
			RightStickUp.SetValue(RightStick.Up.Value, updateTick);
			RightStickDown.SetValue(RightStick.Down.Value, updateTick);
		}

		internal void ProcessDPad(ulong updateTick, float deltaTime)
		{
			float lowerDeadZone = Utility.Max(DPadLeft.LowerDeadZone, DPadRight.LowerDeadZone, DPadUp.LowerDeadZone, DPadDown.LowerDeadZone);
			float upperDeadZone = Utility.Min(DPadLeft.UpperDeadZone, DPadRight.UpperDeadZone, DPadUp.UpperDeadZone, DPadDown.UpperDeadZone);
			float x = Utility.ValueFromSides(DPadLeft.NextRawValue, DPadRight.NextRawValue);
			float y = Utility.ValueFromSides(DPadDown.NextRawValue, DPadUp.NextRawValue, InputManager.InvertYAxis);
			Vector2 vector = Utility.ApplyCircularDeadZone(x, y, lowerDeadZone, upperDeadZone);
			DPad.Raw = true;
			DPad.UpdateWithAxes(vector.x, vector.y, updateTick, deltaTime);
			DPadX.Raw = true;
			DPadX.CommitWithValue(vector.x, updateTick, deltaTime);
			DPadY.Raw = true;
			DPadY.CommitWithValue(vector.y, updateTick, deltaTime);
			DPadLeft.SetValue(DPad.Left.Value, updateTick);
			DPadRight.SetValue(DPad.Right.Value, updateTick);
			DPadUp.SetValue(DPad.Up.Value, updateTick);
			DPadDown.SetValue(DPad.Down.Value, updateTick);
		}

		public void Commit(ulong updateTick, float deltaTime)
		{
			ProcessLeftStick(updateTick, deltaTime);
			ProcessRightStick(updateTick, deltaTime);
			ProcessDPad(updateTick, deltaTime);
			int num = Controls.Length;
			for (int i = 0; i < num; i++)
			{
				InputControl inputControl = Controls[i];
				if (inputControl != null)
				{
					inputControl.Commit();
					if (inputControl.HasChanged)
					{
						LastChangeTick = updateTick;
					}
				}
			}
			if (IsKnown)
			{
				Command.CommitWithState(AnyCommandControlIsPressed(), updateTick, deltaTime);
			}
		}

		public bool LastChangedAfter(InputDevice otherDevice)
		{
			return LastChangeTick > otherDevice.LastChangeTick;
		}

		internal void RequestActivation()
		{
			LastChangeTick = InputManager.CurrentTick;
		}

		public virtual void Vibrate(float leftMotor, float rightMotor)
		{
		}

		public void Vibrate(float intensity)
		{
			Vibrate(intensity, intensity);
		}

		public void StopVibration()
		{
			Vibrate(0f);
		}

		public static implicit operator bool(InputDevice device)
		{
			return device != null;
		}
	}
}
