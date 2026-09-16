using UnityEngine;

namespace AssetBundles
{
	public class Utility
	{
		public const string AssetBundlesOutputPath = "AssetBundles";

		public static string GetPlatformName()
		{
			return GetPlatformForAssetBundles(Application.platform);
		}

		private static string GetPlatformForAssetBundles(RuntimePlatform platform)
		{
			switch (platform)
			{
			case RuntimePlatform.Android:
				return "Android";
			case RuntimePlatform.IPhonePlayer:
				return "iOS";
			case RuntimePlatform.WebGLPlayer:
				return "WebGL";
			case RuntimePlatform.WindowsPlayer:
				return "Windows";
			case RuntimePlatform.OSXPlayer:
				return "OSX";
			case RuntimePlatform.LinuxPlayer:
				return "Linux";
			case RuntimePlatform.XboxOne:
				return "XboxOne";
			case RuntimePlatform.PS4:
				return "PS4";
			case RuntimePlatform.Switch:
				return "Switch";
			default:
				return null;
			}
		}
	}
}
