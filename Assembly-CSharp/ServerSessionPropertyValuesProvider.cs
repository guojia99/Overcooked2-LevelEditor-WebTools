using System;
using System.Collections.Generic;
using Team17.Online;

public static class ServerSessionPropertyValuesProvider
{
	private static bool m_bInit = false;

	private static OnlineMultiplayerSessionPropertyValue m_versionPropertyValue = new OnlineMultiplayerSessionPropertyValue();

	private static OnlineMultiplayerSessionPropertyValue m_gamemodePropertyValue = new OnlineMultiplayerSessionPropertyValue();

	private static List<OnlineMultiplayerSessionPropertyValue> m_sessionPropertyValues = new List<OnlineMultiplayerSessionPropertyValue>(Enum.GetNames(typeof(OnlineMultiplayerSessionPropertyId)).Length);

	public static bool Initialise(IOnlineMultiplayerSessionPropertyCoordinator propertyCoordinator)
	{
		if (!m_bInit && propertyCoordinator != null && propertyCoordinator.IsInitialized())
		{
			m_versionPropertyValue.m_value = OnlineMultiplayerConfig.CodeVersion;
			m_versionPropertyValue.m_property = propertyCoordinator.FindProperty(OnlineMultiplayerSessionPropertyId.eVersion);
			m_sessionPropertyValues.Add(m_versionPropertyValue);
			m_gamemodePropertyValue.m_value = 4u;
			m_gamemodePropertyValue.m_property = propertyCoordinator.FindProperty(OnlineMultiplayerSessionPropertyId.eGameMode);
			m_sessionPropertyValues.Add(m_gamemodePropertyValue);
			return true;
		}
		return false;
	}

	public static void SetGameMode(GameMode mode)
	{
		if (!m_bInit)
		{
			IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
			m_bInit = Initialise(onlinePlatformManager.OnlineMultiplayerSessionPropertyCoordinator());
		}
		m_gamemodePropertyValue.m_value = (uint)mode;
	}

	public static GameMode GetGameMode()
	{
		if (!m_bInit)
		{
			IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
			m_bInit = Initialise(onlinePlatformManager.OnlineMultiplayerSessionPropertyCoordinator());
		}
		return (GameMode)m_gamemodePropertyValue.m_value;
	}

	public static List<OnlineMultiplayerSessionPropertyValue> GetValues()
	{
		if (!m_bInit)
		{
			IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
			m_bInit = Initialise(onlinePlatformManager.OnlineMultiplayerSessionPropertyCoordinator());
		}
		return m_sessionPropertyValues;
	}
}
