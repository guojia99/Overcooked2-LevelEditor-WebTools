using System;
using UnityEngine;

public class EmbeddedDeviceIconTextLookup : EmbeddedTextImageLookupBase
{
	[SerializeField]
	private ControlPadInput.Button[] m_buttons = new ControlPadInput.Button[0];

	[SerializeField]
	private Sprite[] m_iconOverridesPC = new Sprite[0];

	private ControllerIconLookup m_controllerIconLookup;

	private PlayerManager m_playerManager;

	protected void Awake()
	{
		m_controllerIconLookup = GameUtils.RequireManager<ControllerIconLookup>();
		m_playerManager = GameUtils.RequireManager<PlayerManager>();
		PlayerInputLookup.OnRegenerateControls = (CallbackVoid)Delegate.Combine(PlayerInputLookup.OnRegenerateControls, new CallbackVoid(base.RefreshImage));
	}

	protected void OnDestroy()
	{
		PlayerInputLookup.OnRegenerateControls = (CallbackVoid)Delegate.Remove(PlayerInputLookup.OnRegenerateControls, new CallbackVoid(base.RefreshImage));
	}

	protected override Sprite GetIcon(int _materialNum)
	{
		if (m_iconOverridesPC.Length > 0 && _materialNum < m_iconOverridesPC.Length && m_iconOverridesPC[_materialNum] != null)
		{
			return m_iconOverridesPC[_materialNum];
		}
		ControlPadInput.Button button = m_buttons.TryAtIndex(_materialNum);
		ControllerIconLookup.DeviceContext device = ((!KeyboardUtils.IsKeyboard(PlayerInputLookup.Player.One)) ? PlayerButtonImage.GetDevice(m_playerManager, PlayerInputLookup.Player.One) : ControllerIconLookup.DeviceContext.Keyboard);
		return m_controllerIconLookup.GetIcon(button, ControllerIconLookup.IconContext.Bordered, device);
	}
}
