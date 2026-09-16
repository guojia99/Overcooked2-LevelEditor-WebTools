using System;
using System.Collections.Generic;
using UnityEngine;

public class ManagerDirectory : MonoBehaviour
{
	private Dictionary<Type, Manager> m_allManagers = new Dictionary<Type, Manager>();

	private bool m_initialised;

	private void Awake()
	{
		CheckInitialised();
	}

	private void OnTransformChildrenChanged()
	{
		m_initialised = false;
		m_allManagers.Clear();
		CheckInitialised();
	}

	private void CheckInitialised()
	{
		if (m_initialised)
		{
			return;
		}
		Manager[] componentsInChildren = base.gameObject.GetComponentsInChildren<Manager>();
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			for (Type type = componentsInChildren[i].GetType(); type != null; type = type.BaseType)
			{
				m_allManagers.SafeAdd(type, componentsInChildren[i]);
			}
		}
		m_initialised = true;
	}

	public T RequireManager<T>() where T : Manager
	{
		return RequestManager<T>();
	}

	public T RequestManager<T>() where T : Manager
	{
		if (Application.isPlaying)
		{
			CheckInitialised();
			Manager value;
			m_allManagers.TryGetValue(typeof(T), out value);
			if (value != null)
			{
				return value as T;
			}
			return (T)null;
		}
		return base.gameObject.RequestComponentInImmediateChildren<T>();
	}

	public T RequestManagerInterface<T>() where T : class
	{
		if (Application.isPlaying)
		{
			CheckInitialised();
			foreach (KeyValuePair<Type, Manager> allManager in m_allManagers)
			{
				T val = allManager.Value.gameObject.RequestInterface<T>();
				if (val != null)
				{
					return val;
				}
			}
			return (T)null;
		}
		return base.gameObject.RequestInterfaceInImmediateChildren<T>();
	}

	public T RequireManagerInterface<T>() where T : class
	{
		return RequestManagerInterface<T>();
	}
}
