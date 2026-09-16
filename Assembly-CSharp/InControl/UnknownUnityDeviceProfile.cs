namespace InControl
{
	public sealed class UnknownUnityDeviceProfile : UnityInputDeviceProfile
	{
		public sealed override bool IsKnown
		{
			get
			{
				return false;
			}
		}

		public UnknownUnityDeviceProfile(string joystickName)
		{
			base.Name = "Unknown Controller";
			base.Meta = "\"" + joystickName + "\"";
			base.Sensitivity = 1f;
			base.LowerDeadZone = 0.2f;
			base.UpperDeadZone = 0.9f;
			base.SupportedPlatforms = null;
			JoystickNames = new string[1] { joystickName };
			base.AnalogMappings = new InputControlMapping[24];
			base.AnalogMappings[0] = UnityInputDeviceProfile.LeftStickLeftMapping(UnityInputDeviceProfile.Analog0);
			base.AnalogMappings[1] = UnityInputDeviceProfile.LeftStickRightMapping(UnityInputDeviceProfile.Analog0);
			base.AnalogMappings[2] = UnityInputDeviceProfile.LeftStickUpMapping(UnityInputDeviceProfile.Analog1);
			base.AnalogMappings[3] = UnityInputDeviceProfile.LeftStickDownMapping(UnityInputDeviceProfile.Analog1);
			for (int i = 0; i < 20; i++)
			{
				base.AnalogMappings[i + 4] = new InputControlMapping
				{
					Handle = "Analog " + i,
					Source = UnityInputDeviceProfile.Analog(i),
					Target = (InputControlType)(42 + i)
				};
			}
			base.ButtonMappings = new InputControlMapping[20];
			for (int j = 0; j < 20; j++)
			{
				base.ButtonMappings[j] = new InputControlMapping
				{
					Handle = "Button " + j,
					Source = UnityInputDeviceProfile.Button(j),
					Target = (InputControlType)(62 + j)
				};
			}
		}
	}
}
