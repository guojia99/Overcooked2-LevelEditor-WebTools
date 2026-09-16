using System.Collections.Generic;
using Team17.Online;

public static class PrivilegeCheckCache
{
	private static Dictionary<GamepadUser, OnlineMultiplayerLocalUserId> m_AllowedUsers = new Dictionary<GamepadUser, OnlineMultiplayerLocalUserId>(4);

	public static void Clear()
	{
		m_AllowedUsers.Clear();
	}

	public static void AddAllowedUser(GamepadUser gamepadUser, OnlineMultiplayerLocalUserId onlineUser)
	{
		if (m_AllowedUsers.ContainsKey(gamepadUser))
		{
			m_AllowedUsers[gamepadUser] = onlineUser;
		}
		else
		{
			m_AllowedUsers.Add(gamepadUser, onlineUser);
		}
	}

	public static void RemoveAllowedUser(GamepadUser gamepadUser)
	{
		if (m_AllowedUsers.ContainsKey(gamepadUser))
		{
			m_AllowedUsers.Remove(gamepadUser);
		}
	}

	public static OnlineMultiplayerLocalUserId GetAllowedUser(GamepadUser gamepadUser)
	{
		if (m_AllowedUsers.ContainsKey(gamepadUser))
		{
			return m_AllowedUsers[gamepadUser];
		}
		return null;
	}
}
