using System;
using UnityEngine;

[Serializable]
public class OptionalVector2 : Optional<Vector2>
{
	public OptionalVector2(Vector2 _value)
		: base(_value)
	{
	}

	public OptionalVector2()
	{
	}

	public static implicit operator OptionalVector2(Vector2 _value)
	{
		return new OptionalVector2(_value);
	}
}
