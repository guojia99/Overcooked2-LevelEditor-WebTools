using System;
using System.Collections.Generic;
using UnityEngine;

namespace InControl
{
	public class UnityInputDeviceManager : InputDeviceManager
	{
		private const float deviceRefreshInterval = 1f;

		private float deviceRefreshTimer;

		private List<InputDeviceProfile> systemDeviceProfiles = new List<InputDeviceProfile>();

		private List<InputDeviceProfile> customDeviceProfiles = new List<InputDeviceProfile>();

		private string[] joystickNames;

		private int lastJoystickCount;

		private int lastJoystickHash;

		private int joystickCount;

		private int joystickHash;

		private bool JoystickInfoHasChanged
		{
			get
			{
				return joystickHash != lastJoystickHash || joystickCount != lastJoystickCount;
			}
		}

		public UnityInputDeviceManager()
		{
			AddSystemDeviceProfiles();
			QueryJoystickInfo();
			AttachDevices();
		}

		public override void Update(ulong updateTick, float deltaTime)
		{
			deviceRefreshTimer += deltaTime;
			if (deviceRefreshTimer >= 1f)
			{
				deviceRefreshTimer = 0f;
				QueryJoystickInfo();
				if (JoystickInfoHasChanged)
				{
					Logger.LogInfo("Change in attached Unity joysticks detected; refreshing device list.");
					DetachDevices();
					AttachDevices();
				}
			}
		}

		private void QueryJoystickInfo()
		{
			joystickNames = Input.GetJoystickNames();
			joystickCount = joystickNames.Length;
			if (joystickCount > 11)
			{
				Debug.LogWarning("Oli: Joytstick entries are present beyond the index  unity itself allows access too...");
				joystickCount = 11;
				Array.Resize(ref joystickNames, joystickCount);
			}
			joystickHash = 527 + joystickCount;
			for (int i = 0; i < joystickCount; i++)
			{
				joystickHash = joystickHash * 31 + joystickNames[i].GetHashCode();
			}
		}

		private void AttachDevices()
		{
			AttachKeyboardDevices();
			AttachJoystickDevices();
			lastJoystickCount = joystickCount;
			lastJoystickHash = joystickHash;
		}

		private void DetachDevices()
		{
			int count = devices.Count;
			for (int i = 0; i < count; i++)
			{
				InputManager.DetachDevice(devices[i]);
			}
			devices.Clear();
		}

		public void ReloadDevices()
		{
			QueryJoystickInfo();
			DetachDevices();
			AttachDevices();
		}

		private void AttachDevice(UnityInputDevice device)
		{
			devices.Add(device);
			InputManager.AttachDevice(device);
		}

		private void AttachKeyboardDevices()
		{
			int count = systemDeviceProfiles.Count;
			for (int i = 0; i < count; i++)
			{
				InputDeviceProfile inputDeviceProfile = systemDeviceProfiles[i];
				if (inputDeviceProfile.IsNotJoystick && inputDeviceProfile.IsSupportedOnThisPlatform)
				{
					AttachDevice(new UnityInputDevice(inputDeviceProfile));
				}
			}
		}

		private void AttachJoystickDevices()
		{
			try
			{
				for (int i = 0; i < joystickCount; i++)
				{
					DetectJoystickDevice(i + 1, joystickNames[i]);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex.Message);
				Logger.LogError(ex.StackTrace);
			}
		}

		private bool HasAttachedDeviceWithJoystickId(int unityJoystickId)
		{
			int count = devices.Count;
			for (int i = 0; i < count; i++)
			{
				UnityInputDevice unityInputDevice = devices[i] as UnityInputDevice;
				if (unityInputDevice != null && unityInputDevice.JoystickId == unityJoystickId)
				{
					return true;
				}
			}
			return false;
		}

		private void DetectJoystickDevice(int unityJoystickId, string unityJoystickName)
		{
			if (HasAttachedDeviceWithJoystickId(unityJoystickId) || unityJoystickName.IndexOf("webcam", StringComparison.OrdinalIgnoreCase) != -1 || (InputManager.UnityVersion < new VersionInfo(4, 5, 0, 0) && (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer) && unityJoystickName == "Unknown Wireless Controller") || (InputManager.UnityVersion >= new VersionInfo(4, 6, 3, 0) && (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer) && string.IsNullOrEmpty(unityJoystickName)))
			{
				return;
			}
			InputDeviceProfile inputDeviceProfile = null;
			if (inputDeviceProfile == null)
			{
				inputDeviceProfile = customDeviceProfiles.Find((InputDeviceProfile config) => config.HasJoystickName(unityJoystickName));
			}
			if (inputDeviceProfile == null)
			{
				inputDeviceProfile = systemDeviceProfiles.Find((InputDeviceProfile config) => config.HasJoystickName(unityJoystickName));
			}
			if (inputDeviceProfile == null)
			{
				inputDeviceProfile = customDeviceProfiles.Find((InputDeviceProfile config) => config.HasLastResortRegex(unityJoystickName));
			}
			if (inputDeviceProfile == null)
			{
				inputDeviceProfile = systemDeviceProfiles.Find((InputDeviceProfile config) => config.HasLastResortRegex(unityJoystickName));
			}
			if (inputDeviceProfile == null)
			{
				Logger.LogWarning("Device " + unityJoystickId + " with name \"" + unityJoystickName + "\" does not match any supported profiles and will be considered an unknown controller.");
				UnknownUnityDeviceProfile profile = new UnknownUnityDeviceProfile(unityJoystickName);
				UnknownUnityInputDevice device = new UnknownUnityInputDevice(profile, unityJoystickId);
				AttachDevice(device);
			}
			else if (!inputDeviceProfile.IsHidden)
			{
				UnityInputDevice device2 = new UnityInputDevice(inputDeviceProfile, unityJoystickId);
				AttachDevice(device2);
				Logger.LogInfo("Device " + unityJoystickId + " matched profile " + inputDeviceProfile.GetType().Name + " (" + inputDeviceProfile.Name + ")");
			}
			else
			{
				Logger.LogInfo("Device " + unityJoystickId + " matching profile " + inputDeviceProfile.GetType().Name + " (" + inputDeviceProfile.Name + ") is hidden and will not be attached.");
			}
		}

		private void AddSystemDeviceProfile(UnityInputDeviceProfile deviceProfile)
		{
			if (deviceProfile.IsSupportedOnThisPlatform)
			{
				systemDeviceProfiles.Add(deviceProfile);
			}
		}

		private void AddSystemDeviceProfiles()
		{
			string[] profiles = UnityInputDeviceProfileList.Profiles;
			foreach (string typeName in profiles)
			{
				UnityInputDeviceProfile deviceProfile = (UnityInputDeviceProfile)Activator.CreateInstance(Type.GetType(typeName));
				AddSystemDeviceProfile(deviceProfile);
			}
		}
	}
}
