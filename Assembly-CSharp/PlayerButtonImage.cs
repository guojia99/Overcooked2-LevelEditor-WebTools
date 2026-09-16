using System;
using UnityEngine;
using UnityEngine.UI;

public class PlayerButtonImage : MonoBehaviour
{
	[SerializeField]
	[AssignComponent(Editorbility.NonEditable)]
	private Image m_image;

	[SerializeField]
	private PlayerInputLookup.LogicalButtonID m_control = PlayerInputLookup.LogicalButtonID.UISelect;

	[SerializeField]
	private PlayerInputLookup.Player m_player;

	[SerializeField]
	private ControllerIconLookup.IconContext m_context;

	[SerializeField]
	private bool m_lookupScale = true;

	private PlayerManager m_playerManager;

	private void Awake()
	{
		m_playerManager = GameUtils.RequireManager<PlayerManager>();
		m_playerManager.EngagementChangeCallback += OnEngagementChanged;
		RefreshImage();
		PlayerInputLookup.OnRegenerateControls = (CallbackVoid)Delegate.Combine(PlayerInputLookup.OnRegenerateControls, new CallbackVoid(RefreshImage));
	}

	private void OnEngagementChanged(EngagementSlot _s, GamepadUser _b, GamepadUser _a)
	{
		RefreshImage();
	}

	private void OnDestroy()
	{
		PlayerInputLookup.OnRegenerateControls = (CallbackVoid)Delegate.Remove(PlayerInputLookup.OnRegenerateControls, new CallbackVoid(RefreshImage));
		m_playerManager.EngagementChangeCallback -= OnEngagementChanged;
	}

	public static bool IsKeyBoard(PlayerManager _playerManger, PlayerInputLookup.Player _player)
	{
		ControlPadInput.PadNum padForPlayer = PlayerInputLookup.GetPadForPlayer(_player);
		GamepadUser user = _playerManger.GetUser((EngagementSlot)padForPlayer);
		if (user != null)
		{
			return user.ControlType == GamepadUser.ControlTypeEnum.Keyboard;
		}
		return false;
	}

	public static ControllerIconLookup.DeviceContext GetDevice(PlayerManager _playerManger, PlayerInputLookup.Player _player)
	{
		bool flag = IsKeyBoard(_playerManger, _player);
		ControllerIconLookup.DeviceContext result = ControllerIconLookup.DeviceContext.Pad;
		if (flag)
		{
			result = ControllerIconLookup.DeviceContext.Keyboard;
			if (StandardActionSet.IsKeyboardSplit())
			{
				result = ControllerIconLookup.DeviceContext.SplitKeyboard;
			}
		}
		return result;
	}

	private void RefreshImage()
	{
		ControllerIconLookup controllerIconLookup = GameUtils.RequireManager<ControllerIconLookup>();
		ControllerIconLookup.DeviceContext device = GetDevice(m_playerManager, m_player);
		Sprite sprite = null;
		float num = 1f;
		ControlPadInput.Button? button = GetControlPadButton<ControlPadInput.Button>(m_control, m_player, device);
		if (button.HasValue)
		{
			if (PlayerManagerShared<PCPlayerManager.PCPlayerProfile>.AcceptAndCancelButtonsInverted)
			{
				if (button.Value == ControlPadInput.Button.A)
				{
					button = ControlPadInput.Button.B;
				}
				else if (button.Value == ControlPadInput.Button.B)
				{
					button = ControlPadInput.Button.A;
				}
			}
			sprite = controllerIconLookup.GetIcon(button.Value, m_context, device);
			num = controllerIconLookup.GetIconScale(button.Value, m_context, device);
		}
		m_image.sprite = sprite;
		if (sprite != null && m_lookupScale)
		{
			base.transform.localScale = new Vector3(num, num, 1f);
		}
	}

	public static T GetButtonComponent<T>(ILogicalButton _compositeButton) where T : LogicalButtonBase
	{
		AcyclicGraph<ILogicalElement, LogicalLinkInfo> _graph;
		AcyclicGraph<ILogicalElement, LogicalLinkInfo>.Node _head;
		_compositeButton.GetLogicTreeData(out _graph, out _head);
		foreach (ILogicalElement item in _graph)
		{
			T val = item as T;
			if (val != null)
			{
				return val;
			}
		}
		return (T)null;
	}

	public static T? GetControlPadButton<T>(ILogicalButton _iLogicalButton, ControllerIconLookup.DeviceContext device) where T : struct
	{
		if (device == ControllerIconLookup.DeviceContext.Pad)
		{
			LogicalPadButtonBase<T> buttonComponent = GetButtonComponent<LogicalPadButtonBase<T>>(_iLogicalButton);
			if (buttonComponent != null)
			{
				return buttonComponent.GetControlpadButton();
			}
		}
		else
		{
			LogicalKeycodeButtonBase<T> buttonComponent2 = GetButtonComponent<LogicalKeycodeButtonBase<T>>(_iLogicalButton);
			if (buttonComponent2 != null)
			{
				return buttonComponent2.GetControlButton();
			}
		}
		return null;
	}

	public static T? GetControlPadButton<T>(PlayerInputLookup.LogicalButtonID _id, PlayerInputLookup.Player _player, ControllerIconLookup.DeviceContext device) where T : struct
	{
		ILogicalButton button = PlayerInputLookup.GetButton(_id, _player);
		return GetControlPadButton<T>(button, device);
	}
}
