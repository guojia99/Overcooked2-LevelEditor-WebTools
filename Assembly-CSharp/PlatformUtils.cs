using UnityEngine;

public class PlatformUtils : MonoBehaviour
{
	public enum Platforms
	{
		PC = 0,
		XboxOne = 1,
		PS4 = 2,
		NX = 3
	}

	public enum OperatingSystem
	{
		Unknown = 0,
		Windows = 1,
		OSX = 2,
		Linux = 3
	}

	public static readonly int s_PlatformCount = 4;

	public static bool HasPlatformFlag(int _mask)
	{
		return MaskUtils.HasFlag(_mask, Platforms.PC);
	}

	public static Platforms GetCurrentPlatform()
	{
		return Platforms.PC;
	}

	public static bool HasOperatingSystemFlag(int _mask)
	{
		return MaskUtils.HasFlag(_mask, OperatingSystem.Windows);
	}

	public static OperatingSystem GetCurrentOperatingSystem()
	{
		return OperatingSystem.Windows;
	}
}
