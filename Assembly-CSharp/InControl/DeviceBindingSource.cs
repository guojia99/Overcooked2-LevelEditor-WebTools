using System.IO;
using UnityEngine;

namespace InControl
{
	public class DeviceBindingSource : BindingSource
	{
		public InputControlType Control { get; protected set; }

		public override string Name
		{
			get
			{
				if (base.BoundTo == null)
				{
					return string.Empty;
				}
				InputDevice device = base.BoundTo.Device;
				InputControl control = device.GetControl(Control);
				if (control == InputControl.Null)
				{
					return Control.ToString();
				}
				return device.GetControl(Control).Handle;
			}
		}

		public override string DeviceName
		{
			get
			{
				if (base.BoundTo == null)
				{
					return string.Empty;
				}
				InputDevice device = base.BoundTo.Device;
				if (device == InputDevice.Null)
				{
					return "Controller";
				}
				return device.Name;
			}
		}

		internal override BindingSourceType BindingSourceType
		{
			get
			{
				return BindingSourceType.DeviceBindingSource;
			}
		}

		internal override bool IsValid
		{
			get
			{
				if (base.BoundTo == null)
				{
					Debug.LogError("Cannot query property 'IsValid' for unbound BindingSource.");
					return false;
				}
				return base.BoundTo.Device.HasControl(Control) || Utility.TargetIsStandard(Control);
			}
		}

		internal DeviceBindingSource()
		{
			Control = InputControlType.None;
		}

		public DeviceBindingSource(InputControlType control)
		{
			Control = control;
		}

		public override float GetValue(InputDevice inputDevice)
		{
			if (inputDevice == null)
			{
				return 0f;
			}
			return inputDevice.GetControl(Control).Value;
		}

		public override bool GetState(InputDevice inputDevice)
		{
			if (inputDevice == null)
			{
				return false;
			}
			return inputDevice.GetControl(Control).State;
		}

		public override bool Equals(BindingSource other)
		{
			if (other == null)
			{
				return false;
			}
			DeviceBindingSource deviceBindingSource = other as DeviceBindingSource;
			if (deviceBindingSource != null)
			{
				return Control == deviceBindingSource.Control;
			}
			return false;
		}

		public override bool Equals(object other)
		{
			if (other == null)
			{
				return false;
			}
			DeviceBindingSource deviceBindingSource = other as DeviceBindingSource;
			if (deviceBindingSource != null)
			{
				return Control == deviceBindingSource.Control;
			}
			return false;
		}

		public override int GetHashCode()
		{
			return Control.GetHashCode();
		}

		internal override void Save(BinaryWriter writer)
		{
			writer.Write((int)Control);
		}

		internal override void Load(BinaryReader reader)
		{
			Control = (InputControlType)reader.ReadInt32();
		}
	}
}
