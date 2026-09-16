using System.Collections.Generic;

public class StaticList<T>
{
	private List<T> m_objects = new List<T>();

	public int Count
	{
		get
		{
			return m_objects.Count;
		}
	}

	public event VoidGeneric<int> OnObjectsChanged;

	public event VoidGeneric<T> OnObjectAdded;

	public event VoidGeneric<T> OnObjectRemoved;

	public IEnumerable<T> GetContents()
	{
		return m_objects;
	}

	public void Add(T _f)
	{
		m_objects.Add(_f);
		if (this.OnObjectsChanged != null)
		{
			this.OnObjectsChanged(m_objects.Count);
		}
		if (this.OnObjectAdded != null)
		{
			this.OnObjectAdded(_f);
		}
	}

	public void Remove(T _f)
	{
		m_objects.Remove(_f);
		if (this.OnObjectsChanged != null)
		{
			this.OnObjectsChanged(m_objects.Count);
		}
		if (this.OnObjectRemoved != null)
		{
			this.OnObjectRemoved(_f);
		}
	}
}
