using System;
using UnityEngine;

public class InGameControllerOptionsMenu : InGameMenuBehaviour
{
	private KeyboardRebindController m_KeyboardRebindController;

	protected override void SingleTimeInitialize()
	{
		base.SingleTimeInitialize();
		m_KeyboardRebindController = GetComponentInChildren<KeyboardRebindController>();
		m_KeyboardRebindController.SingleTimeInitialize();
	}

	public override bool Show(GamepadUser currentGamer, BaseMenuBehaviour parent, GameObject invoker, bool hideInvoker = true)
	{
		if (m_KeyboardRebindController != null)
		{
			m_KeyboardRebindController.OnShow(this);
		}
		SteamPlayerManager.OverlayVisbilityChanged = (GenericVoid<bool>)Delegate.Combine(SteamPlayerManager.OverlayVisbilityChanged, new GenericVoid<bool>(OnOverlayVisbilityChanged));
		return base.Show(currentGamer, parent, invoker, hideInvoker);
	}

	public override bool Hide(bool restoreInvokerState = true, bool isTabSwitch = false)
	{
		SteamPlayerManager.OverlayVisbilityChanged = (GenericVoid<bool>)Delegate.Remove(SteamPlayerManager.OverlayVisbilityChanged, new GenericVoid<bool>(OnOverlayVisbilityChanged));
		return base.Hide(restoreInvokerState, isTabSwitch);
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		SteamPlayerManager.OverlayVisbilityChanged = (GenericVoid<bool>)Delegate.Remove(SteamPlayerManager.OverlayVisbilityChanged, new GenericVoid<bool>(OnOverlayVisbilityChanged));
	}

	protected override void Update()
	{
		if (!(m_KeyboardRebindController != null) || !m_KeyboardRebindController.IsRebinding)
		{
			base.Update();
		}
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
		base.Close();
	}

	private void OnOverlayVisbilityChanged(bool _visible)
	{
		if (_visible && m_KeyboardRebindController != null)
		{
			m_KeyboardRebindController.CancelAndCloseAllDialogs();
		}
	}
}
