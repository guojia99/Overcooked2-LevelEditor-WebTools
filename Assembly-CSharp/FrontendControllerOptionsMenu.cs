using System;
using UnityEngine;

public class FrontendControllerOptionsMenu : FrontendMenuBehaviour
{
	private GamepadEngagementManager m_gamepadEngagementManager;

	private Suppressor m_engagementSuppressor;

	private KeyboardRebindController m_KeyboardRebindController;

	protected override void SingleTimeInitialize()
	{
		base.SingleTimeInitialize();
		m_gamepadEngagementManager = GameUtils.RequireManager<GamepadEngagementManager>();
		m_KeyboardRebindController = GetComponentInChildren<KeyboardRebindController>();
		m_KeyboardRebindController.SingleTimeInitialize();
	}

	public override bool Show(GamepadUser currentGamer, BaseMenuBehaviour parent, GameObject invoker, bool hideInvoker = true)
	{
		if (m_KeyboardRebindController != null)
		{
			m_KeyboardRebindController.OnShow(this);
		}
		if (T17FrontendFlow.Instance != null)
		{
			T17FrontendFlow.Instance.BlockFocusKitchen = true;
		}
		if (m_gamepadEngagementManager != null)
		{
			m_engagementSuppressor = m_gamepadEngagementManager.Suppressor.AddSuppressor(this);
		}
		InviteMonitor.InviteAccepted = (GenericVoid)Delegate.Combine(InviteMonitor.InviteAccepted, new GenericVoid(OnInviteAccepted));
		SteamPlayerManager.OverlayVisbilityChanged = (GenericVoid<bool>)Delegate.Combine(SteamPlayerManager.OverlayVisbilityChanged, new GenericVoid<bool>(OnOverlayVisbilityChanged));
		return base.Show(currentGamer, parent, invoker, hideInvoker);
	}

	public override bool Hide(bool restoreInvokerState = true, bool isTabSwitch = false)
	{
		if (T17FrontendFlow.Instance != null)
		{
			T17FrontendFlow.Instance.BlockFocusKitchen = false;
		}
		if (m_engagementSuppressor != null)
		{
			m_engagementSuppressor.Release();
			m_engagementSuppressor = null;
		}
		InviteMonitor.InviteAccepted = (GenericVoid)Delegate.Remove(InviteMonitor.InviteAccepted, new GenericVoid(OnInviteAccepted));
		SteamPlayerManager.OverlayVisbilityChanged = (GenericVoid<bool>)Delegate.Remove(SteamPlayerManager.OverlayVisbilityChanged, new GenericVoid<bool>(OnOverlayVisbilityChanged));
		return base.Hide(restoreInvokerState, isTabSwitch);
	}

	protected override void Start()
	{
		base.Start();
	}

	protected override void Update()
	{
		if (!(m_KeyboardRebindController != null) || !m_KeyboardRebindController.IsRebinding)
		{
			base.Update();
		}
	}

	public void CancelAndCloseAllDialogs()
	{
		if (m_KeyboardRebindController != null)
		{
			m_KeyboardRebindController.CancelAndCloseAllDialogs();
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		InviteMonitor.InviteAccepted = (GenericVoid)Delegate.Remove(InviteMonitor.InviteAccepted, new GenericVoid(OnInviteAccepted));
		SteamPlayerManager.OverlayVisbilityChanged = (GenericVoid<bool>)Delegate.Remove(SteamPlayerManager.OverlayVisbilityChanged, new GenericVoid<bool>(OnOverlayVisbilityChanged));
	}

	public override void Close()
	{
		if (m_KeyboardRebindController != null)
		{
			if (m_KeyboardRebindController.IsRebinding)
			{
				m_KeyboardRebindController.CancelRebind();
			}
			if (m_KeyboardRebindController.UnsavedChanges && m_KeyboardRebindController.ShowUnsavedChangesDialog())
			{
				return;
			}
		}
		Hide();
	}

	private void OnInviteAccepted()
	{
		CancelAndCloseAllDialogs();
		Close();
	}

	private void OnOverlayVisbilityChanged(bool _visible)
	{
		if (_visible)
		{
			CancelAndCloseAllDialogs();
		}
	}
}
