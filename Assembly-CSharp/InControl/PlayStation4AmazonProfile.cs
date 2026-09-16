namespace InControl
{
	[AutoDiscover]
	public class PlayStation4AmazonProfile : UnityInputDeviceProfile
	{
		public PlayStation4AmazonProfile()
		{
			base.Name = "PlayStation 4 Controller";
			base.Meta = "PlayStation 4 Controller on Amazon Fire TV";
			base.SupportedPlatforms = new string[1] { "Amazon AFT" };
			JoystickNames = new string[1] { "Sony Computer Entertainment Wireless Controller" };
			base.ButtonMappings = new InputControlMapping[11]
			{
				new InputControlMapping
				{
					Handle = "Cross",
					Target = InputControlType.Action1,
					Source = UnityInputDeviceProfile.Button0
				},
				new InputControlMapping
				{
					Handle = "Circle",
					Target = InputControlType.Action2,
					Source = UnityInputDeviceProfile.Button1
				},
				new InputControlMapping
				{
					Handle = "Square",
					Target = InputControlType.Action3,
					Source = UnityInputDeviceProfile.Button2
				},
				new InputControlMapping
				{
					Handle = "Triangle",
					Target = InputControlType.Action4,
					Source = UnityInputDeviceProfile.Button3
				},
				new InputControlMapping
				{
					Handle = "Left Bumper",
					Target = InputControlType.LeftBumper,
					Source = UnityInputDeviceProfile.Button4
				},
				new InputControlMapping
				{
					Handle = "Right Bumper",
					Target = InputControlType.RightBumper,
					Source = UnityInputDeviceProfile.Button5
				},
				new InputControlMapping
				{
					Handle = "Share",
					Target = InputControlType.Share,
					Source = UnityInputDeviceProfile.Button11
				},
				new InputControlMapping
				{
					Handle = "Options",
					Target = InputControlType.Options,
					Source = UnityInputDeviceProfile.Button10
				},
				new InputControlMapping
				{
					Handle = "Left Stick Button",
					Target = InputControlType.LeftStickButton,
					Source = UnityInputDeviceProfile.Button8
				},
				new InputControlMapping
				{
					Handle = "Right Stick Button",
					Target = InputControlType.RightStickButton,
					Source = UnityInputDeviceProfile.Button9
				},
				new InputControlMapping
				{
					Handle = "Menu",
					Target = InputControlType.Menu,
					Source = UnityInputDeviceProfile.MenuKey
				}
			};
			base.AnalogMappings = new InputControlMapping[14]
			{
				UnityInputDeviceProfile.LeftStickLeftMapping(UnityInputDeviceProfile.Analog0),
				UnityInputDeviceProfile.LeftStickRightMapping(UnityInputDeviceProfile.Analog0),
				UnityInputDeviceProfile.LeftStickUpMapping(UnityInputDeviceProfile.Analog1),
				UnityInputDeviceProfile.LeftStickDownMapping(UnityInputDeviceProfile.Analog1),
				UnityInputDeviceProfile.RightStickLeftMapping(UnityInputDeviceProfile.Analog2),
				UnityInputDeviceProfile.RightStickRightMapping(UnityInputDeviceProfile.Analog2),
				UnityInputDeviceProfile.RightStickUpMapping(UnityInputDeviceProfile.Analog3),
				UnityInputDeviceProfile.RightStickDownMapping(UnityInputDeviceProfile.Analog3),
				UnityInputDeviceProfile.DPadLeftMapping(UnityInputDeviceProfile.Analog4),
				UnityInputDeviceProfile.DPadRightMapping(UnityInputDeviceProfile.Analog4),
				UnityInputDeviceProfile.DPadUpMapping(UnityInputDeviceProfile.Analog5),
				UnityInputDeviceProfile.DPadDownMapping(UnityInputDeviceProfile.Analog5),
				new InputControlMapping
				{
					Handle = "Left Trigger",
					Target = InputControlType.LeftTrigger,
					Source = UnityInputDeviceProfile.Analog15,
					SourceRange = InputRange.ZeroToOne,
					TargetRange = InputRange.ZeroToOne
				},
				new InputControlMapping
				{
					Handle = "Right Trigger",
					Target = InputControlType.RightTrigger,
					Source = UnityInputDeviceProfile.Analog16,
					SourceRange = InputRange.ZeroToOne,
					TargetRange = InputRange.ZeroToOne
				}
			};
		}
	}
}
