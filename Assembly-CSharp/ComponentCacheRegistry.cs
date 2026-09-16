using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ComponentCacheRegistry : Manager
{
	private static FastList<Type> m_cachedTypes = new FastList<Type>();

	private static IEnumerator m_scanRoutine = null;

	public static bool ScanActive
	{
		get
		{
			return m_scanRoutine != null;
		}
	}

	protected void Awake()
	{
		AddCacheScanType(typeof(Transform));
	}

	protected void OnDestroy()
	{
		Clear();
	}

	protected void Update()
	{
		if (m_scanRoutine != null && !m_scanRoutine.MoveNext())
		{
			m_scanRoutine = null;
		}
	}

	public static void ScanForInitialObjects(CallbackVoid _finished = null)
	{
		Clear();
		m_scanRoutine = BuildScanRoutine(m_cachedTypes.ToArray(), _finished);
	}

	private static IEnumerator BuildScanRoutine(Type[] scanTypes, CallbackVoid _finished)
	{
		HashSet<GameObject> cachedObjects = new HashSet<GameObject>();
		float yieldTime = Time.realtimeSinceStartup + 0.1f;
		GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
		for (int i = 0; i < rootObjects.Length; i++)
		{
			string rootObjName = rootObjects[i].gameObject.name;
			foreach (Type type in scanTypes)
			{
				if (rootObjects[i] == null)
				{
					break;
				}
				Component[] objects = rootObjects[i].GetComponentsInChildren(type, true);
				string[] objectNames = objects.ConvertAll((Component x) => x.gameObject.name);
				for (int j = 0; j < objects.Length; j++)
				{
					if (!(objects[j] == null) && !(objects[j].gameObject == null))
					{
						GameObject obj = objects[j].gameObject;
						if (!cachedObjects.Contains(obj))
						{
							UpdateObject(obj);
							cachedObjects.Add(obj);
						}
						if (Time.realtimeSinceStartup > yieldTime)
						{
							yield return null;
							yieldTime = Time.realtimeSinceStartup + 0.1f;
						}
					}
				}
			}
		}
		if (_finished != null)
		{
			_finished();
		}
	}

	private static void AddCacheScanType(Type _type)
	{
		if (!m_cachedTypes.Contains(_type))
		{
			m_cachedTypes.Add(_type);
		}
	}

	public static void UpdateObject(GameObject _object)
	{
		CachedObject component = _object.GetComponent<CachedObject>();
		if (component == null)
		{
			_object.AddComponent<CachedObject>();
		}
		ComponentCache<IGridLocation>.CacheObject(_object);
		ComponentCache<StaticGridLocation>.CacheObject(_object);
		ComponentCache<ClientInteractable>.CacheObject(_object);
		ComponentCache<IClientHandlePickup>.CacheObject(_object);
		ComponentCache<IClientHandlePlacement>.CacheObject(_object);
		ComponentCache<ClientHandlePickupReferral>.CacheObject(_object);
		ComponentCache<ClientHandlePlacementReferral>.CacheObject(_object);
		ComponentCache<ServerCatchableItem>.CacheObject(_object);
		ComponentCache<IThrowable>.CacheObject(_object);
		ComponentCache<IAttachment>.CacheObject(_object);
		ComponentCache<ServerFlammable>.CacheObject(_object);
		ComponentCache<IHandlePlacement>.CacheObject(_object);
		ComponentCache<ServerHandlePlacementReferral>.CacheObject(_object);
	}

	public static void EraseObject(GameObject _object)
	{
		ComponentCache<IGridLocation>.EraseObject(_object);
		ComponentCache<StaticGridLocation>.EraseObject(_object);
		ComponentCache<ClientInteractable>.EraseObject(_object);
		ComponentCache<IClientHandlePickup>.EraseObject(_object);
		ComponentCache<IClientHandlePlacement>.EraseObject(_object);
		ComponentCache<ClientHandlePickupReferral>.EraseObject(_object);
		ComponentCache<ClientHandlePlacementReferral>.EraseObject(_object);
		ComponentCache<ServerCatchableItem>.EraseObject(_object);
		ComponentCache<IThrowable>.EraseObject(_object);
		ComponentCache<IAttachment>.EraseObject(_object);
		ComponentCache<ServerFlammable>.EraseObject(_object);
		ComponentCache<IHandlePlacement>.EraseObject(_object);
		ComponentCache<ServerHandlePlacementReferral>.EraseObject(_object);
	}

	public static void Clear()
	{
		ComponentCache<IGridLocation>.Clear();
		ComponentCache<StaticGridLocation>.Clear();
		ComponentCache<ClientInteractable>.Clear();
		ComponentCache<IClientHandlePickup>.Clear();
		ComponentCache<IClientHandlePlacement>.Clear();
		ComponentCache<ClientHandlePickupReferral>.Clear();
		ComponentCache<ClientHandlePlacementReferral>.Clear();
		ComponentCache<ServerCatchableItem>.Clear();
		ComponentCache<IThrowable>.Clear();
		ComponentCache<IAttachment>.Clear();
		ComponentCache<ServerFlammable>.Clear();
		ComponentCache<IHandlePlacement>.Clear();
		ComponentCache<ServerHandlePlacementReferral>.Clear();
	}
}
