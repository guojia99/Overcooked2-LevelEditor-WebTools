using System.Collections;
using System.Collections.Generic;
using InControl;
using Team17.Online;

public class PCPlayerManager : PlayerManagerShared<PCPlayerManager.PCPlayerProfile>
{
	public class PCPlayerProfile : GamepadUser
	{
		private PlayerActionSet m_actionSet;

		public override string UID
		{
			get
			{
				return (m_actionSet.Device == null) ? "Keyboard" : m_actionSet.Device.Meta;
			}
		}

		public override ControlTypeEnum ControlType
		{
			get
			{
				if (m_actionSet != null && m_actionSet.Device == null)
				{
					return ControlTypeEnum.Keyboard;
				}
				return ControlTypeEnum.Pad;
			}
		}

		public override string DisplayName
		{
			get
			{
				if (m_actionSet != null)
				{
					if (m_actionSet.Device != null)
					{
						return Localization.Get("StartScreen.Engagement.ControllerDevice");
					}
					return Localization.Get("StartScreen.Engagement.KeyboardDevice");
				}
				return string.Empty;
			}
		}

		public PCPlayerProfile(PlayerActionSet _input)
		{
			m_actionSet = _input;
		}
	}

	protected override void Awake()
	{
		WindowsAccessibility.ToggleAccessibilityShortcutKeys(false);
		base.Awake();
	}

	protected virtual void OnDestroy()
	{
		WindowsAccessibility.ToggleAccessibilityShortcutKeys(true);
	}

	protected override bool CanEngage(EngagementSlot _e, ControlPadInput.PadNum _new, bool onlyAllowLostStickyProfiles)
	{
		if (PCPadInputProvider.IsKeyboard(_new) && !CanChangeSplitPads)
		{
			GameInputConfig baseInputConfig = PlayerInputLookup.GetBaseInputConfig();
			GameInputConfig.ConfigEntry[] array = baseInputConfig.m_playerConfigs.FindAll((GameInputConfig.ConfigEntry x) => x.Pad == (ControlPadInput.PadNum)_e);
			if (array.Length > 1)
			{
				return false;
			}
			if (array.Length == 1 && array[0].Side != PadSide.Both)
			{
				return false;
			}
		}
		return base.CanEngage(_e, _new, onlyAllowLostStickyProfiles);
	}

	public override bool HasPlayer()
	{
		return IsEngaged(EngagementSlot.One);
	}

	public override bool HasSavablePlayer()
	{
		return HasPlayer();
	}

	protected void BootstrapAwake()
	{
		EngagementSlot engagementSlot = EngagementSlot.One;
		for (int i = 0; i < PlayerInputLookup.GetSystemControllerMaximum(); i++)
		{
			ControlPadInput.PadNum padNum = (ControlPadInput.PadNum)i;
			bool flag = Singleton<PCPadInputProvider>.Get().IsPadAttached(padNum);
			if ((engagementSlot == EngagementSlot.One || flag) && engagementSlot != EngagementSlot.Count)
			{
				EngagePadToSlot(padNum, engagementSlot);
				engagementSlot++;
			}
		}
	}

	protected override IEnumerator PadEngageRoutine(ControlPadInput.PadNum _engagingPadNum, EngagmentCircumstances _circumstances, EngagementSlot _intendedSlot, bool _forcePlayerChoice, VoidGeneric<GamepadUser> _finishedCallback)
	{
		PCPlayerProfile param = EngagePadToSlot(_engagingPadNum, _intendedSlot);
		if (_finishedCallback != null)
		{
			_finishedCallback(param);
		}
		yield break;
	}

	protected virtual PCPlayerProfile EngagePadToSlot(ControlPadInput.PadNum _engagingPadNum, EngagementSlot _intendedSlot)
	{
		PlayerActionSet input = PCPadInputProvider.EngagePad(_engagingPadNum, (ControlPadInput.PadNum)_intendedSlot);
		PCPlayerProfile pCPlayerProfile = new PCPlayerProfile(input);
		if (m_lostUsers.Count > 0 && m_lostUsers[0].UID != pCPlayerProfile.UID)
		{
			PCPadInputProvider.DisengagePad((ControlPadInput.PadNum)_intendedSlot);
			return null;
		}
		pCPlayerProfile.StickyEngagement = _intendedSlot == EngagementSlot.One;
		AssignProfileToSlot(_intendedSlot, pCPlayerProfile);
		return pCPlayerProfile;
	}

	protected override void OnPadDisengage(EngagementSlot _slot)
	{
		PCPadInputProvider.DisengagePad((ControlPadInput.PadNum)_slot);
		base.OnPadDisengage(_slot);
	}

	public override void ShowGamerCard(GamepadUser localUser)
	{
	}

	public override void ShowGamerCard(OnlineUserPlatformId onlineUser)
	{
	}

	protected override void Update()
	{
		for (int i = 0; i < 4; i++)
		{
			EngagementSlot engagementSlot = (EngagementSlot)i;
			if (IsEngaged(engagementSlot))
			{
				ControlPadInput.PadNum pad = (ControlPadInput.PadNum)i;
				if (!PlayerInputLookup.IsPadAttached(pad))
				{
					DisengageSlots(new List<EngagementSlot>(new EngagementSlot[1] { engagementSlot }));
				}
			}
		}
		base.Update();
	}

	public override bool SupportsInvitesForAnyUser()
	{
		return false;
	}
}
