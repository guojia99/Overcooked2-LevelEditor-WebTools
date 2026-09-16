using UnityEngine;

public class ArrayNamesAttribute : PropertyAttribute
{
	public string NameCalculatorMethod;

	public ArrayNamesAttribute(string _nameCalculatorMethod)
	{
		NameCalculatorMethod = _nameCalculatorMethod;
	}
}
