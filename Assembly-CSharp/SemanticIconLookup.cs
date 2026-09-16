using System;
using UnityEngine;

public class SemanticIconLookup : Manager
{
	[Serializable]
	private class ButtonSet
	{
		public Sprite Pickup;

		public Sprite Chop;

		public Sprite Button;

		public Sprite Washing;

		public Sprite FireExtinguisher;

		public Sprite Talk;

		public Sprite Portal;

		public Sprite Switch;

		public Sprite Whack;

		public Sprite Dash;

		public Sprite Generic;

		public Sprite GetSemanticIcon(Semantic _s)
		{
			switch (_s)
			{
			case Semantic.Pickup:
				return Pickup;
			case Semantic.Chop:
				return Chop;
			case Semantic.Button:
				return Button;
			case Semantic.Washing:
				return Washing;
			case Semantic.FireExtinguisher:
				return FireExtinguisher;
			case Semantic.Talk:
				return Talk;
			case Semantic.Portal:
				return Portal;
			case Semantic.Switch:
				return Switch;
			case Semantic.Whack:
				return Whack;
			case Semantic.Dash:
				return Dash;
			default:
				return Generic;
			}
		}

		public PlayerInputLookup.LogicalButtonID GetButton(Semantic _s)
		{
			switch (_s)
			{
			case Semantic.Pickup:
			case Semantic.FireExtinguisher:
			case Semantic.Talk:
			case Semantic.Portal:
				return PlayerInputLookup.LogicalButtonID.PickupAndDrop;
			case Semantic.Chop:
			case Semantic.Button:
			case Semantic.Washing:
			case Semantic.Whack:
				return PlayerInputLookup.LogicalButtonID.WorkstationInteract;
			case Semantic.Switch:
				return PlayerInputLookup.LogicalButtonID.PlayerSwitch;
			case Semantic.Dash:
				return PlayerInputLookup.LogicalButtonID.Dash;
			default:
				return PlayerInputLookup.LogicalButtonID.WorkstationInteract;
			}
		}
	}

	[Serializable]
	private class PlatformSet
	{
		public ButtonSet XboxOne;

		public ButtonSet PS4;

		public ButtonSet Switch;

		public ButtonSet PC;

		public ButtonSet GetIconPack()
		{
			if (KeyboardUtils.IsKeyboard(PlayerInputLookup.Player.One))
			{
				return PC;
			}
			return XboxOne;
		}
	}

	public enum Semantic
	{
		Pickup = 0,
		Chop = 1,
		Button = 2,
		Washing = 3,
		FireExtinguisher = 4,
		Talk = 5,
		Portal = 6,
		Switch = 7,
		Whack = 8,
		Dash = 9,
		Generic = 10
	}

	[SerializeField]
	private PlatformSet m_platformSet;

	private PlayerManager m_playerManager;

	private ControllerIconLookup m_controllerIconLookup;

	private void Awake()
	{
		m_controllerIconLookup = GameUtils.RequireManager<ControllerIconLookup>();
		m_playerManager = GameUtils.RequireManager<PlayerManager>();
	}

	private bool IsSemantic()
	{
		GameInputConfig inputConfig = PlayerInputLookup.GetInputConfig();
		GameInputConfig.ConfigEntry[] playerConfigs = inputConfig.m_playerConfigs;
		playerConfigs = playerConfigs.AllRemoved_Predicate((GameInputConfig.ConfigEntry x) => GetUser(x) == null || !x.IsLocal());
		if (playerConfigs.Length == 0)
		{
			return true;
		}
		bool flag = playerConfigs.Contains((GameInputConfig.ConfigEntry x) => x.Side != playerConfigs[0].Side);
		bool flag2 = playerConfigs.Contains((GameInputConfig.ConfigEntry x) => GetUser(x).ControlType != GetUser(playerConfigs[0]).ControlType);
		bool flag3 = playerConfigs.Contains((GameInputConfig.ConfigEntry x) => GetUser(x).ControlType == GamepadUser.ControlTypeEnum.Keyboard);
		return flag || flag2 || flag3;
	}

	private GamepadUser GetUser(GameInputConfig.ConfigEntry _entry)
	{
		return m_playerManager.GetUser((EngagementSlot)_entry.Pad);
	}

	public Sprite GetSemanticIcon(Semantic _semantic)
	{
		ButtonSet iconPack = m_platformSet.GetIconPack();
		return iconPack.GetSemanticIcon(_semantic);
	}

	public Sprite GetIcon(Semantic _semantic, PlayerInputLookup.Player _player = PlayerInputLookup.Player.One, ControllerIconLookup.IconContext _context = ControllerIconLookup.IconContext.Bordered)
	{
		ButtonSet iconPack = m_platformSet.GetIconPack();
		if (IsSemantic())
		{
			return iconPack.GetSemanticIcon(_semantic);
		}
		PlayerInputLookup.LogicalButtonID button = iconPack.GetButton(_semantic);
		ControllerIconLookup.DeviceContext device = PlayerButtonImage.GetDevice(m_playerManager, _player);
		ControlPadInput.Button button2 = PlayerButtonImage.GetControlPadButton<ControlPadInput.Button>(button, _player, device).Value;
		if (PlayerManagerShared<PCPlayerManager.PCPlayerProfile>.AcceptAndCancelButtonsInverted)
		{
			switch (button2)
			{
			case ControlPadInput.Button.A:
				button2 = ControlPadInput.Button.B;
				break;
			case ControlPadInput.Button.B:
				button2 = ControlPadInput.Button.A;
				break;
			}
		}
		return m_controllerIconLookup.GetIcon(button2, _context, device);
	}
}
