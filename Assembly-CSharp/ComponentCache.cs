using System.Collections.Generic;
using UnityEngine;

public static class ComponentCache<T> where T : class
{
	public static Dictionary<GameObject, T> m_Component = new Dictionary<GameObject, T>();

	public static Dictionary<GameObject, T[]> m_Components = new Dictionary<GameObject, T[]>();

	public static void Clear()
	{
		m_Component.Clear();
		m_Components.Clear();
	}

	public static void CacheObject(GameObject _object)
	{
		if (!m_Component.ContainsKey(_object))
		{
			AddObject(_object);
		}
		UpdateCacheForObject(_object);
	}

	public static void EraseObject(GameObject _object)
	{
		if (m_Component.ContainsKey(_object))
		{
			RemoveObject(_object);
		}
	}

	public static T GetComponent(GameObject _object)
	{
		T value = (T)null;
		if (!m_Component.TryGetValue(_object, out value))
		{
			CacheObject(_object);
			if (!m_Component.TryGetValue(_object, out value))
			{
				value = _object.GetComponent<T>();
			}
		}
		return value;
	}

	public static T[] GetComponents(GameObject _object)
	{
		T[] value = null;
		if (!m_Components.TryGetValue(_object, out value))
		{
			CacheObject(_object);
			if (!m_Components.TryGetValue(_object, out value))
			{
				value = _object.GetComponents<T>();
			}
		}
		return value;
	}

	private static void AddObject(GameObject _object)
	{
		m_Component.Add(_object, (T)null);
		m_Components.Add(_object, new T[0]);
	}

	private static void RemoveObject(GameObject _object)
	{
		m_Component.Remove(_object);
		m_Components.Remove(_object);
	}

	private static void UpdateCacheForObject(GameObject _object)
	{
		m_Component[_object] = _object.GetComponent<T>();
		m_Components[_object] = _object.GetComponents<T>();
	}
}
