public static class KeyboardUtils
{
	public static bool IsKeyboard(PlayerGameInput input)
	{
		PlayerManager playerManager = GameUtils.RequireManager<PlayerManager>();
		GamepadUser user = playerManager.GetUser((EngagementSlot)input.Pad);
		if (user != null)
		{
			return user.ControlType == GamepadUser.ControlTypeEnum.Keyboard;
		}
		return false;
	}

	public static bool IsKeyboard(PlayerInputLookup.Player player)
	{
		PlayerManager playerManager = GameUtils.RequireManager<PlayerManager>();
		ControlPadInput.PadNum padForPlayer = PlayerInputLookup.GetPadForPlayer(player);
		GamepadUser user = playerManager.GetUser((EngagementSlot)padForPlayer);
		return user != null && user.ControlType == GamepadUser.ControlTypeEnum.Keyboard;
	}

	public static bool KeyboardSide(PlayerGameInput input, out PadSide side)
	{
		side = input.Side;
		PlayerManager playerManager = GameUtils.RequireManager<PlayerManager>();
		GamepadUser user = playerManager.GetUser((EngagementSlot)input.Pad);
		if (user != null)
		{
			return user.ControlType == GamepadUser.ControlTypeEnum.Keyboard;
		}
		return false;
	}
}
