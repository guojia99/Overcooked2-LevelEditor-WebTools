using System.Collections.Generic;
using Team17.Online;

public static class LocalDroppedInUserCache
{
	private static List<OnlineMultiplayerLocalUserId> m_DroppedInUsers = new List<OnlineMultiplayerLocalUserId>();

	public static void Clear()
	{
		m_DroppedInUsers.Clear();
	}

	public static void AddDroppedInUser(OnlineMultiplayerLocalUserId user)
	{
		if (!m_DroppedInUsers.Contains(user))
		{
			m_DroppedInUsers.Add(user);
		}
	}

	public static void RemoveDroppedInUser(OnlineMultiplayerLocalUserId user)
	{
		if (m_DroppedInUsers.Contains(user))
		{
			m_DroppedInUsers.Remove(user);
		}
	}

	public static bool HasBeenDroppedIn(OnlineMultiplayerLocalUserId user)
	{
		if (m_DroppedInUsers.Contains(user))
		{
			return true;
		}
		return false;
	}
}
