using UnityEngine;

public class HideInInspectorTestAttribute : PropertyAttribute
{
	public struct HideTestPair
	{
		public string m_variableName;

		public object m_value;

		public HideTestPair(string _variableName, object _value)
		{
			m_variableName = _variableName;
			m_value = _value;
		}
	}

	public SerializationUtils.RootType SearchRoot;

	public HideTestPair[] Pairs;

	public HideInInspectorTestAttribute(string _variableName, object _value, SerializationUtils.RootType _rootType)
		: this(_variableName, _value)
	{
		SearchRoot = _rootType;
	}

	public HideInInspectorTestAttribute(string _variableName, object _value)
	{
		Pairs = new HideTestPair[1]
		{
			new HideTestPair(_variableName, _value)
		};
	}

	public HideInInspectorTestAttribute(string _variableName, object _value, string _variableName2, object _value2)
	{
		Pairs = new HideTestPair[2]
		{
			new HideTestPair(_variableName, _value),
			new HideTestPair(_variableName2, _value2)
		};
	}
}
