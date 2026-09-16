using System;
using UnityEngine;

[Serializable]
public abstract class Optional<T>
{
	[SerializeField]
	[HideInInspectorTest("m_hasValue", true)]
	public T m_value = default(T);

	[SerializeField]
	protected bool m_hasValue;

	public T Value
	{
		get
		{
			return m_value;
		}
		set
		{
			m_value = value;
			m_hasValue = true;
		}
	}

	public bool HasValue
	{
		get
		{
			return m_hasValue;
		}
	}

	public Optional(T _value)
	{
		m_value = _value;
		m_hasValue = true;
	}

	public Optional()
	{
	}
}
