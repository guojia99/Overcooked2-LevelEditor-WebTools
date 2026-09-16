using System.Collections.Generic;
using System.Linq;
using Team17.Online;
using UnityEngine;

public class T17EventSystemsManager
{
	private static T17EventSystemsManager m_Instance;

	private Dictionary<string, T17EventSystem> m_EventSystems;

	private Dictionary<string, GameObject> m_SelectedBeforeDisable;

	private List<T17EventSystem> m_FreeEventSystems;

	private Dictionary<string, int> m_DisabledUsersRefCounts;

	public static T17EventSystemsManager Instance
	{
		get
		{
			if (m_Instance == null)
			{
				m_Instance = new T17EventSystemsManager();
			}
			return m_Instance;
		}
	}

	private T17EventSystemsManager()
	{
		m_EventSystems = new Dictionary<string, T17EventSystem>();
		m_SelectedBeforeDisable = new Dictionary<string, GameObject>();
		m_FreeEventSystems = new List<T17EventSystem>();
		m_DisabledUsersRefCounts = new Dictionary<string, int>();
	}

	~T17EventSystemsManager()
	{
	}

	public void ResetEventSystem(T17EventSystem eventSystem)
	{
		if (m_EventSystems.ContainsKey(eventSystem.AssignedGamepadUser.UID))
		{
			m_EventSystems.Remove(eventSystem.AssignedGamepadUser.UID);
		}
		eventSystem.ResetSystem();
		eventSystem.enabled = true;
		m_FreeEventSystems.Add(eventSystem);
	}

	public void ResetAll()
	{
		List<T17EventSystem> list = m_EventSystems.Values.ToList();
		for (int i = 0; i < list.Count; i++)
		{
			ResetEventSystem(list[i]);
		}
		m_EventSystems.Clear();
		m_SelectedBeforeDisable.Clear();
		m_DisabledUsersRefCounts.Clear();
	}

	public void RegisterEventSystem(T17EventSystem eventSystem)
	{
		eventSystem.SetAssignedGamepadUser(null);
		m_FreeEventSystems.Add(eventSystem);
	}

	public T17EventSystem AssignFreeEventSystemToGamepadUser(GamepadUser user)
	{
		if (user == null)
		{
			return null;
		}
		if (m_EventSystems.ContainsKey(user.UID))
		{
			return null;
		}
		for (int num = m_FreeEventSystems.Count - 1; num >= 0; num--)
		{
			if (m_FreeEventSystems[num].AssignedGamepadUser == null)
			{
				T17EventSystem t17EventSystem = m_FreeEventSystems[num];
				t17EventSystem.SetAssignedGamepadUser(user);
				m_EventSystems.Add(user.UID, t17EventSystem);
				m_FreeEventSystems.RemoveAt(num);
				return t17EventSystem;
			}
		}
		return null;
	}

	public T17EventSystem GetEventSystemForGamepadUser(GamepadUser gamepadUser)
	{
		if (gamepadUser == null)
		{
			return null;
		}
		if (m_EventSystems.ContainsKey(gamepadUser.UID))
		{
			return m_EventSystems[gamepadUser.UID];
		}
		return null;
	}

	public T17EventSystem GetFirstFreeEventSystem()
	{
		if (m_FreeEventSystems.Count > 0)
		{
			return m_FreeEventSystems[0];
		}
		return null;
	}

	public void DisableAllEventSystemsExceptFor(GamepadUser gamer)
	{
		foreach (KeyValuePair<string, T17EventSystem> eventSystem in m_EventSystems)
		{
			if (!(gamer == null) && !(eventSystem.Key != gamer.UID))
			{
				continue;
			}
			int value = 0;
			m_DisabledUsersRefCounts.TryGetValue(eventSystem.Key, out value);
			if (value == 0)
			{
				if (!m_SelectedBeforeDisable.ContainsKey(eventSystem.Key))
				{
					m_SelectedBeforeDisable.Add(eventSystem.Key, eventSystem.Value.currentSelectedGameObject);
				}
				eventSystem.Value.enabled = false;
			}
			value++;
			m_DisabledUsersRefCounts[eventSystem.Key] = value;
		}
	}

	public void EnableAllEventSystems()
	{
		foreach (KeyValuePair<string, T17EventSystem> eventSystem in m_EventSystems)
		{
			int value = 0;
			if (!m_DisabledUsersRefCounts.TryGetValue(eventSystem.Key, out value))
			{
				continue;
			}
			value--;
			if (value == 0)
			{
				eventSystem.Value.enabled = true;
				if (m_SelectedBeforeDisable.ContainsKey(eventSystem.Key))
				{
					GameObject gameObject = m_SelectedBeforeDisable[eventSystem.Key];
					if (gameObject != null && gameObject.activeInHierarchy)
					{
						eventSystem.Value.SetSelectedGameObject(null);
						eventSystem.Value.SetSelectedGameObject(gameObject);
					}
					m_SelectedBeforeDisable.Remove(eventSystem.Key);
				}
				m_DisabledUsersRefCounts.Remove(eventSystem.Key);
			}
			else
			{
				m_DisabledUsersRefCounts[eventSystem.Key] = value;
			}
		}
	}

	public T17EventSystem GetEventSystemForUser(User user)
	{
		if (user == null || user.GamepadUser == null)
		{
			return null;
		}
		return GetEventSystemForGamepadUser(user.GamepadUser);
	}

	public T17EventSystem GetEventSystemForEngagementSlot(EngagementSlot slot)
	{
		FastList<User> users = ClientUserSystem.m_Users;
		for (int i = 0; i < users.Count; i++)
		{
			User user = users._items[i];
			if (user.Engagement == slot)
			{
				if (user.GamepadUser != null)
				{
					return Instance.GetEventSystemForGamepadUser(user.GamepadUser);
				}
				return null;
			}
		}
		return null;
	}
}
