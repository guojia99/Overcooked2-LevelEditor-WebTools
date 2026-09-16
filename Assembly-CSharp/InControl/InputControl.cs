namespace InControl
{
	public class InputControl : InputControlBase
	{
		public static readonly InputControl Null = new InputControl();

		private ulong zeroTick;

		public string Handle { get; protected set; }

		public InputControlType Target { get; protected set; }

		public bool IsButton { get; protected set; }

		public bool IsAnalog { get; protected set; }

		internal bool IsOnZeroTick
		{
			get
			{
				return base.UpdateTick == zeroTick;
			}
		}

		public bool IsStandard
		{
			get
			{
				return Utility.TargetIsStandard(Target);
			}
		}

		private InputControl()
		{
			Handle = "None";
			Target = InputControlType.None;
		}

		public InputControl(string handle, InputControlType target)
		{
			Handle = handle;
			Target = target;
			IsButton = Utility.TargetIsButton(target);
			IsAnalog = !IsButton;
		}

		internal void SetZeroTick()
		{
			zeroTick = base.UpdateTick;
		}
	}
}
