using System.Runtime.InteropServices;
using UnityEngine;

public class WindowsAccessibility : MonoBehaviour
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
	public struct SKEY
	{
		public uint cbSize;

		public uint dwFlags;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
	public struct FILTERKEY
	{
		public uint cbSize;

		public uint dwFlags;

		public uint iWaitMSec;

		public uint iDelayMSec;

		public uint iRepeatMSec;

		public uint iBounceMSec;
	}

	private const uint SPI_GETFILTERKEYS = 50u;

	private const uint SPI_SETFILTERKEYS = 51u;

	private const uint SPI_GETTOGGLEKEYS = 52u;

	private const uint SPI_SETTOGGLEKEYS = 53u;

	private const uint SPI_GETSTICKYKEYS = 58u;

	private const uint SPI_SETSTICKYKEYS = 59u;

	private static bool StartupAccessibilitySet;

	private static SKEY StartupStickyKeys;

	private static SKEY StartupToggleKeys;

	private static FILTERKEY StartupFilterKeys;

	private const uint SKF_STICKYKEYSON = 1u;

	private const uint TKF_TOGGLEKEYSON = 1u;

	private const uint SKF_CONFIRMHOTKEY = 8u;

	private const uint SKF_HOTKEYACTIVE = 4u;

	private const uint TKF_CONFIRMHOTKEY = 8u;

	private const uint TKF_HOTKEYACTIVE = 4u;

	private const uint FKF_CONFIRMHOTKEY = 8u;

	private const uint FKF_HOTKEYACTIVE = 4u;

	private static uint SKEYSize = 8u;

	private static uint FKEYSize = 24u;

	[DllImport("user32.dll")]
	private static extern bool SystemParametersInfo(uint action, uint param, ref SKEY vparam, uint init);

	[DllImport("user32.dll")]
	private static extern bool SystemParametersInfo(uint action, uint param, ref FILTERKEY vparam, uint init);

	public static void ToggleAccessibilityShortcutKeys(bool ReturnToStarting)
	{
		if (!StartupAccessibilitySet)
		{
			StartupStickyKeys.cbSize = SKEYSize;
			StartupToggleKeys.cbSize = SKEYSize;
			StartupFilterKeys.cbSize = FKEYSize;
			SystemParametersInfo(58u, SKEYSize, ref StartupStickyKeys, 0u);
			SystemParametersInfo(52u, SKEYSize, ref StartupToggleKeys, 0u);
			SystemParametersInfo(50u, FKEYSize, ref StartupFilterKeys, 0u);
			StartupAccessibilitySet = true;
		}
		if (ReturnToStarting)
		{
			SystemParametersInfo(59u, SKEYSize, ref StartupStickyKeys, 0u);
			SystemParametersInfo(53u, SKEYSize, ref StartupToggleKeys, 0u);
			SystemParametersInfo(51u, FKEYSize, ref StartupFilterKeys, 0u);
			return;
		}
		SKEY vparam = StartupStickyKeys;
		vparam.dwFlags &= 4294967291u;
		vparam.dwFlags &= 4294967287u;
		SystemParametersInfo(59u, SKEYSize, ref vparam, 0u);
		SKEY vparam2 = StartupToggleKeys;
		vparam2.dwFlags &= 4294967291u;
		vparam2.dwFlags &= 4294967287u;
		SystemParametersInfo(53u, SKEYSize, ref vparam2, 0u);
		FILTERKEY vparam3 = StartupFilterKeys;
		vparam3.dwFlags &= 4294967291u;
		vparam3.dwFlags &= 4294967287u;
		SystemParametersInfo(51u, FKEYSize, ref vparam3, 0u);
	}
}
