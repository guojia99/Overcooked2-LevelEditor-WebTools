using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameObjectUtils
{
	public static void SetObjectVisibility(this GameObject _obj, bool _isVisible)
	{
		if ((bool)_obj.GetComponent<Renderer>())
		{
			_obj.GetComponent<Renderer>().enabled = _isVisible;
		}
		for (int i = 0; i < _obj.transform.childCount; i++)
		{
			_obj.transform.GetChild(i).gameObject.SetObjectVisibility(_isVisible);
		}
	}

	public static void SetObjectLayer(this GameObject _obj, int _layer)
	{
		_obj.layer = _layer;
		for (int i = 0; i < _obj.transform.childCount; i++)
		{
			_obj.transform.GetChild(i).gameObject.SetObjectLayer(_layer);
		}
	}

	public static T RequestInterface<T>(this GameObject _obj) where T : class
	{
		return _obj.GetComponent<T>();
	}

	public static bool IsInHierarchyOf(this GameObject _obj, GameObject _parent)
	{
		if (_obj == _parent)
		{
			return true;
		}
		if (_obj.transform.parent != null)
		{
			return _obj.transform.parent.gameObject.IsInHierarchyOf(_parent);
		}
		return false;
	}

	public static T RequestComponentUpwardsRecursive<T>(this GameObject _obj) where T : Component
	{
		if (_obj.GetComponent<T>() != null)
		{
			return _obj.GetComponent<T>();
		}
		if (_obj.transform.parent != null)
		{
			return _obj.transform.parent.gameObject.RequestComponentUpwardsRecursive<T>();
		}
		return (T)null;
	}

	public static T RequireInterface<T>(this GameObject _obj) where T : class
	{
		T val = _obj.RequestInterface<T>();
		if (val != null)
		{
			return val;
		}
		return (T)null;
	}

	public static T[] RequestInterfaces<T>(this GameObject _obj) where T : class
	{
		return _obj.GetComponents<T>();
	}

	public static T RequestInterfaceRecursive<T>(this GameObject _obj) where T : class
	{
		return _obj.GetComponentInChildren<T>();
	}

	public static T RequireInterfaceRecursive<T>(this GameObject _obj) where T : class
	{
		T componentInChildren = _obj.GetComponentInChildren<T>();
		if (componentInChildren == null)
		{
		}
		return componentInChildren;
	}

	public static T RequestInterfaceUpwardsRecursive<T>(this GameObject _obj) where T : class
	{
		T component = _obj.GetComponent<T>();
		if (component != null)
		{
			return component;
		}
		Transform parent = _obj.transform.parent;
		if (parent != null)
		{
			return parent.gameObject.RequestInterfaceUpwardsRecursive<T>();
		}
		return (T)null;
	}

	public static T RequestComponent<T>(this GameObject _obj) where T : Component
	{
		return _obj.GetComponent<T>();
	}

	public static T[] RequestComponents<T>(this GameObject _obj) where T : Component
	{
		return _obj.GetComponents<T>();
	}

	public static T RequireComponent<T>(this GameObject _obj) where T : Component
	{
		T component = _obj.GetComponent<T>();
		if (component != null)
		{
			return component;
		}
		return (T)null;
	}

	public static T RequestComponentRecursive<T>(this GameObject _obj) where T : Component
	{
		T component = _obj.GetComponent<T>();
		if (component != null)
		{
			return component;
		}
		for (int i = 0; i < _obj.transform.childCount; i++)
		{
			T val = _obj.transform.GetChild(i).gameObject.RequestComponentRecursive<T>();
			if ((bool)val)
			{
				return val;
			}
		}
		return (T)null;
	}

	public static T RequireComponentRecursive<T>(this GameObject _obj) where T : Component
	{
		T val = _obj.RequestComponentRecursive<T>();
		if (val != null)
		{
			return val;
		}
		return (T)null;
	}

	public static T RequestInterfaceInImmediateChildren<T>(this GameObject _obj) where T : class
	{
		int childCount = _obj.transform.childCount;
		for (int i = 0; i < childCount; i++)
		{
			Transform child = _obj.transform.GetChild(i);
			T val = child.gameObject.RequestInterface<T>();
			if (val != null)
			{
				return val;
			}
		}
		return (T)null;
	}

	public static T RequestComponentInImmediateChildren<T>(this GameObject _obj) where T : Component
	{
		int childCount = _obj.transform.childCount;
		for (int i = 0; i < childCount; i++)
		{
			Transform child = _obj.transform.GetChild(i);
			T component = child.gameObject.GetComponent<T>();
			if (component != null)
			{
				return component;
			}
		}
		return (T)null;
	}

	public static T[] RequestComponentsInImmediateChildren<T>(this GameObject _obj) where T : Component
	{
		List<T> list = new List<T>();
		int childCount = _obj.transform.childCount;
		for (int i = 0; i < childCount; i++)
		{
			Transform child = _obj.transform.GetChild(i);
			T[] components = child.gameObject.GetComponents<T>();
			list.AddRange(components);
		}
		return list.ToArray();
	}

	public static T RequireComponentInImmediateChildren<T>(this GameObject _obj) where T : Component
	{
		T val = _obj.RequestComponentInImmediateChildren<T>();
		if (val != null)
		{
			return val;
		}
		return (T)null;
	}

	public static T[] RequestComponentsRecursive<T>(this GameObject _obj) where T : Component
	{
		List<T> list = new List<T>();
		_obj.BuildListOfComponentsRecursive(list);
		return list.ToArray();
	}

	private static void BuildListOfComponentsRecursive<T>(this GameObject _obj, List<T> _recursionList) where T : Component
	{
		T[] components = _obj.GetComponents<T>();
		_recursionList.AddRange(components);
		for (int i = 0; i < _obj.transform.childCount; i++)
		{
			_obj.transform.GetChild(i).gameObject.BuildListOfComponentsRecursive(_recursionList);
		}
	}

	public static T[] RequestInterfacesRecursive<T>(this GameObject _obj) where T : class
	{
		List<T> list = new List<T>();
		_obj.BuildListOfInterfacesRecursive(list);
		return list.ToArray();
	}

	private static void BuildListOfInterfacesRecursive<T>(this GameObject _obj, List<T> _recursionList) where T : class
	{
		T[] collection = _obj.RequestInterfaces<T>();
		_recursionList.AddRange(collection);
		for (int i = 0; i < _obj.transform.childCount; i++)
		{
			_obj.transform.GetChild(i).gameObject.BuildListOfInterfacesRecursive(_recursionList);
		}
	}

	public static T[] FindComponentsOfTypeInScene<T>() where T : Component
	{
		List<T> list = new List<T>();
		int sceneCount = SceneManager.sceneCount;
		for (int i = 0; i < sceneCount; i++)
		{
			Scene sceneAt = SceneManager.GetSceneAt(i);
			if (sceneAt.IsValid() && sceneAt.isLoaded)
			{
				GameObject[] rootGameObjects = sceneAt.GetRootGameObjects();
				for (int j = 0; j < rootGameObjects.Length; j++)
				{
					list.AddRange(rootGameObjects[j].GetComponentsInChildren<T>(true));
				}
			}
		}
		return list.ToArray();
	}

	public static GameObject InstantiateOnCamera(this GameObject _source)
	{
		return _source.InstantiateOnParent(Camera.main.transform);
	}

	public static GameObject InstantiateOnParent(this GameObject _source, Transform _parent, bool _worldPositionStays = true)
	{
		GameObject gameObject = Object.Instantiate(_source);
		Vector3 position = gameObject.transform.position;
		Quaternion rotation = gameObject.transform.rotation;
		gameObject.transform.SetParent(_parent, _worldPositionStays);
		gameObject.transform.localPosition = position;
		gameObject.transform.localRotation = rotation;
		gameObject.name = _source.name;
		return gameObject;
	}

	public static GameObject Instantiate(this GameObject _source, Transform _parent, Vector3 _localPosition, Quaternion _localRotation)
	{
		GameObject gameObject = Object.Instantiate(_source, _parent.TransformPoint(_localPosition), _parent.rotation * _localRotation);
		gameObject.transform.SetParent(_parent, true);
		gameObject.name = _source.name;
		return gameObject;
	}

	public static GameObject Instantiate(this GameObject _source, Vector3 _postion, Quaternion _rotation)
	{
		GameObject gameObject = Object.Instantiate(_source, _postion, _rotation);
		gameObject.name = _source.name;
		return gameObject;
	}

	public static GameObject CreateOnParent(GameObject _parent, string _name)
	{
		GameObject gameObject = new GameObject();
		gameObject.transform.SetParent((!(_parent != null)) ? null : _parent.transform, false);
		gameObject.transform.localPosition = Vector3.zero;
		gameObject.transform.localRotation = Quaternion.identity;
		gameObject.name = _name;
		return gameObject;
	}

	public static GameObject CreateOnParent<T>(GameObject _parent, string _name)
	{
		GameObject gameObject = new GameObject(_name, typeof(T));
		gameObject.transform.SetParent((!(_parent != null)) ? null : _parent.transform, false);
		gameObject.transform.localPosition = Vector3.zero;
		gameObject.transform.localRotation = Quaternion.identity;
		return gameObject;
	}

	public static Transform FindChildRecursive(this Transform parent, string name)
	{
		if (parent.name.Equals(name))
		{
			return parent;
		}
		for (int i = 0; i < parent.childCount; i++)
		{
			Transform transform = parent.GetChild(i).FindChildRecursive(name);
			if (transform != null)
			{
				return transform;
			}
		}
		return null;
	}

	public static Transform FindChildStartsWithRecursive(this Transform parent, string name, bool activeInHierarchyRequired = false)
	{
		if (parent.name.StartsWith(name) && (!activeInHierarchyRequired || parent.gameObject.activeInHierarchy))
		{
			return parent;
		}
		for (int i = 0; i < parent.childCount; i++)
		{
			Transform transform = parent.GetChild(i).FindChildStartsWithRecursive(name, activeInHierarchyRequired);
			if (transform != null)
			{
				return transform;
			}
		}
		return null;
	}

	public static Transform FindParentRecursive(this Transform child, string name)
	{
		if (child.parent != null)
		{
			if (child.parent.name.Equals(name))
			{
				return child.parent;
			}
			return child.parent.FindParentRecursive(name);
		}
		return null;
	}

	public static void Destroy(this GameObject _object)
	{
		_object.SendMessage("OnTrigger", "destroyed", SendMessageOptions.DontRequireReceiver);
		Object.Destroy(_object);
	}

	public static void DestroyImmediate(this GameObject _object)
	{
		_object.SendMessage("OnTrigger", "destroyed", SendMessageOptions.DontRequireReceiver);
		Object.DestroyImmediate(_object);
	}

	public static void SetRendering(this GameObject _object, bool _shouldRender)
	{
		Renderer[] array = _object.RequestComponentsRecursive<Renderer>();
		Light[] array2 = _object.RequestComponentsRecursive<Light>();
		ParticleSystem[] array3 = _object.RequestComponentsRecursive<ParticleSystem>();
		for (int i = 0; i < array.Length; i++)
		{
			array[i].enabled = _shouldRender;
		}
		for (int j = 0; j < array2.Length; j++)
		{
			array2[j].enabled = _shouldRender;
		}
		for (int k = 0; k < array3.Length; k++)
		{
			if (array3[k].isPlaying && !_shouldRender)
			{
				array3[k].Stop();
				array3[k].Clear();
			}
			if (_shouldRender && !array3[k].isPlaying)
			{
				array3[k].Play();
			}
		}
	}

	public static void DestroyChildren(this GameObject _object)
	{
		for (int i = 0; i < _object.transform.childCount; i++)
		{
			Object.Destroy(_object.transform.GetChild(i).gameObject);
		}
	}

	public static GameObject RequestChild(this GameObject _object, string _name)
	{
		Transform transform = _object.transform.Find(_name);
		if (transform != null)
		{
			return transform.gameObject;
		}
		return null;
	}

	public static GameObject RequestChildRecursive(this GameObject _object, string _name)
	{
		Transform transform = _object.transform.FindChildRecursive(_name);
		return transform.gameObject;
	}

	public static GameObject RequireChild(this GameObject _object, string _name)
	{
		return _object.RequestChild(_name);
	}

	public static void SendTrigger(this GameObject _target, string _trigger)
	{
		if (_trigger != string.Empty)
		{
			ITriggerReceiver[] array = _target.RequestInterfaces<ITriggerReceiver>();
			for (int i = 0; i < array.Length; i++)
			{
				array[i].OnTrigger(_trigger);
			}
		}
	}
}
