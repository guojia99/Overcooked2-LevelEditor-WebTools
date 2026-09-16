using System;
using UnityEngine;

namespace InControl
{
	[Obsolete("Custom profiles are deprecated. Use the bindings API instead.", false)]
	public class CustomInputDeviceProfile : InputDeviceProfile
	{
		public sealed override bool IsKnown
		{
			get
			{
				return true;
			}
		}

		public sealed override bool IsJoystick
		{
			get
			{
				return false;
			}
		}

		public CustomInputDeviceProfile()
		{
			base.Name = "Custom Device Profile";
			base.Meta = "Custom Device Profile";
			base.SupportedPlatforms = new string[3] { "Windows", "Mac", "Linux" };
			base.Sensitivity = 1f;
			base.LowerDeadZone = 0f;
			base.UpperDeadZone = 1f;
		}

		public sealed override bool HasJoystickName(string joystickName)
		{
			return false;
		}

		public sealed override bool HasLastResortRegex(string joystickName)
		{
			return false;
		}

		public sealed override bool HasJoystickOrRegexName(string joystickName)
		{
			return false;
		}

		protected static InputControlSource KeyCodeButton(params KeyCode[] keyCodeList)
		{
			return new UnityKeyCodeSource(keyCodeList);
		}

		protected static InputControlSource KeyCodeComboButton(params KeyCode[] keyCodeList)
		{
			return new UnityKeyCodeComboSource(keyCodeList);
		}
	}
}
