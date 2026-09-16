using UnityEngine;

namespace InControl
{
	public class UnityInputDevice : InputDevice
	{
		public const int MaxDevices = 11;

		public const int MaxButtons = 20;

		public const int MaxAnalogs = 20;

		internal int JoystickId { get; private set; }

		public InputDeviceProfile Profile { get; protected set; }

		public override bool IsSupportedOnThisPlatform
		{
			get
			{
				return Profile != null && Profile.IsSupportedOnThisPlatform;
			}
		}

		public override bool IsKnown
		{
			get
			{
				return Profile != null && Profile.IsKnown;
			}
		}

		public UnityInputDevice(InputDeviceProfile profile, int joystickId)
			: base(profile.Name)
		{
			Initialize(profile, joystickId);
		}

		public UnityInputDevice(InputDeviceProfile profile)
			: base(profile.Name)
		{
			Initialize(profile, 0);
		}

		private void Initialize(InputDeviceProfile profile, int joystickId)
		{
			Profile = profile;
			base.Meta = Profile.Meta;
			int analogCount = Profile.AnalogCount;
			for (int i = 0; i < analogCount; i++)
			{
				InputControlMapping inputControlMapping = Profile.AnalogMappings[i];
				InputControl inputControl = AddControl(inputControlMapping.Target, inputControlMapping.Handle);
				inputControl.Sensitivity = Mathf.Min(Profile.Sensitivity, inputControlMapping.Sensitivity);
				inputControl.LowerDeadZone = Mathf.Max(Profile.LowerDeadZone, inputControlMapping.LowerDeadZone);
				inputControl.UpperDeadZone = Mathf.Min(Profile.UpperDeadZone, inputControlMapping.UpperDeadZone);
				inputControl.Raw = inputControlMapping.Raw;
			}
			int buttonCount = Profile.ButtonCount;
			for (int j = 0; j < buttonCount; j++)
			{
				InputControlMapping inputControlMapping2 = Profile.ButtonMappings[j];
				AddControl(inputControlMapping2.Target, inputControlMapping2.Handle);
			}
			JoystickId = joystickId;
			if (joystickId != 0)
			{
				SortOrder = 100 + joystickId;
			}
		}

		public override void Update(ulong updateTick, float deltaTime)
		{
			if (Profile == null)
			{
				return;
			}
			int analogCount = Profile.AnalogCount;
			for (int i = 0; i < analogCount; i++)
			{
				InputControlMapping inputControlMapping = Profile.AnalogMappings[i];
				float value = inputControlMapping.Source.GetValue(this);
				InputControl control = GetControl(inputControlMapping.Target);
				if (!inputControlMapping.IgnoreInitialZeroValue || !control.IsOnZeroTick || !Utility.IsZero(value))
				{
					float value2 = inputControlMapping.MapValue(value);
					control.UpdateWithValue(value2, updateTick, deltaTime);
				}
			}
			int buttonCount = Profile.ButtonCount;
			for (int j = 0; j < buttonCount; j++)
			{
				InputControlMapping inputControlMapping2 = Profile.ButtonMappings[j];
				bool state = inputControlMapping2.Source.GetState(this);
				UpdateWithState(inputControlMapping2.Target, state, updateTick, deltaTime);
			}
		}
	}
}
