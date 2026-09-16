using System;
using UnityEngine;

public class PCDisconnectIconTextLookup : EmbeddedTextImageLookupBase
{
	private ControllerIconLookup m_controllerIconLookup;

	protected void Awake()
	{
		m_controllerIconLookup = GameUtils.RequireManager<ControllerIconLookup>();
		PlayerInputLookup.OnRegenerateControls = (CallbackVoid)Delegate.Combine(PlayerInputLookup.OnRegenerateControls, new CallbackVoid(base.RefreshImage));
	}

	protected void OnDestroy()
	{
		PlayerInputLookup.OnRegenerateControls = (CallbackVoid)Delegate.Remove(PlayerInputLookup.OnRegenerateControls, new CallbackVoid(base.RefreshImage));
	}

	protected override Sprite GetIcon(int _materialNum)
	{
		return m_controllerIconLookup.GetIcon(ControlPadInput.Button.A);
	}
}
