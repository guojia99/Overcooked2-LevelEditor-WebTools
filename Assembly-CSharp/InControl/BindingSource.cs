using System;
using System.IO;

namespace InControl
{
	public abstract class BindingSource : InputControlSource, IEquatable<BindingSource>
	{
		public abstract string Name { get; }

		public abstract string DeviceName { get; }

		internal abstract BindingSourceType BindingSourceType { get; }

		internal PlayerAction BoundTo { get; set; }

		internal virtual bool IsValid
		{
			get
			{
				return true;
			}
		}

		public abstract float GetValue(InputDevice inputDevice);

		public abstract bool GetState(InputDevice inputDevice);

		public abstract bool Equals(BindingSource other);

		public static bool operator ==(BindingSource a, BindingSource b)
		{
			if (object.ReferenceEquals(a, b))
			{
				return true;
			}
			if ((object)a == null || (object)b == null)
			{
				return false;
			}
			if (a.BindingSourceType != b.BindingSourceType)
			{
				return false;
			}
			return a.Equals(b);
		}

		public static bool operator !=(BindingSource a, BindingSource b)
		{
			return !(a == b);
		}

		public override bool Equals(object other)
		{
			return Equals((BindingSource)other);
		}

		public override int GetHashCode()
		{
			return base.GetHashCode();
		}

		internal abstract void Save(BinaryWriter writer);

		internal abstract void Load(BinaryReader reader);
	}
}
