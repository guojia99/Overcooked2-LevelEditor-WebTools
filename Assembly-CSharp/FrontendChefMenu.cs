using System;
using System.Collections.Generic;
using Team17.Online;
using UnityEngine;

public class FrontendChefMenu : FrontendMenuBehaviour
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

	public FrontendChefCustomisation[] m_PlayerChefs;

	private T17TabPanel m_tabPanel;

	private GamepadUser m_currentGamer;

	private BaseMenuBehaviour m_parent;

	private GameObject m_invoker;

	private ILogicalButton m_selectButton;

	private ILogicalButton m_cancelButton;

	private Dictionary<CachedKey, uint> m_cachedAvatars = new Dictionary<CachedKey, uint>();

	private ChefCustomiser m_customiser = new ChefCustomiser();

	private void OnInviteJoinComplete()
	{
		if (base.CurrentGamepadUser != null)
		{
			Close();
		}
	}

	protected override void Start()
	{
		base.Start();
		m_customiser.SetChefs(m_PlayerChefs);
		m_selectButton = PlayerInputLookup.GetEngagedButton(PlayerInputLookup.LogicalButtonID.UISelect, PlayerInputLookup.Player.One, PadSide.Both);
		m_cancelButton = PlayerInputLookup.GetEngagedButton(PlayerInputLookup.LogicalButtonID.UICancel, PlayerInputLookup.Player.One, PadSide.Both);
	}

	public override bool Show(GamepadUser currentGamer, BaseMenuBehaviour parent, GameObject invoker, bool hideInvoker = true)
	{
		if ((!base.gameObject.activeSelf && T17FrontendFlow.Instance != null && T17FrontendFlow.Instance.IsCameraTransitioning()) || GameUtils.RequireManager<PlayerManager>().IsWarningActive(PlayerWarning.Disengaged))
		{
			return false;
		}
		m_currentGamer = currentGamer;
		m_parent = parent;
		m_invoker = invoker;
		InviteMonitor.InviteJoinComplete = (GenericVoid)Delegate.Combine(InviteMonitor.InviteJoinComplete, new GenericVoid(OnInviteJoinComplete));
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Combine(ClientUserSystem.usersChanged, new GenericVoid(EngagementChangedEvent));
		ActivateChefCustomisation(true);
		PlayIntroSelectingAnimations();
		FrontendRootMenu frontendRootMenu = parent as FrontendRootMenu;
		if (frontendRootMenu != null)
		{
			m_tabPanel = frontendRootMenu.m_MainTabPanel;
			m_tabPanel.Hide();
		}
		if (T17FrontendFlow.Instance != null)
		{
			T17FrontendFlow.Instance.PushForwardCamera();
		}
		bool flag = base.Show(currentGamer, parent, invoker, hideInvoker);
		if (flag)
		{
			if (m_selectButton != null)
			{
				m_selectButton.ClaimPressEvent();
				m_selectButton.ClaimReleaseEvent();
			}
			if (m_cancelButton != null)
			{
				m_cancelButton.ClaimPressEvent();
				m_cancelButton.ClaimReleaseEvent();
			}
		}
		m_customiser.CacheCurrentAvatars();
		return flag;
	}

	public override bool Hide(bool restoreInvokerState = true, bool isTabSwitch = false)
	{
		if (T17FrontendFlow.Instance == null || T17FrontendFlow.Instance.IsCameraTransitioning())
		{
			return false;
		}
		InviteMonitor.InviteJoinComplete = (GenericVoid)Delegate.Remove(InviteMonitor.InviteJoinComplete, new GenericVoid(OnInviteJoinComplete));
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Remove(ClientUserSystem.usersChanged, new GenericVoid(EngagementChangedEvent));
		ActivateChefCustomisation(false);
		m_customiser.PlaySelectedAnimations();
		if (base.gameObject != null && base.gameObject.activeInHierarchy && T17FrontendFlow.Instance != null)
		{
			T17FrontendFlow.Instance.PullBackCamera();
		}
		if (m_tabPanel != null)
		{
			m_tabPanel.Show(m_currentGamer, m_parent, m_invoker);
			m_tabPanel.ExpandCurrentTab();
		}
		return base.Hide(restoreInvokerState, isTabSwitch);
	}

	protected override void OnDestroy()
	{
		Hide();
		base.OnDestroy();
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Remove(ClientUserSystem.usersChanged, new GenericVoid(EngagementChangedEvent));
		InviteMonitor.InviteJoinComplete = (GenericVoid)Delegate.Remove(InviteMonitor.InviteJoinComplete, new GenericVoid(OnInviteJoinComplete));
	}

	protected override void Update()
	{
		base.Update();
		if (m_selectButton.JustPressed() && !T17FrontendFlow.Instance.IsCameraTransitioning())
		{
			m_selectButton.ClaimPressEvent();
			m_selectButton.ClaimReleaseEvent();
			Hide();
		}
		if (m_selectButton.JustPressed() && !T17FrontendFlow.Instance.IsCameraTransitioning())
		{
			m_cancelButton.ClaimPressEvent();
			m_cancelButton.ClaimReleaseEvent();
			Close();
		}
	}

	private void ActivateChefCustomisation(bool activate)
	{
		m_customiser.SetChefs(m_PlayerChefs);
		m_customiser.ActivateChefCustomisation(activate);
		T17FrontendFlow instance = T17FrontendFlow.Instance;
		if (instance != null)
		{
			if (instance.m_PlayerLobby != null)
			{
				instance.m_PlayerLobby.SetMouseBlockActive(activate);
			}
			instance.allowMultichefMenu = !activate;
		}
	}

	private void EngagementChangedEvent()
	{
		ActivateChefCustomisation(base.enabled);
	}

	private void PlayIntroSelectingAnimations()
	{
		int num = m_PlayerChefs.Length;
		for (int i = 0; i < num; i++)
		{
			m_PlayerChefs[i].PlaySelectingAnimation();
		}
	}

	public override void Close()
	{
		m_customiser.RevertAvatars();
		base.Close();
	}
}
