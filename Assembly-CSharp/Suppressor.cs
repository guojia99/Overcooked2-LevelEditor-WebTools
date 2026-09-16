using UnityEngine;

public class Suppressor
{
	private Object m_owner;

	public Suppressor(Object _owner)
	{
		m_owner = _owner;
	}

	public void Release()
	{
		m_owner = null;
	}

	public bool IsReleased()
	{
		return m_owner == null;
	}

	public bool IsSuppressedBy(Object _other)
	{
		return m_owner == _other;
	}
}
