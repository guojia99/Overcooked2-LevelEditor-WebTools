using System;

[Serializable]
public class OptionalFloat : Optional<float>
{
	public OptionalFloat(float _value)
		: base(_value)
	{
	}

	public OptionalFloat()
	{
	}

	public static implicit operator OptionalFloat(float _value)
	{
		return new OptionalFloat(_value);
	}
}
