using System;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field)]
public class MaskAttribute : PropertyAttribute
{
	public Type EnumType;

	public MaskAttribute(Type _enumType)
	{
		EnumType = _enumType;
	}
}
