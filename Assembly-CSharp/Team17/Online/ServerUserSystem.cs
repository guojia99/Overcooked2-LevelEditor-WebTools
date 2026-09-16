using System;
using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;

namespace Team17.Online
{
	public class ServerUserSystem
	{
		public static User.MachineID s_LocalMachineId = User.MachineID.One;

		public static FastList<User> m_Users = new FastList<User>(4);

		private IPlayerManager m_IPlayerManager;

		private static ServerDropInUserMonitor m_PrivilegeChecker = new ServerDropInUserMonitor();

		private static bool m_ignoreEngagement = false;

		public static GenericVoid<User> OnUserRemoved = null;

		public static GenericVoid<User, int> OnUserRemovedWithIndex = null;

		public static GenericVoid<User> OnUserAdded = null;

		public static GenericVoid<IConnectionModeSwitchStatus> OnEngagementPrivilegeCheckStarted;

		public static GenericVoid<IConnectionModeSwitchStatus> OnEngagementPrivilegeCheckCompleted;

		public static bool EngagementsLocked
		{
			get
			{
				return m_ignoreEngagement;
			}
		}

		public void Initialise()
		{
			m_IPlayerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
			m_IPlayerManager.EngagementChangeCallback += OnEngagementChanged;
			m_PrivilegeChecker.Initialise(EngagementPrivilegeCheckStarted, EngagementPrivilegeCheckComplete);
			Mailbox.Server.RegisterForMessageType(MessageType.GameState, OnClientGameStateChanged);
			Mailbox.Server.RegisterForMessageType(MessageType.ChefAvatar, OnClientChefAvatarChanged);
			Mailbox.Server.RegisterForMessageType(MessageType.ControllerSettings, OnClientControllerSettingsChanged);
		}

		public void Shutdown()
		{
			m_IPlayerManager.EngagementChangeCallback -= OnEngagementChanged;
			Mailbox.Server.UnregisterForMessageType(MessageType.GameState, OnClientGameStateChanged);
			Mailbox.Server.UnregisterForMessageType(MessageType.ChefAvatar, OnClientChefAvatarChanged);
			Mailbox.Server.UnregisterForMessageType(MessageType.ControllerSettings, OnClientControllerSettingsChanged);
			m_PrivilegeChecker.Shutdown();
			m_Users.Clear();
		}

		public void InvalidateEntities()
		{
			int count = m_Users.Count;
			for (int i = 0; i < count; i++)
			{
				m_Users._items[i].EntityID = 0u;
				m_Users._items[i].Entity2ID = 0u;
			}
		}

		public static void RemoveMatchmadeUsers()
		{
			FastList<User> fastList = new FastList<User>(4);
			int count = m_Users.Count;
			for (int i = 0; i < count; i++)
			{
				User user = m_Users._items[i];
				if (user.PartyPersist == User.PartyPersistance.Kick)
				{
					fastList.Add(user);
				}
			}
			for (int j = 0; j < fastList.Count; j++)
			{
				RemoveUser(fastList._items[j]);
			}
		}

		public static void ResetTeams()
		{
			int count = m_Users.Count;
			for (int i = 0; i < count; i++)
			{
				User user = m_Users._items[i];
				user.Team = TeamID.None;
			}
		}

		private void OnClientGameStateChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
		{
			GameStateMessage gameStateMessage = (GameStateMessage)message;
			int count = m_Users.Count;
			for (int i = 0; i < count; i++)
			{
				User user = m_Users._items[i];
				if (user.Machine == gameStateMessage.m_Machine)
				{
					user.GameState = gameStateMessage.m_State;
				}
			}
		}

		private void OnClientChefAvatarChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
		{
			ChefAvatarMessage chefAvatarMessage = (ChefAvatarMessage)message;
			FastList<User> users = m_Users;
			IOnlineMultiplayerSessionUserId sessionId = null;
			User.MachineID machine = chefAvatarMessage.m_Machine;
			EngagementSlot engagementSlot = chefAvatarMessage.m_EngagementSlot;
			User.SplitStatus split = chefAvatarMessage.m_Split;
			User user = UserSystemUtils.FindUser(users, sessionId, machine, engagementSlot, TeamID.Count, split);
			if (user != null)
			{
				user.SelectedChefAvatar = chefAvatarMessage.m_ChefAvatar;
			}
		}

		private void OnClientControllerSettingsChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
		{
			ControllerSettingsMessage controllerSettingsMessage = (ControllerSettingsMessage)message;
			FastList<User> users = m_Users;
			IOnlineMultiplayerSessionUserId sessionId = null;
			User.MachineID machine = controllerSettingsMessage.m_Machine;
			EngagementSlot engagementSlot = controllerSettingsMessage.m_EngagementSlot;
			User.SplitStatus split = controllerSettingsMessage.m_Split;
			User user = UserSystemUtils.FindUser(users, sessionId, machine, engagementSlot, TeamID.Count, split);
			if (user != null)
			{
				user.PadSide = controllerSettingsMessage.m_side;
			}
		}

		public void Update()
		{
			m_PrivilegeChecker.Update();
			int count = m_Users.Count;
			bool flag = false;
			for (int i = 0; i < count; i++)
			{
				if (m_Users._items[i].ChangedThisFrame)
				{
					flag = true;
				}
			}
			if (!flag || ConnectionModeSwitcher.GetStatus().GetProgress() != eConnectionModeSwitchProgress.Complete || ConnectionModeSwitcher.GetStatus().GetResult() != eConnectionModeSwitchResult.Success)
			{
				return;
			}
			for (int j = 0; j < count; j++)
			{
				if (m_Users._items[j].ChangedThisFrame)
				{
					m_Users._items[j].ChangedThisFrame = false;
				}
			}
			ServerMessenger.UsersChanged();
		}

		public static User AddUser(bool bLocal = false, User.MachineID machine = User.MachineID.Count, IOnlineMultiplayerSessionUserId sessionId = null, uint entityId = 0u, uint entity2Id = 0u, EngagementSlot engagement = EngagementSlot.Count, PadSide side = PadSide.Both, TeamID team = TeamID.None, User.PartyPersistance partyPersist = User.PartyPersistance.NotSet, OnlineUserPlatformId platformID = null, uint chefAvatarID = 127u, string remoteDisplayName = null, User.SplitStatus splitStatus = User.SplitStatus.NotSplit)
		{
			if (m_Users.Count >= 4)
			{
				return null;
			}
			uint num = (uint)m_Users.Count;
			User.MachineID machineId;
			if (bLocal)
			{
				if ((uint)engagement <= m_Users.Count)
				{
					num = (uint)engagement;
				}
				FastList<User> users = m_Users;
				machineId = machine;
				User user = UserSystemUtils.FindUser(users, null, machineId, engagement);
				if (user != null)
				{
					splitStatus = User.SplitStatus.SplitPadGuest;
					side = ((user.PadSide != PadSide.Right) ? PadSide.Right : PadSide.Left);
					user.Split = User.SplitStatus.SplitPadHost;
					user.PadSide = ((side != PadSide.Right) ? PadSide.Right : PadSide.Left);
					num = (uint)m_Users.Count;
				}
			}
			uint num2 = num;
			string displayName = "Unknown";
			if (bLocal)
			{
				IPlayerManager playerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
				if (playerManager != null)
				{
					GamepadUser user2 = playerManager.GetUser(engagement);
					if (user2 != null)
					{
						OnlineMultiplayerLocalUserId allowedUser = PrivilegeCheckCache.GetAllowedUser(user2);
						if (allowedUser != null)
						{
							if (platformID == null)
							{
								platformID = allowedUser.m_platformId;
							}
							displayName = allowedUser.m_userName;
						}
						else if (user2 != null)
						{
							displayName = user2.DisplayName;
						}
					}
				}
			}
			else if (remoteDisplayName != null)
			{
				displayName = remoteDisplayName;
			}
			bool bLocal2 = bLocal;
			machineId = machine;
			uint colour = num2;
			PadSide padSide = side;
			User user3 = new User(bLocal2, machineId, sessionId, entityId, entity2Id, engagement, team, colour, 127u, padSide, splitStatus, partyPersist, displayName, platformID, chefAvatarID);
			if (num >= m_Users.Count)
			{
				m_Users.Add(user3);
			}
			else
			{
				m_Users.Insert((int)num, user3);
			}
			UpdatePlayersColourAssignments();
			if (OnUserAdded != null)
			{
				OnUserAdded(user3);
			}
			ServerMessenger.UserAdded(num, user3);
			return user3;
		}

		public static void RemoveUser(User user, bool fireUsersChanged = true)
		{
			User user2 = null;
			if (user.Split == User.SplitStatus.SplitPadHost)
			{
				FastList<User> users = m_Users;
				User.MachineID machineId = s_LocalMachineId;
				user2 = UserSystemUtils.FindUser(users, null, machineId, user.Engagement, TeamID.Count, User.SplitStatus.SplitPadGuest);
			}
			else if (user.Split == User.SplitStatus.SplitPadGuest)
			{
				FastList<User> users = m_Users;
				User.MachineID machineId = s_LocalMachineId;
				User user3 = UserSystemUtils.FindUser(users, null, machineId, user.Engagement, TeamID.Count, User.SplitStatus.SplitPadHost);
				if (user3 != null)
				{
					user3.Split = User.SplitStatus.NotSplit;
					user3.PadSide = PadSide.Both;
				}
			}
			int param = m_Users.FindIndex((User x) => x == user);
			if (m_Users.Remove(user))
			{
				if (user2 != null)
				{
					m_Users.Remove(user2);
				}
				UpdatePlayersColourAssignments();
				if (OnUserRemoved != null)
				{
					OnUserRemoved(user);
				}
				if (OnUserRemovedWithIndex != null)
				{
					OnUserRemovedWithIndex(user, param);
				}
				if (fireUsersChanged)
				{
					ServerMessenger.UsersChanged();
				}
			}
		}

		public static void LockEngagement()
		{
			m_ignoreEngagement = true;
		}

		public static void UnlockEngagement()
		{
			m_ignoreEngagement = false;
		}

		public void OnEngagementChanged(EngagementSlot _e, GamepadUser _prev, GamepadUser _new)
		{
			FastList<User> users = m_Users;
			User.MachineID machineId = s_LocalMachineId;
			User user = UserSystemUtils.FindUser(users, null, machineId, _e);
			if (user != null)
			{
				if (_new == null && _prev != null)
				{
					if ((_e != EngagementSlot.One || !user.IsLocal) && !m_ignoreEngagement)
					{
						RemoveUser(user);
					}
				}
				else
				{
					user.Engagement = _e;
				}
			}
			else if (null != _new && !m_ignoreEngagement)
			{
				if (ConnectionModeSwitcher.GetRequestedConnectionState() == NetConnectionState.Offline)
				{
					bool bLocal = true;
					machineId = s_LocalMachineId;
					IOnlineMultiplayerSessionUserId sessionId = null;
					AddUser(bLocal, machineId, sessionId, 0u, 0u, _e, PadSide.Both, TeamID.None, User.PartyPersistance.Remain);
				}
				else if (m_PrivilegeChecker.GetStatus().GetProgress() != eConnectionModeSwitchProgress.InProgress && ConnectionModeSwitcher.GetStatus().GetProgress() == eConnectionModeSwitchProgress.Complete && ConnectionModeSwitcher.GetStatus().GetResult() == eConnectionModeSwitchResult.Success)
				{
					m_PrivilegeChecker.TriggerDropIn();
				}
			}
		}

		public static bool SplitUser(EngagementSlot _e)
		{
			if (!ConnectionStatus.IsInSession() && ConnectionModeSwitcher.GetRequestedConnectionState() == NetConnectionState.Offline)
			{
				FastList<User> users = m_Users;
				User.MachineID machineId = s_LocalMachineId;
				User user = UserSystemUtils.FindUser(users, null, machineId, _e, TeamID.Count, User.SplitStatus.NotSplit);
				if (user != null)
				{
					bool bLocal = true;
					machineId = s_LocalMachineId;
					IOnlineMultiplayerSessionUserId sessionId = null;
					AddUser(bLocal, machineId, sessionId, 0u, 0u, _e, PadSide.Both, TeamID.None, User.PartyPersistance.Remain);
					return true;
				}
			}
			return false;
		}

		public void ResetUsersToOfflineState()
		{
			UserSystemUtils.ResetUsersToOffline(ref m_Users);
			s_LocalMachineId = User.MachineID.One;
			for (int i = 0; i < 4; i++)
			{
				EngagementSlot engagementSlot = (EngagementSlot)i;
				GamepadUser user = m_IPlayerManager.GetUser(engagementSlot);
				if (!(user != null))
				{
					continue;
				}
				FastList<User> users = m_Users;
				EngagementSlot slot = engagementSlot;
				User user2 = UserSystemUtils.FindUser(users, null, User.MachineID.Count, slot);
				if (user2 != null)
				{
					user2.Machine = User.MachineID.One;
					continue;
				}
				UserSystemUtils.ResetUsersToOffline(ref ClientUserSystem.m_Users);
				users = ClientUserSystem.m_Users;
				slot = engagementSlot;
				user2 = UserSystemUtils.FindUser(users, null, User.MachineID.Count, slot);
				if (user2 != null)
				{
					bool bLocal = true;
					User.MachineID machine = s_LocalMachineId;
					slot = engagementSlot;
					PadSide padSide = user2.PadSide;
					AddUser(bLocal, machine, null, 0u, 0u, slot, padSide, TeamID.None, User.PartyPersistance.Remain, null, user2.SelectedChefAvatar);
				}
				else
				{
					bool bLocal = true;
					User.MachineID machine = s_LocalMachineId;
					slot = engagementSlot;
					AddUser(bLocal, machine, null, 0u, 0u, slot, PadSide.Both, TeamID.None, User.PartyPersistance.Remain);
				}
			}
			ServerMessenger.UsersChanged();
		}

		public void ResetUsersToOnlineState(OnlineMultiplayerLocalUserId localUserId)
		{
			IOnlineMultiplayerSessionUserId sessionHostUser = UserSystemUtils.GetSessionHostUser();
			for (int i = 0; i < 4; i++)
			{
				EngagementSlot engagementSlot = (EngagementSlot)i;
				GamepadUser user = m_IPlayerManager.GetUser(engagementSlot);
				if (!(user != null))
				{
					continue;
				}
				OnlineMultiplayerLocalUserId allowedUser = PrivilegeCheckCache.GetAllowedUser(user);
				OnlineUserPlatformId platformID = ((allowedUser == null) ? null : allowedUser.m_platformId);
				User user2 = UserSystemUtils.FindUser(m_Users, null, s_LocalMachineId, engagementSlot);
				if (user2 != null)
				{
					if (engagementSlot == EngagementSlot.One)
					{
						user2.SessionId = sessionHostUser;
					}
					user2.PlatformID = platformID;
				}
				else if (engagementSlot == EngagementSlot.One)
				{
					bool bLocal = true;
					User.MachineID machine = s_LocalMachineId;
					IOnlineMultiplayerSessionUserId sessionId = sessionHostUser;
					EngagementSlot engagement = engagementSlot;
					AddUser(bLocal, machine, sessionId, 0u, 0u, engagement, PadSide.Both, TeamID.None, User.PartyPersistance.Remain, platformID);
				}
			}
			ServerMessenger.UsersChanged();
		}

		public User AddNewRemoteUser(User.MachineID machineId, IOnlineMultiplayerSessionUserId sessionUser, JoinDataProvider.GameUserData gameData)
		{
			if (m_Users.Count == 4)
			{
				return null;
			}
			bool bLocal = false;
			EngagementSlot slot = gameData.Slot;
			PadSide controllerSide = gameData.ControllerSide;
			uint avatarId = gameData.AvatarId;
			return AddUser(bLocal, machineId, sessionUser, 0u, 0u, slot, controllerSide, TeamID.None, NetworkUtils.GetRemoteUserPartyPersistanceForJoinState(gameData.JoinMethod), gameData.PlatformId, avatarId, gameData.DisplayName, gameData.ControllerSplitStatus);
		}

		public static User.MachineID GetAvailableMachineId()
		{
			User user = null;
			Array values = Enum.GetValues(typeof(User.MachineID));
			int length = values.Length;
			for (int i = 0; i < length; i++)
			{
				User.MachineID machineID = (User.MachineID)values.GetValue(i);
				FastList<User> users = m_Users;
				User.MachineID machineId = machineID;
				user = UserSystemUtils.FindUser(users, null, machineId);
				if (user == null)
				{
					return machineID;
				}
			}
			return User.MachineID.Count;
		}

		private static void UpdatePlayersColourAssignments()
		{
			for (int i = 0; i < m_Users.Count; i++)
			{
				m_Users._items[i].Colour = (uint)i;
			}
		}

		public static IConnectionModeSwitchStatus GetEngagementPrivilegeCheckStatus()
		{
			return m_PrivilegeChecker.GetStatus();
		}

		private void EngagementPrivilegeCheckStarted(IConnectionModeSwitchStatus status)
		{
			if (OnEngagementPrivilegeCheckStarted != null)
			{
				OnEngagementPrivilegeCheckStarted(status);
			}
		}

		private void EngagementPrivilegeCheckComplete(IConnectionModeSwitchStatus status)
		{
			if (OnEngagementPrivilegeCheckCompleted != null)
			{
				OnEngagementPrivilegeCheckCompleted(status);
			}
		}
	}
}
