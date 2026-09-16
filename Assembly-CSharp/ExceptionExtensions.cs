using System;
using System.Reflection;

public static class ExceptionExtensions
{
	public static int HResultPublic(this Exception exception)
	{
		PropertyInfo[] array = exception.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FindAll((PropertyInfo x) => x.Name.Equals("HResult"));
		if (array.Length > 0)
		{
			return (int)array[0].GetValue(exception, null);
		}
		return 0;
	}
}
