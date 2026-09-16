public abstract class GamepadUser
{
	public enum ControlTypeEnum
	{
		Pad = 0,
		Keyboard = 1,
		Joycon = 2
	}

	public PadSide Side = PadSide.Both;

	public bool StickyEngagement;

	public virtual ControlTypeEnum ControlType
	{
		get
		{
			return ControlTypeEnum.Pad;
		}
	}

	public abstract string UID { get; }

	public abstract string DisplayName { get; }

	public override bool Equals(object obj)
	{
		if (!(obj is GamepadUser))
		{
			return false;
		}
		return (obj as GamepadUser).UID == UID;
	}

	public override int GetHashCode()
	{
		return UID.GetHashCode();
	}

	public static bool operator ==(GamepadUser _a, GamepadUser _b)
	{
		if ((object)_a == null || (object)_b == null)
		{
			return (object)_a == null && (object)_b == null;
		}
		return _a.Equals(_b);
	}

	public static bool operator !=(GamepadUser _a, GamepadUser _b)
	{
		return !(_a == _b);
	}
}
