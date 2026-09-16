using System;

public class RichPresenceMangerBase : Manager
{
	protected const string sc_onlineKitchenId = "OnlineKitchen";

	protected const string sc_campaignId = "Campaign";

	protected const string sc_versusId = "Versus";

	protected const string sc_partyId = "Party";

	protected const string sc_engagedId = "Engaged";

	protected static GameMode m_gameMode = GameMode.COUNT;

	protected static GenericVoid OnGameModeSet;

	protected PlayerManager m_playerManager;

	private void Awake()
	{
		m_playerManager = GameUtils.RequireManager<PlayerManager>();
		m_playerManager.EngagementChangeCallback += OnEngagementChanged;
		OnGameModeSet = (GenericVoid)Delegate.Combine(OnGameModeSet, new GenericVoid(OnNewGameMode));
		Initialise();
	}

	private void OnDestroy()
	{
		m_playerManager.EngagementChangeCallback -= OnEngagementChanged;
		OnGameModeSet = (GenericVoid)Delegate.Remove(OnGameModeSet, new GenericVoid(OnNewGameMode));
	}

	private void OnNewGameMode()
	{
		RefreshPresence();
	}

	private void OnEngagementChanged(EngagementSlot _slot, GamepadUser _prevUser, GamepadUser _nextUser)
	{
		if (_nextUser != null)
		{
			RefreshPresence();
		}
		if (_prevUser != null)
		{
			OnUserDisengaged(_prevUser);
		}
	}

	protected virtual void Initialise()
	{
	}

	protected virtual bool UserIsValidForPresence(GamepadUser user)
	{
		return true;
	}

	private void RefreshPresence()
	{
		for (int i = 0; i < 4; i++)
		{
			EngagementSlot slot = (EngagementSlot)i;
			GamepadUser user = m_playerManager.GetUser(slot);
			if (UserIsValidForPresence(user))
			{
				switch (m_gameMode)
				{
				case GameMode.OnlineKitchen:
					SetOnlineKitchenPresence(user);
					break;
				case GameMode.Campaign:
					SetCampaignPresence(user);
					break;
				case GameMode.Party:
					SetPartyPresence(user);
					break;
				case GameMode.Versus:
					SetVersusPresence(user);
					break;
				default:
					SetDefaultPresence(user);
					break;
				}
			}
		}
	}

	protected virtual void SetDefaultPresence(GamepadUser _user)
	{
	}

	protected virtual void SetOnlineKitchenPresence(GamepadUser _user)
	{
	}

	protected virtual void SetCampaignPresence(GamepadUser _user)
	{
	}

	protected virtual void SetPartyPresence(GamepadUser _user)
	{
	}

	protected virtual void SetVersusPresence(GamepadUser _user)
	{
	}

	protected virtual void OnUserDisengaged(GamepadUser _user)
	{
	}
}
