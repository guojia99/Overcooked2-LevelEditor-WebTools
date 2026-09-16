using System;
using UnityEngine;

namespace InControl
{
	public abstract class SingletonMonoBehavior<T> : MonoBehaviour where T : MonoBehaviour
	{
		private static T instance;

		private static bool hasInstance;

		private static object lockObject = new object();

		public static T Instance
		{
			get
			{
				return GetInstance();
			}
		}

		private static void CreateInstance()
		{
			GameObject gameObject = new GameObject();
			gameObject.name = typeof(T).ToString();
			Debug.Log("Creating instance of singleton: " + gameObject.name);
			instance = gameObject.AddComponent<T>();
			hasInstance = true;
		}

		private static T GetInstance()
		{
			lock (lockObject)
			{
				if (hasInstance)
				{
					return instance;
				}
				Type typeFromHandle = typeof(T);
				T[] array = UnityEngine.Object.FindObjectsOfType<T>();
				if (array.Length > 0)
				{
					instance = array[0];
					hasInstance = true;
					if (array.Length > 1)
					{
						Debug.LogWarning(string.Concat("Multiple instances of singleton ", typeFromHandle, " found; destroying all but the first."));
						for (int i = 1; i < array.Length; i++)
						{
							UnityEngine.Object.DestroyImmediate(array[i].gameObject);
						}
					}
					return instance;
				}
				SingletonPrefabAttribute singletonPrefabAttribute = Attribute.GetCustomAttribute(typeFromHandle, typeof(SingletonPrefabAttribute)) as SingletonPrefabAttribute;
				if (singletonPrefabAttribute == null)
				{
					CreateInstance();
				}
				else
				{
					string text = singletonPrefabAttribute.Name;
					GameObject gameObject = UnityEngine.Object.Instantiate(Resources.Load<GameObject>(text));
					if (gameObject == null)
					{
						Debug.LogError(string.Concat("Could not find prefab ", text, " for singleton of type ", typeFromHandle, "."));
						CreateInstance();
					}
					else
					{
						gameObject.name = text;
						instance = gameObject.GetComponent<T>();
						if (instance == null)
						{
							Debug.LogWarning(string.Concat("There wasn't a component of type \"", typeFromHandle, "\" inside prefab \"", text, "\"; creating one now."));
							instance = gameObject.AddComponent<T>();
							hasInstance = true;
						}
					}
				}
				return instance;
			}
		}

		private static void EnforceSingleton()
		{
			lock (lockObject)
			{
				if (!hasInstance)
				{
					return;
				}
				T[] array = UnityEngine.Object.FindObjectsOfType<T>();
				for (int i = 0; i < array.Length; i++)
				{
					if (array[i].GetInstanceID() != instance.GetInstanceID())
					{
						UnityEngine.Object.DestroyImmediate(array[i].gameObject);
					}
				}
			}
		}

		protected bool SetupSingleton()
		{
			EnforceSingleton();
			int instanceID = GetInstanceID();
			T val = Instance;
			return instanceID == val.GetInstanceID();
		}

		private void OnDestroy()
		{
			hasInstance = false;
		}
	}
}
