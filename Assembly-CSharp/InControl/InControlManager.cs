using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InControl
{
	public class InControlManager : SingletonMonoBehavior<InControlManager>
	{
		public bool logDebugInfo;

		public bool invertYAxis;

		public bool useFixedUpdate;

		public bool dontDestroyOnLoad;

		public bool enableXInput;

		public int xInputUpdateRate;

		public int xInputBufferSize;

		public bool enableICade;

		public List<string> customProfiles = new List<string>();

		private void Awake()
		{
			if (!SetupSingleton())
			{
				return;
			}
			InputManager.InvertYAxis = invertYAxis;
			InputManager.EnableXInput = enableXInput;
			InputManager.XInputUpdateRate = (uint)Mathf.Max(xInputUpdateRate, 0);
			InputManager.XInputBufferSize = (uint)Mathf.Max(xInputBufferSize, 0);
			InputManager.EnableICade = enableICade;
			if (InputManager.SetupInternal())
			{
				if (logDebugInfo)
				{
					Debug.Log(string.Concat("InControl (version ", InputManager.Version, ")"));
					Logger.OnLogMessage += LogMessage;
				}
				foreach (string customProfile in customProfiles)
				{
					Type type = Type.GetType(customProfile);
					if (type == null)
					{
						Debug.LogError("Cannot find class for custom profile: " + customProfile);
						continue;
					}
					InputDeviceProfile inputDeviceProfile = Activator.CreateInstance(type) as InputDeviceProfile;
					if (inputDeviceProfile != null)
					{
						InputManager.AttachDevice(new UnityInputDevice(inputDeviceProfile));
					}
				}
			}
			if (dontDestroyOnLoad)
			{
				UnityEngine.Object.DontDestroyOnLoad(this);
			}
			SceneManager.sceneLoaded += OnSceneLoaded;
		}

		private void OnDestroy()
		{
			if (SingletonMonoBehavior<InControlManager>.Instance == this)
			{
				InputManager.ResetInternal();
			}
			SceneManager.sceneLoaded -= OnSceneLoaded;
		}

		private void Update()
		{
			if (!useFixedUpdate || Utility.IsZero(Time.timeScale))
			{
				InputManager.UpdateInternal();
			}
		}

		private void FixedUpdate()
		{
			if (useFixedUpdate)
			{
				InputManager.UpdateInternal();
			}
		}

		private void OnApplicationFocus(bool focusState)
		{
			InputManager.OnApplicationFocus(focusState);
		}

		private void OnApplicationPause(bool pauseState)
		{
			InputManager.OnApplicationPause(pauseState);
		}

		private void OnApplicationQuit()
		{
			InputManager.OnApplicationQuit();
		}

		private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			if (mode != LoadSceneMode.Additive)
			{
				InputManager.OnLevelWasLoaded();
			}
		}

		private void LogMessage(LogMessage logMessage)
		{
			switch (logMessage.type)
			{
			case LogMessageType.Info:
				Debug.Log(logMessage.text);
				break;
			case LogMessageType.Warning:
				Debug.LogWarning(logMessage.text);
				break;
			case LogMessageType.Error:
				Debug.LogError(logMessage.text);
				break;
			}
		}
	}
}
