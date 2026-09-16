using UnityEngine;

public class ArrayIndexAttribute : PropertyAttribute
{
	public SerializationUtils.RootType SearchRoot;

	public string ArrayPath = string.Empty;

	public string ScriptableObjectPath = string.Empty;

	public ArrayIndexAttribute(string _arrayPath)
	{
		ArrayPath = _arrayPath;
	}

	public ArrayIndexAttribute(string _arrayPath, string _scriptableObjectPath)
		: this(_arrayPath)
	{
		ScriptableObjectPath = _scriptableObjectPath;
	}

	public ArrayIndexAttribute(string _arrayPath, string _scriptableObjectPath, SerializationUtils.RootType _rootType)
		: this(_arrayPath, _scriptableObjectPath)
	{
		SearchRoot = _rootType;
	}
}
