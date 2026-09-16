using System;
using System.ComponentModel;
using System.Reflection;

public static class AttributesHelperExtension
{
	public static string ToDescription(this Enum value)
	{
		FieldInfo field = value.GetType().GetField(value.ToString());
		if (field != null)
		{
			DescriptionAttribute[] array = (DescriptionAttribute[])field.GetCustomAttributes(typeof(DescriptionAttribute), false);
			return (array.Length <= 0) ? value.ToString() : array[0].Description;
		}
		return value.ToString();
	}
}
