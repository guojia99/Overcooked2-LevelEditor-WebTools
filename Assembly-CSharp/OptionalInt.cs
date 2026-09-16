using System;

[Serializable]
public class OptionalInt : Optional<int>
{
	public OptionalInt(int _value)
		: base(_value)
	{
	}

	public OptionalInt()
	{
	}

	public static implicit operator OptionalInt(int _value)
	{
		return new OptionalInt(_value);
	}
}
