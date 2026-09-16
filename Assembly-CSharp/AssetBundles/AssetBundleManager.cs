using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AssetBundles
{
	public class AssetBundleManager : MonoBehaviour
	{
		public enum LogMode
		{
			All = 0,
			JustErrors = 1
		}

		public enum LogType
		{
			Info = 0,
			Warning = 1,
			Error = 2
		}

		private static LogMode m_LogMode = LogMode.All;

		private static string m_BaseDownloadingURL = string.Empty;

		private static string[] m_ActiveVariants = new string[0];

		private static AssetBundleManifest m_AssetBundleManifest = null;

		private static Dictionary<string, LoadedAssetBundle> m_LoadedAssetBundles = new Dictionary<string, LoadedAssetBundle>();

		private static Dictionary<string, LoadingAssetBundle> m_LoadingAssetBundles = new Dictionary<string, LoadingAssetBundle>();

		private static Dictionary<string, string> m_LoadingErrors = new Dictionary<string, string>();

		private static Dictionary<string, string[]> m_Dependencies = new Dictionary<string, string[]>();

		private static List<AssetBundleLoadOperation> m_InProgressOperations = new List<AssetBundleLoadOperation>();

		private static AssetBundleManager m_Instance = null;

		public static LogMode logMode
		{
			get
			{
				return m_LogMode;
			}
			set
			{
				m_LogMode = value;
			}
		}

		public static string BaseDownloadingURL
		{
			get
			{
				return m_BaseDownloadingURL;
			}
			set
			{
				m_BaseDownloadingURL = value;
			}
		}

		public static string[] ActiveVariants
		{
			get
			{
				return m_ActiveVariants;
			}
			set
			{
				m_ActiveVariants = value;
			}
		}

		public static AssetBundleManifest AssetBundleManifestObject
		{
			get
			{
				return m_AssetBundleManifest;
			}
			set
			{
				m_AssetBundleManifest = value;
			}
		}

		public static string GetStreamingAssetsPath()
		{
			return Path.Combine(Application.streamingAssetsPath, "Windows");
		}

		private static void Log(LogType logType, string text)
		{
			if (logType == LogType.Error)
			{
				Debug.LogError("[AssetBundleManager] " + text);
			}
			else if (m_LogMode == LogMode.All)
			{
				Debug.Log("[AssetBundleManager] " + text);
			}
		}

		public static void SetSourceAssetBundleDirectory(string relativePath)
		{
			BaseDownloadingURL = GetStreamingAssetsPath() + relativePath;
		}

		public static void SetSourceAssetBundleURL(string absolutePath)
		{
			BaseDownloadingURL = absolutePath + Utility.GetPlatformName() + "/";
		}

		public static void SetDevelopmentAssetBundleServer()
		{
			TextAsset textAsset = Resources.Load("AssetBundleServerURL") as TextAsset;
			string text = ((!(textAsset != null)) ? null : textAsset.text.Trim());
			if (text == null || text.Length == 0)
			{
				Debug.LogError("Development Server URL could not be found.");
			}
			else
			{
				SetSourceAssetBundleURL(text);
			}
		}

		public static LoadedAssetBundle GetLoadedAssetBundle(string assetBundleName, out string error)
		{
			if (m_LoadingErrors.TryGetValue(assetBundleName, out error))
			{
				return null;
			}
			LoadedAssetBundle value = null;
			m_LoadedAssetBundles.TryGetValue(assetBundleName, out value);
			if (value == null)
			{
				return null;
			}
			string[] value2 = null;
			if (!m_Dependencies.TryGetValue(assetBundleName, out value2))
			{
				return value;
			}
			string[] array = value2;
			foreach (string key in array)
			{
				if (m_LoadingErrors.TryGetValue(assetBundleName, out error))
				{
					return value;
				}
				LoadedAssetBundle value3;
				m_LoadedAssetBundles.TryGetValue(key, out value3);
				if (value3 == null)
				{
					return null;
				}
			}
			return value;
		}

		public static AssetBundleLoadManifestOperation Initialize()
		{
			return Initialize(Utility.GetPlatformName());
		}

		public static AssetBundleLoadManifestOperation Initialize(string manifestAssetBundleName)
		{
			GameObject gameObject = new GameObject("AssetBundleManager", typeof(AssetBundleManager));
			UnityEngine.Object.DontDestroyOnLoad(gameObject);
			if (m_Instance != null && m_Instance.gameObject != gameObject)
			{
				m_Instance.gameObject.Destroy();
				m_Instance = null;
			}
			m_Instance = gameObject.GetComponent<AssetBundleManager>();
			LoadAssetBundle(manifestAssetBundleName, true, true);
			AssetBundleLoadManifestOperation assetBundleLoadManifestOperation = new AssetBundleLoadManifestOperation(manifestAssetBundleName, "AssetBundleManifest", typeof(AssetBundleManifest));
			m_InProgressOperations.Add(assetBundleLoadManifestOperation);
			return assetBundleLoadManifestOperation;
		}

		protected static void LoadAssetBundle(string assetBundleName, bool async, bool isLoadingAssetBundleManifest = false)
		{
			Log(LogType.Info, "Loading Asset Bundle " + ((!isLoadingAssetBundleManifest) ? ": " : "Manifest: ") + assetBundleName);
			if (!isLoadingAssetBundleManifest && m_AssetBundleManifest == null)
			{
				Debug.LogError("Please initialize AssetBundleManifest by calling AssetBundleManager.Initialize()");
				return;
			}
			LoadAssetBundleInternal(assetBundleName, async, isLoadingAssetBundleManifest);
			if (!isLoadingAssetBundleManifest)
			{
				LoadDependencies(assetBundleName, async);
			}
		}

		protected static string RemapVariantName(string assetBundleName)
		{
			string[] allAssetBundlesWithVariant = m_AssetBundleManifest.GetAllAssetBundlesWithVariant();
			string[] array = assetBundleName.Split('.');
			int num = int.MaxValue;
			int num2 = -1;
			for (int i = 0; i < allAssetBundlesWithVariant.Length; i++)
			{
				string[] array2 = allAssetBundlesWithVariant[i].Split('.');
				if (!(array2[0] != array[0]))
				{
					int num3 = Array.IndexOf(m_ActiveVariants, array2[1]);
					if (num3 == -1)
					{
						num3 = 2147483646;
					}
					if (num3 < num)
					{
						num = num3;
						num2 = i;
					}
				}
			}
			if (num == 2147483646)
			{
				Debug.LogWarning("Ambigious asset bundle variant chosen because there was no matching active variant: " + allAssetBundlesWithVariant[num2]);
			}
			if (num2 != -1)
			{
				return allAssetBundlesWithVariant[num2];
			}
			return assetBundleName;
		}

		protected static void LoadDependencies(string assetBundleName, bool async)
		{
			if (m_AssetBundleManifest == null)
			{
				Debug.LogError("Please initialize AssetBundleManifest by calling AssetBundleManager.Initialize()");
				return;
			}
			string[] allDependencies = m_AssetBundleManifest.GetAllDependencies(assetBundleName);
			if (allDependencies.Length != 0)
			{
				for (int i = 0; i < allDependencies.Length; i++)
				{
					allDependencies[i] = RemapVariantName(allDependencies[i]);
				}
				if (!m_Dependencies.ContainsKey(assetBundleName))
				{
					m_Dependencies.Add(assetBundleName, allDependencies);
				}
				for (int j = 0; j < allDependencies.Length; j++)
				{
					LoadAssetBundleInternal(allDependencies[j], async, false);
				}
			}
		}

		protected static bool LoadAssetBundleInternal(string assetBundleName, bool async, bool isLoadingAssetBundleManifest)
		{
			LoadedAssetBundle value = null;
			m_LoadedAssetBundles.TryGetValue(assetBundleName, out value);
			if (value != null)
			{
				value.m_ReferencedCount++;
				return true;
			}
			LoadingAssetBundle value2 = null;
			m_LoadingAssetBundles.TryGetValue(assetBundleName, out value2);
			if (value2 != null)
			{
				if (!async)
				{
					Debug.LogError(assetBundleName + " could not be loaded synchronously as the bundle is currently being async loaded!");
				}
				value2.m_ReferencedCount++;
				return true;
			}
			if (async)
			{
				Debug.Log(string.Format("Initializing file request with path : \"{0}\"", GetStreamingAssetsPath() + "/" + assetBundleName));
				m_Instance.StartCoroutine(m_Instance.LoadAssetBundleFromFileAsync(assetBundleName));
			}
			else
			{
				Debug.Log(string.Format("Initializing syncronous file request with path : \"{0}\"", GetStreamingAssetsPath() + "/" + assetBundleName));
				m_Instance.LoadAssetBundleFromFile(assetBundleName);
			}
			return false;
		}

		public static void UnloadAssetBundle(string assetBundleName)
		{
			UnloadAssetBundleInternal(assetBundleName);
			UnloadDependencies(assetBundleName);
		}

		protected static void UnloadDependencies(string assetBundleName)
		{
			string[] value = null;
			if (m_Dependencies.TryGetValue(assetBundleName, out value))
			{
				string[] array = value;
				foreach (string assetBundleName2 in array)
				{
					UnloadAssetBundleInternal(assetBundleName2);
				}
				LoadedAssetBundle value2 = null;
				m_LoadedAssetBundles.TryGetValue(assetBundleName, out value2);
				if (value2 == null)
				{
					m_Dependencies.Remove(assetBundleName);
				}
			}
		}

		protected static void UnloadAssetBundleInternal(string assetBundleName)
		{
			string error;
			LoadedAssetBundle loadedAssetBundle = GetLoadedAssetBundle(assetBundleName, out error);
			if (loadedAssetBundle == null)
			{
				LoadingAssetBundle value = null;
				m_LoadingAssetBundles.TryGetValue(assetBundleName, out value);
				if (value != null)
				{
					value.m_ReferencedCount--;
				}
			}
			else if (--loadedAssetBundle.m_ReferencedCount <= 0)
			{
				loadedAssetBundle.m_AssetBundle.Unload(true);
				m_LoadedAssetBundles.Remove(assetBundleName);
				Log(LogType.Info, assetBundleName + " has been unloaded successfully");
			}
		}

		public static AssetBundleLoadAssetOperation LoadAssetAsync(string assetBundleName, string assetName, Type type)
		{
			Log(LogType.Info, "Loading " + assetName + " from " + assetBundleName + " bundle");
			AssetBundleLoadAssetOperation assetBundleLoadAssetOperation = null;
			assetBundleName = RemapVariantName(assetBundleName);
			LoadAssetBundle(assetBundleName, true);
			assetBundleLoadAssetOperation = new AssetBundleLoadAssetOperationFull(assetBundleName, assetName, type);
			m_InProgressOperations.Add(assetBundleLoadAssetOperation);
			return assetBundleLoadAssetOperation;
		}

		public static AssetBundleLoadLevelOperationBase LoadLevelAsync(string assetBundleName, string levelName, bool isAdditive)
		{
			Log(LogType.Info, "Loading " + levelName + " from " + assetBundleName + " bundle");
			assetBundleName = assetBundleName.ToLowerInvariant();
			AssetBundleLoadLevelOperationBase assetBundleLoadLevelOperationBase = null;
			assetBundleName = RemapVariantName(assetBundleName);
			LoadAssetBundle(assetBundleName, true);
			assetBundleLoadLevelOperationBase = new AssetBundleLoadLevelOperation(assetBundleName, levelName, isAdditive);
			m_InProgressOperations.Add(assetBundleLoadLevelOperationBase);
			AssetBundleLoadLevelOperationBase assetBundleLoadLevelOperationBase2 = assetBundleLoadLevelOperationBase;
			assetBundleLoadLevelOperationBase2.OperationComplete = (AssetBundleLoadOperation.OperationCompleteDelegate)Delegate.Combine(assetBundleLoadLevelOperationBase2.OperationComplete, (AssetBundleLoadOperation.OperationCompleteDelegate)delegate
			{
				UpdateLoadedSceneBundles();
			});
			return assetBundleLoadLevelOperationBase;
		}

		public static void LoadLevel(string assetBundleName, string levelName, bool isAdditive)
		{
			Log(LogType.Info, "Loading " + levelName + " from " + assetBundleName + " bundle");
			assetBundleName = assetBundleName.ToLowerInvariant();
			assetBundleName = RemapVariantName(assetBundleName);
			LoadAssetBundle(assetBundleName, false);
			string error;
			LoadedAssetBundle loadedAssetBundle = GetLoadedAssetBundle(assetBundleName, out error);
			if (loadedAssetBundle != null)
			{
				if (isAdditive)
				{
					SceneManager.LoadScene(levelName, LoadSceneMode.Additive);
				}
				else
				{
					SceneManager.LoadScene(levelName, LoadSceneMode.Single);
				}
			}
			else
			{
				Debug.LogError(error);
			}
			UpdateLoadedSceneBundles();
		}

		protected static void UpdateLoadedSceneBundles()
		{
			List<string> list = new List<string>();
			int i = 0;
			for (int sceneCount = SceneManager.sceneCount; i < sceneCount; i++)
			{
				list.Add(SceneManager.GetSceneAt(i).name.ToLowerInvariant());
			}
			List<string> list2 = new List<string>();
			Dictionary<string, LoadedAssetBundle>.KeyCollection keys = m_LoadedAssetBundles.Keys;
			IEnumerator<string> enumerator = keys.GetEnumerator();
			while (enumerator.MoveNext())
			{
				string current = enumerator.Current;
				if (!list.Contains(current))
				{
					LoadedAssetBundle value = null;
					m_LoadedAssetBundles.TryGetValue(current, out value);
					if (value != null && value.m_AssetBundle != null && value.m_AssetBundle.isStreamedSceneAssetBundle)
					{
						list2.Add(current);
					}
				}
			}
			int j = 0;
			for (int count = list2.Count; j < count; j++)
			{
				UnloadAssetBundle(list2[j]);
			}
		}

		private IEnumerator LoadAssetBundleFromFileAsync(string assetBundleName)
		{
			AssetBundleCreateRequest request = AssetBundle.LoadFromFileAsync(GetStreamingAssetsPath() + "/" + assetBundleName);
			m_LoadingAssetBundles.Add(assetBundleName, new LoadingAssetBundle(request));
			yield return request;
			LoadedAssetBundle assetBundle = null;
			m_LoadedAssetBundles.TryGetValue(assetBundleName, out assetBundle);
			if (assetBundle == null || assetBundle.m_AssetBundle == null)
			{
				if (request.isDone)
				{
					AssetBundle assetBundle2 = request.assetBundle;
					if (assetBundle2 == null)
					{
						m_LoadingErrors.SafeAdd(assetBundleName, string.Format("{0} is not a valid asset bundle.", assetBundleName));
					}
					else
					{
						m_LoadedAssetBundles.Add(assetBundleName, new LoadedAssetBundle(assetBundle2));
					}
				}
				else
				{
					m_LoadingErrors.SafeAdd(assetBundleName, string.Format("Failed to complete request for bundle {0}.", assetBundleName));
				}
			}
			LoadedAssetBundle loadedBundle = null;
			m_LoadedAssetBundles.TryGetValue(assetBundleName, out loadedBundle);
			LoadingAssetBundle loadingBundle = null;
			m_LoadingAssetBundles.TryGetValue(assetBundleName, out loadingBundle);
			if (loadedBundle != null && loadingBundle != null)
			{
				loadedBundle.m_ReferencedCount += loadingBundle.m_ReferencedCount;
				if (loadedBundle.m_ReferencedCount <= 0)
				{
					UnloadAssetBundle(assetBundleName);
				}
			}
			m_LoadingAssetBundles.Remove(assetBundleName);
		}

		private void LoadAssetBundleFromFile(string assetBundleName)
		{
			AssetBundle assetBundle = AssetBundle.LoadFromFile(GetStreamingAssetsPath() + "/" + assetBundleName);
			if (assetBundle == null)
			{
				m_LoadingErrors.SafeAdd(assetBundleName, string.Format("{0} is not a valid asset bundle.", assetBundleName));
			}
			else
			{
				m_LoadedAssetBundles.Add(assetBundleName, new LoadedAssetBundle(assetBundle));
			}
		}

		private void Update()
		{
			int num = 0;
			while (num < m_InProgressOperations.Count)
			{
				if (!m_InProgressOperations[num].Update())
				{
					m_InProgressOperations.RemoveAt(num);
				}
				else
				{
					num++;
				}
			}
		}
	}
}
