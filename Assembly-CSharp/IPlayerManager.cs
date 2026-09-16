using System.Collections;
using Team17.Online;

public interface IPlayerManager
{
	event VoidGeneric<EngagementSlot, GamepadUser, GamepadUser> EngagementChangeCallback;

	GamepadUser GetUser(EngagementSlot _slot);

	ControlPadInput.PadNum GetEngagementPad(out EngagmentCircumstances o_circumstances);

	bool HasPlayer();

	bool HasSavablePlayer();

	bool IsEngagingSlot(EngagementSlot slot);

	bool HasFreeEngagementSlot();

	bool IsBusy();

	bool IsWarningActive(PlayerWarning warning);

	IEnumerator RunGameownerEngagement(ControlPadInput.PadNum _engagingPadNum, EngagmentCircumstances _circumstances);

	void StartGameownerEngagement(ControlPadInput.PadNum _engagingPadNum, EngagmentCircumstances _circumstances, VoidGeneric<GamepadUser> _finishedCall);

	void StartPadEngagement(ControlPadInput.PadNum _engagingPadNum, EngagmentCircumstances _circumstances, VoidGeneric<GamepadUser> _finishedCall);

	void DisengagePad(EngagementSlot _intendedSlot);

	void ShowGamerCard(EngagementSlot slot);

	void ShowGamerCard(GamepadUser localUser);

	void ShowGamerCard(OnlineUserPlatformId onlineUser);

	bool SupportsInvitesForAnyUser();
}
