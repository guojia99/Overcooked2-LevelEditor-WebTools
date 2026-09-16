namespace InControl
{
	[AutoDiscover]
	public class SteelSeriesStratusXLAndroidProfile : UnityInputDeviceProfile
	{
		public SteelSeriesStratusXLAndroidProfile()
		{
			base.Name = "SteelSeries Stratus XL";
			base.Meta = "SteelSeries Stratus XL on Android";
			base.SupportedPlatforms = new string[1] { "Android" };
			JoystickNames = new string[1] { "SteelSeries Stratus XL" };
			base.ButtonMappings = new InputControlMapping[8]
			{
				new InputControlMapping
				{
					Handle = "A",
					Target = InputControlType.Action1,
					Source = UnityInputDeviceProfile.Button0
				},
				new InputControlMapping
				{
					Handle = "B",
					Target = InputControlType.Action2,
					Source = UnityInputDeviceProfile.Button1
				},
				new InputControlMapping
				{
					Handle = "X",
					Target = InputControlType.Action3,
					Source = UnityInputDeviceProfile.Button13
				},
				new InputControlMapping
				{
					Handle = "Y",
					Target = InputControlType.Action4,
					Source = UnityInputDeviceProfile.Button2
				},
				new InputControlMapping
				{
					Handle = "L1",
					Target = InputControlType.LeftBumper,
					Source = UnityInputDeviceProfile.Button3
				},
				new InputControlMapping
				{
					Handle = "R1",
					Target = InputControlType.RightBumper,
					Source = UnityInputDeviceProfile.Button14
				},
				new InputControlMapping
				{
					Handle = "L2",
					Target = InputControlType.LeftTrigger,
					Source = UnityInputDeviceProfile.Button4
				},
				new InputControlMapping
				{
					Handle = "R2",
					Target = InputControlType.RightTrigger,
					Source = UnityInputDeviceProfile.Button5
				}
			};
			base.AnalogMappings = new InputControlMapping[8]
			{
				UnityInputDeviceProfile.LeftStickLeftMapping(UnityInputDeviceProfile.Analog0),
				UnityInputDeviceProfile.LeftStickRightMapping(UnityInputDeviceProfile.Analog0),
				UnityInputDeviceProfile.LeftStickUpMapping(UnityInputDeviceProfile.Analog1),
				UnityInputDeviceProfile.LeftStickDownMapping(UnityInputDeviceProfile.Analog1),
				UnityInputDeviceProfile.RightStickLeftMapping(UnityInputDeviceProfile.Analog2),
				UnityInputDeviceProfile.RightStickRightMapping(UnityInputDeviceProfile.Analog2),
				UnityInputDeviceProfile.RightStickUpMapping(UnityInputDeviceProfile.Analog3),
				UnityInputDeviceProfile.RightStickDownMapping(UnityInputDeviceProfile.Analog3)
			};
			base.AnalogMappings[2].SourceRange = InputRange.ZeroToOne;
			base.AnalogMappings[3].SourceRange = InputRange.ZeroToMinusOne;
			base.AnalogMappings[6].SourceRange = InputRange.ZeroToOne;
			base.AnalogMappings[7].SourceRange = InputRange.ZeroToMinusOne;
		}
	}
}
