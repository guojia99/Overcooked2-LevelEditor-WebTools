using System.Collections.Generic;
using Team17.Online;
using UnityEngine;

public class ChefCustomiser
{
	private struct CachedKey
	{
		public EngagementSlot slot;

		public User.SplitStatus splitStatus;

		public CachedKey(EngagementSlot _slot, User.SplitStatus _status)
		{
			slot = _slot;
			splitStatus = _status;
		}
	}

	private PlayerManager m_playerManager;

	private FrontendChefCustomisation[] m_chefCustomisations;

	private int m_audioLayer = -1;

	private GameInputConfig m_initialInputConfig;

	private Dictionary<CachedKey, uint> m_cachedAvatars = new Dictionary<CachedKey, uint>();

	public void CacheCurrentAvatars()
	{
		m_cachedAvatars.Clear();
		for (int i = 0; i < ClientUserSystem.m_Users.Count; i++)
		{
			User user = ClientUserSystem.m_Users._items[i];
			if (user.IsLocal)
			{
				CachedKey key = new CachedKey(user.Engagement, user.Split);
				m_cachedAvatars[key] = user.SelectedChefAvatar;
			}
		}
	}

	public void RevertAvatars()
	{
		foreach (KeyValuePair<CachedKey, uint> cachedAvatar in m_cachedAvatars)
		{
			CachedKey key = cachedAvatar.Key;
			FastList<User> users = ClientUserSystem.m_Users;
			User.MachineID s_LocalMachineId = ClientUserSystem.s_LocalMachineId;
			User user = UserSystemUtils.FindUser(users, null, s_LocalMachineId, cachedAvatar.Key.slot, TeamID.Count, cachedAvatar.Key.splitStatus);
			if (user != null)
			{
				ClientMessenger.ChefAvatar(cachedAvatar.Value, user);
			}
		}
	}

	public void ActivateChefCustomisation(bool activate)
	{
		if (m_playerManager == null)
		{
			m_playerManager = GameUtils.RequireManager<PlayerManager>();
		}
		if (activate && m_audioLayer == -1)
		{
			m_audioLayer = LayerMask.NameToLayer("Administration");
		}
		bool flag = false;
		for (int i = 0; i < m_chefCustomisations.Length; i++)
		{
			if (m_chefCustomisations[i].IsActive())
			{
				flag = true;
				break;
			}
		}
		if (activate && !flag)
		{
			m_initialInputConfig = PlayerInputLookup.GetInputConfig();
			UserSystemUtils.BuildGameInputConfig();
		}
		else if (!activate && flag)
		{
			if (m_initialInputConfig != null)
			{
				PlayerInputLookup.SetInputConfig(m_initialInputConfig);
			}
			else
			{
				PlayerInputLookup.ResetToDefaultInputConfig();
			}
		}
		int num = -1;
		for (int j = 0; j < m_chefCustomisations.Length; j++)
		{
			User user = null;
			if (j < ClientUserSystem.m_Users.Count)
			{
				user = ClientUserSystem.m_Users._items[j];
			}
			bool flag2 = user != null && user.IsLocal;
			if (flag2)
			{
				num++;
			}
			if (activate)
			{
				if (flag2)
				{
					ControlPadInput.PadNum engagement = (ControlPadInput.PadNum)user.Engagement;
					AmbiControlsMappingData mappingData = ((user.PadSide != PadSide.Both) ? m_playerManager.SidedAmbiMapping : m_playerManager.UnsidedAmbiMapping);
					PlayerGameInput playerGameInput = new PlayerGameInput(engagement, user.PadSide, mappingData);
					if (playerGameInput != null && user.SelectedChefData != null)
					{
						if (!m_chefCustomisations[j].IsActive())
						{
							m_chefCustomisations[j].Activate(playerGameInput, user.SelectedChefData);
						}
						else
						{
							m_chefCustomisations[j].RebindInput(playerGameInput);
						}
					}
				}
				else if (user != null)
				{
					m_chefCustomisations[j].ActivateLayoutOnly();
				}
				else
				{
					m_chefCustomisations[j].Deactivate();
				}
			}
			else
			{
				m_chefCustomisations[j].Deactivate();
			}
		}
	}

	public void SetChefs(FrontendChefCustomisation[] _chefCustomisation)
	{
		m_chefCustomisations = _chefCustomisation;
	}

	public void PlaySelectedAnimations()
	{
		bool flag = false;
		int num = m_chefCustomisations.Length;
		for (int i = 0; i < num; i++)
		{
			User user = null;
			if (i < ClientUserSystem.m_Users.Count)
			{
				user = ClientUserSystem.m_Users._items[i];
			}
			if (user != null && user.IsLocal)
			{
				FrontendChefCustomisation frontendChefCustomisation = m_chefCustomisations[i];
				frontendChefCustomisation.PlaySelectedAnimation();
				if (!flag && m_audioLayer != -1)
				{
					GameUtils.TriggerAudio(GameOneShotAudioTag.UIChefSelected, m_audioLayer);
					flag = true;
				}
			}
		}
	}
}
