using System;
using UnityEngine;

public class DeviceIconSwap : MonoBehaviour
{
	[SerializeField]
	private ControlPadInput.Button m_button;

	private ControllerIconLookup m_controllerIconLookup;

	private PlayerManager m_playerManager;

	[SerializeField]
	private T17Image m_Image;

	public ControlPadInput.Button Button
	{
		get
		{
			return m_button;
		}
		set
		{
			m_button = value;
		}
	}

	protected void Awake()
	{
		m_controllerIconLookup = GameUtils.RequireManager<ControllerIconLookup>();
		m_playerManager = GameUtils.RequireManager<PlayerManager>();
		if (m_Image == null)
		{
			m_Image = base.gameObject.RequireComponent<T17Image>();
		}
		RefreshImage();
		PlayerInputLookup.OnRegenerateControls = (CallbackVoid)Delegate.Combine(PlayerInputLookup.OnRegenerateControls, new CallbackVoid(RefreshImage));
	}

	protected void OnDestroy()
	{
		PlayerInputLookup.OnRegenerateControls = (CallbackVoid)Delegate.Remove(PlayerInputLookup.OnRegenerateControls, new CallbackVoid(RefreshImage));
	}

	private void RefreshImage()
	{
		m_Image.sprite = GetIcon();
	}

	private Sprite GetIcon()
	{
		ControllerIconLookup.DeviceContext device = PlayerButtonImage.GetDevice(m_playerManager, PlayerInputLookup.Player.One);
		return m_controllerIconLookup.GetIcon(m_button, ControllerIconLookup.IconContext.Bordered, device);
	}
}
