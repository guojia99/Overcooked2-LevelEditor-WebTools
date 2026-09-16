using System.Collections.Generic;
using Team17.Online;
using UnityEngine;

public class UIFocusBlocker : MonoBehaviour
{
	private Suppressor m_Supressor;

	private void OnEnable()
	{
		FastList<User> users = ClientUserSystem.m_Users;
		if (T17EventSystemsManager.Instance == null)
		{
			return;
		}
		for (int i = 0; i < users.Count; i++)
		{
			User user = users._items[i];
			if (user.Engagement != EngagementSlot.One)
			{
				continue;
			}
			if (user != null && user.GamepadUser != null)
			{
				T17EventSystem eventSystemForGamepadUser = T17EventSystemsManager.Instance.GetEventSystemForGamepadUser(user.GamepadUser);
				if (eventSystemForGamepadUser != null)
				{
					m_Supressor = eventSystemForGamepadUser.Disable(this);
				}
			}
			break;
		}
	}

	private void OnDisable()
	{
		if (m_Supressor != null)
		{
			m_Supressor.Release();
		}
	}

	private void OnDestroy()
	{
		if (m_Supressor != null)
		{
			m_Supressor.Release();
		}
	}
}
