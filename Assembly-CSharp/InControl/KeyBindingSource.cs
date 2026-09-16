using System.IO;

namespace InControl
{
	public class KeyBindingSource : BindingSource
	{
		public KeyCombo Control { get; protected set; }

		public override string Name
		{
			get
			{
				return Control.ToString();
			}
		}

		public override string DeviceName
		{
			get
			{
				return "Keyboard";
			}
		}

		internal override BindingSourceType BindingSourceType
		{
			get
			{
				return BindingSourceType.KeyBindingSource;
			}
		}

		internal KeyBindingSource()
		{
		}

		public KeyBindingSource(KeyCombo keyCombo)
		{
			Control = keyCombo;
		}

		public KeyBindingSource(params Key[] keys)
		{
			Control = new KeyCombo(keys);
		}

		public override float GetValue(InputDevice inputDevice)
		{
			return (!GetState(inputDevice)) ? 0f : 1f;
		}

		public override bool GetState(InputDevice inputDevice)
		{
			return Control.IsPressed;
		}

		public override bool Equals(BindingSource other)
		{
			if (other == null)
			{
				return false;
			}
			KeyBindingSource keyBindingSource = other as KeyBindingSource;
			if (keyBindingSource != null)
			{
				return Control == keyBindingSource.Control;
			}
			return false;
		}

		public override bool Equals(object other)
		{
			if (other == null)
			{
				return false;
			}
			KeyBindingSource keyBindingSource = other as KeyBindingSource;
			if (keyBindingSource != null)
			{
				return Control == keyBindingSource.Control;
			}
			return false;
		}

		public override int GetHashCode()
		{
			return Control.GetHashCode();
		}

		internal override void Load(BinaryReader reader)
		{
			KeyCombo control = default(KeyCombo);
			control.Load(reader);
			Control = control;
		}

		internal override void Save(BinaryWriter writer)
		{
			Control.Save(writer);
		}
	}
}
