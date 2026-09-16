using System;
using System.Collections.Generic;
using Team17.Online;
using UnityEngine;

internal class VoiceChatUserUI : MonoBehaviour
{
	[SerializeField]
	private EngagementSlot m_PlayerSlot;

	[SerializeField]
	private T17Text m_GamerName;

	[SerializeField]
	private GameObject m_TalkingIcon;

	[SerializeField]
	private GameObject m_MuteIcon;

	private User m_TalkingUser;

	public EngagementSlot PlayerSlot
	{
		get
		{
			return m_PlayerSlot;
		}
		set
		{
			m_PlayerSlot = value;
			UpdateTalkingUser();
		}
	}

	private void Awake()
	{
		DisableAllHUD();
		UpdateTalkingUser();
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Combine(ClientUserSystem.usersChanged, new GenericVoid(UpdateTalkingUser));
	}

	private void OnDestroy()
	{
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Remove(ClientUserSystem.usersChanged, new GenericVoid(UpdateTalkingUser));
	}

	private void Update()
	{
		if (m_TalkingUser != null && m_TalkingUser.SessionId != null)
		{
			if (!m_TalkingUser.SessionId.IsLocal && m_TalkingUser.SessionId.IsLocallyMuted)
			{
				if (m_GamerName != null)
				{
					m_GamerName.gameObject.SetActive(true);
					m_GamerName.SetNonLocalizedText(m_TalkingUser.DisplayName);
				}
				m_TalkingIcon.SetActive(false);
				m_MuteIcon.SetActive(true);
			}
			else if (m_TalkingUser.SessionId.IsSpeaking)
			{
				if (m_GamerName != null)
				{
					m_GamerName.gameObject.SetActive(true);
					m_GamerName.SetNonLocalizedText(m_TalkingUser.DisplayName);
				}
				m_TalkingIcon.SetActive(true);
				m_MuteIcon.SetActive(false);
			}
			else
			{
				DisableAllHUD();
			}
		}
		else
		{
			DisableAllHUD();
		}
	}

	private void UpdateTalkingUser()
	{
		FastList<User> users = ClientUserSystem.m_Users;
		if ((int)m_PlayerSlot < users.Count)
		{
			m_TalkingUser = users._items[(int)m_PlayerSlot];
		}
		else
		{
			m_TalkingUser = null;
		}
	}

	private void DisableAllHUD()
	{
		if (m_GamerName != null)
		{
			m_GamerName.gameObject.SetActive(false);
		}
		m_TalkingIcon.SetActive(false);
		m_MuteIcon.SetActive(false);
	}
}
