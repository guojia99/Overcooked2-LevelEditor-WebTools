using System;
using UnityEngine;

[Serializable]
public class PopupData
{
	public enum Kind
	{
		SwitchKitchenTutorial = 0,
		DLC = 1,
		Update = 2
	}

	private const long k_dateMax = 3155378975999999999L;

	public Kind m_kind = Kind.DLC;

	public GameObject m_prefab;

	public string m_saveGameString;

	public DLCFrontendData m_dlcData;

	public long m_disableTicks = 3155378975999999999L;

	[Mask(typeof(PlatformUtils.Platforms))]
	public int m_platforms = -1;

	public string m_nameLocalisationKey;

	public string m_descriptionLocalisationKey;

	public Sprite m_image;

	public bool IsAvailableOnCurrentPlatform()
	{
		return (!m_dlcData) ? PlatformUtils.HasPlatformFlag(m_platforms) : m_dlcData.m_PC;
	}
}
