using System;
using System.Collections.Generic;
using Team17.Online;

public static class SearchSessionPropertyValuesProvider
{
	private static bool m_bInit = false;

	private static OnlineMultiplayerSessionPropertySearchValue m_versionPropertyValue = new OnlineMultiplayerSessionPropertySearchValue();

	private static OnlineMultiplayerSessionPropertySearchValue m_gamemodePropertyValue = new OnlineMultiplayerSessionPropertySearchValue();

	private static List<OnlineMultiplayerSessionPropertySearchValue> m_searchPropertyValues = new List<OnlineMultiplayerSessionPropertySearchValue>(Enum.GetNames(typeof(OnlineMultiplayerSessionPropertyId)).Length);

	public static bool Initialise(IOnlineMultiplayerSessionPropertyCoordinator sessionPropertyCoordinator)
	{
		if (!m_bInit && sessionPropertyCoordinator != null && sessionPropertyCoordinator.IsInitialized())
		{
			m_versionPropertyValue.m_value = OnlineMultiplayerConfig.CodeVersion;
			m_versionPropertyValue.m_valueMinRange = OnlineMultiplayerConfig.CodeVersion;
			m_versionPropertyValue.m_valueMaxRange = OnlineMultiplayerConfig.CodeVersion;
			m_versionPropertyValue.m_property = sessionPropertyCoordinator.FindProperty(OnlineMultiplayerSessionPropertyId.eVersion);
			m_versionPropertyValue.m_operator = OnlineMultiplayerSessionPropertySearchValue.Operator.eEquals;
			m_searchPropertyValues.Add(m_versionPropertyValue);
			m_gamemodePropertyValue.m_value = 4u;
			m_gamemodePropertyValue.m_valueMinRange = 4u;
			m_gamemodePropertyValue.m_valueMaxRange = 4u;
			m_gamemodePropertyValue.m_property = sessionPropertyCoordinator.FindProperty(OnlineMultiplayerSessionPropertyId.eGameMode);
			m_gamemodePropertyValue.m_operator = OnlineMultiplayerSessionPropertySearchValue.Operator.eEquals;
			m_searchPropertyValues.Add(m_gamemodePropertyValue);
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
		m_gamemodePropertyValue.m_valueMinRange = (uint)mode;
		m_gamemodePropertyValue.m_valueMaxRange = (uint)mode;
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

	public static List<OnlineMultiplayerSessionPropertySearchValue> GetValues()
	{
		if (!m_bInit)
		{
			IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
			m_bInit = Initialise(onlinePlatformManager.OnlineMultiplayerSessionPropertyCoordinator());
		}
		return m_searchPropertyValues;
	}
}
