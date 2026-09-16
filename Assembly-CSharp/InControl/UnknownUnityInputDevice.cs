using UnityEngine;

namespace InControl
{
	public class UnknownUnityInputDevice : UnityInputDevice
	{
		internal float[] AnalogSnapshot { get; private set; }

		internal UnknownUnityInputDevice(InputDeviceProfile profile, int joystickId)
			: base(profile, joystickId)
		{
			AnalogSnapshot = new float[20];
		}

		internal void TakeSnapshot()
		{
			for (int i = 0; i < 20; i++)
			{
				InputControlType inputControlType = (InputControlType)(42 + i);
				float num = Utility.ApplySnapping(GetControl(inputControlType).RawValue, 0.5f);
				AnalogSnapshot[i] = num;
			}
		}

		internal UnknownDeviceControl GetFirstPressedAnalog()
		{
			for (int i = 0; i < 20; i++)
			{
				InputControlType inputControlType = (InputControlType)(42 + i);
				float num = Utility.ApplySnapping(GetControl(inputControlType).RawValue, 0.5f);
				float num2 = num - AnalogSnapshot[i];
				Debug.Log(num);
				Debug.Log(AnalogSnapshot[i]);
				Debug.Log(num2);
				if (num2 > 1.9f)
				{
					return new UnknownDeviceControl(inputControlType, InputRangeType.MinusOneToOne);
				}
				if (num2 < -0.9f)
				{
					return new UnknownDeviceControl(inputControlType, InputRangeType.ZeroToMinusOne);
				}
				if (num2 > 0.9f)
				{
					return new UnknownDeviceControl(inputControlType, InputRangeType.ZeroToOne);
				}
			}
			return UnknownDeviceControl.None;
		}

		internal UnknownDeviceControl GetFirstPressedButton()
		{
			for (int i = 0; i < 20; i++)
			{
				InputControlType inputControlType = (InputControlType)(62 + i);
				if (GetControl(inputControlType).IsPressed)
				{
					return new UnknownDeviceControl(inputControlType, InputRangeType.ZeroToOne);
				}
			}
			return UnknownDeviceControl.None;
		}
	}
}
