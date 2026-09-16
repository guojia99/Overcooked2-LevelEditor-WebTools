using System;
using UnityEngine;

[Serializable]
public class OptionalVector3 : Optional<Vector3>
{
	public OptionalVector3(Vector3 _value)
		: base(_value)
	{
	}

	public OptionalVector3()
	{
	}

	public static implicit operator OptionalVector3(Vector3 _value)
	{
		return new OptionalVector3(_value);
	}
}
