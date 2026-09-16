using System;
using System.Collections.Generic;
using Team17.Online;

public class InviteMonitor
{
	public enum HandlerType
	{
		None = 0,
		Frontend = 1,
		Gameplay = 2
	}

	[Flags]
	public enum StatusFlags
	{
		HandlerIsValid = 1,
		HandlerIsIdle = 2,
		HandlerIsWaitingOnUserInput = 4,
		PendingInvite = 8
	}

	public static GenericVoid InviteAccepted;

	public static GenericVoid InviteJoinComplete;

	private static bool m_AcceptedInviteHandled = false;

	private static AcceptInviteData m_AcceptedInvite = null;

	private static OnlineMultiplayerSessionPlayTogetherHosting m_PlayTogetherHost = null;

	private IOnlineMultiplayerGameInviteCoordinator m_iOnlineMultiplayerGameInviteCoordinator;

	private IOnlineMultiplayerSessionCoordinator m_iOnlineMultiplayerSessionCoordinator;

	private SaveManager m_saveManager;

	private ScreenTransitionManager m_transitionManager;

	private IPlayerManager m_LocalPlayerManager;

	private static InviteHandler m_Handler = null;

	private static HandlerType m_HandlerType = HandlerType.None;

	private static FrontendInviteHandler m_FrontendHandler = new FrontendInviteHandler();

	private static GameplayInviteHandler m_GameplayHandler = new GameplayInviteHandler();

	private static Dictionary<HandlerType, InviteHandler> m_Handlers = new Dictionary<HandlerType, InviteHandler>();

	public static bool CheckStatus(StatusFlags flags)
	{
		bool flag = true;
		if ((flags & StatusFlags.HandlerIsValid) == StatusFlags.HandlerIsValid && m_Handler == null)
		{
			flag = false;
		}
		if ((flags & StatusFlags.HandlerIsIdle) == StatusFlags.HandlerIsIdle && m_Handler != null)
		{
			flag &= !m_Handler.IsBusy();
		}
		if ((flags & StatusFlags.HandlerIsWaitingOnUserInput) == StatusFlags.HandlerIsWaitingOnUserInput)
		{
			flag = m_Handler != null && (flag & m_Handler.IsAwaitingUserInput());
		}
		if ((flags & StatusFlags.PendingInvite) == StatusFlags.PendingInvite)
		{
			IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
			if (onlinePlatformManager != null)
			{
				IOnlineMultiplayerGameInviteCoordinator onlineMultiplayerGameInviteCoordinator = onlinePlatformManager.OnlineMultiplayerGameInviteCoordinator();
				flag = onlineMultiplayerGameInviteCoordinator != null && (flag & onlineMultiplayerGameInviteCoordinator.HasPendingAcceptedInvite());
			}
			else
			{
				flag = false;
			}
		}
		return flag;
	}

	public void Initialise()
	{
		m_saveManager = GameUtils.RequireManager<SaveManager>();
		IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
		m_iOnlineMultiplayerSessionCoordinator = onlinePlatformManager.OnlineMultiplayerSessionCoordinator();
		m_iOnlineMultiplayerGameInviteCoordinator = onlinePlatformManager.OnlineMultiplayerGameInviteCoordinator();
		m_LocalPlayerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
		m_Handlers.Add(HandlerType.None, null);
		m_Handlers.Add(HandlerType.Frontend, m_FrontendHandler);
		m_Handlers.Add(HandlerType.Gameplay, m_GameplayHandler);
	}

	public void Update()
	{
		if (m_LocalPlayerManager == null)
		{
			return;
		}
		if (m_transitionManager == null)
		{
			m_transitionManager = GameUtils.RequestManager<ScreenTransitionManager>();
		}
		bool flag = m_transitionManager != null && !m_transitionManager.IsIdle;
		bool isLoading = LoadingScreenFlow.IsLoading;
		bool flag2 = T17DialogBoxManager.HasAnyOpenDialogs() || m_LocalPlayerManager.IsWarningActive(PlayerWarning.Disengaged);
		bool isSaving = m_saveManager.IsSaving;
		if (m_iOnlineMultiplayerGameInviteCoordinator != null && m_iOnlineMultiplayerSessionCoordinator != null && !flag && !isLoading && !isSaving && !flag2)
		{
			OnlineMultiplayerSessionInvite onlineMultiplayerSessionInvite = m_iOnlineMultiplayerGameInviteCoordinator.InviteAccepted();
			OnlineMultiplayerSessionInvite onlineMultiplayerSessionInvite2 = null;
			while (onlineMultiplayerSessionInvite != null)
			{
				onlineMultiplayerSessionInvite2 = onlineMultiplayerSessionInvite;
				onlineMultiplayerSessionInvite = m_iOnlineMultiplayerGameInviteCoordinator.InviteAccepted();
			}
			if (onlineMultiplayerSessionInvite2 != null && (!ConnectionStatus.IsInSession() || !m_iOnlineMultiplayerSessionCoordinator.IsMemberAlready(onlineMultiplayerSessionInvite2)))
			{
				m_AcceptedInvite = new AcceptInviteData();
				m_AcceptedInvite.Invite = onlineMultiplayerSessionInvite2;
			}
			if (m_Handler != null)
			{
				OnlineMultiplayerSessionPlayTogetherHosting onlineMultiplayerSessionPlayTogetherHosting = m_iOnlineMultiplayerGameInviteCoordinator.PlayTogetherHosting();
				if (onlineMultiplayerSessionPlayTogetherHosting != null)
				{
					IPlayerManager playerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
					GamepadUser user = playerManager.GetUser(EngagementSlot.One);
					if (null != user && onlineMultiplayerSessionPlayTogetherHosting.WasAcceptedBy(user))
					{
						m_PlayTogetherHost = onlineMultiplayerSessionPlayTogetherHosting;
						m_Handler.HandlePlayTogetherHost(m_PlayTogetherHost);
					}
				}
			}
		}
		if (m_Handler == null)
		{
			return;
		}
		if (!m_AcceptedInviteHandled && m_AcceptedInvite != null)
		{
			GamepadUser gamepadUser = null;
			EngagementSlot engagementSlot = EngagementSlot.Count;
			IPlayerManager playerManager2 = GameUtils.RequireManagerInterface<IPlayerManager>();
			for (int i = 0; i < 4; i++)
			{
				GamepadUser user2 = playerManager2.GetUser((EngagementSlot)i);
				if (null != user2 && m_AcceptedInvite.Invite.WasAcceptedBy(user2))
				{
					gamepadUser = user2;
					engagementSlot = (EngagementSlot)i;
					break;
				}
			}
			if (m_LocalPlayerManager.SupportsInvitesForAnyUser() || (null != gamepadUser && engagementSlot == EngagementSlot.One))
			{
				m_AcceptedInvite.User = gamepadUser;
				m_AcceptedInviteHandled = true;
				m_Handler.HandleAcceptedInvite(m_AcceptedInvite);
			}
			else
			{
				m_AcceptedInvite = null;
			}
		}
		m_Handler.Update();
	}

	public static AcceptInviteData GetAcceptedInvite()
	{
		return m_AcceptedInvite;
	}

	public static OnlineMultiplayerSessionPlayTogetherHosting GetPlayTogetherHost()
	{
		return m_PlayTogetherHost;
	}

	public static void ClearInvite()
	{
		m_AcceptedInvite = null;
		m_AcceptedInviteHandled = false;
	}

	public static void ClearPlayTogetherHost()
	{
		m_PlayTogetherHost = null;
	}

	public static void SwitchHandlerType(HandlerType type)
	{
		InviteHandler inviteHandler = m_Handlers[type];
		if (inviteHandler != m_Handler)
		{
			if (m_Handler != null)
			{
				m_Handler.Stop();
			}
			m_Handler = inviteHandler;
			m_HandlerType = type;
			if (inviteHandler != null)
			{
				inviteHandler.Start();
			}
		}
	}
}
