using System;
using System.Collections.Generic;

namespace Team17.Online
{
	public class UserSystemUtils
	{
		public const int kMaxUsers = 4;

		private static IOnlineMultiplayerSessionCoordinator m_iOnlineMultiplayerSessionCoordinator;

		public static GenericVoid<GameState, GameStateMessage.GameStatePayload> OnServerChangedGameState;

		public static void Initialise()
		{
			IOnlinePlatformManager onlinePlatformManager = GameUtils.RequireManagerInterface<IOnlinePlatformManager>();
			m_iOnlineMultiplayerSessionCoordinator = onlinePlatformManager.OnlineMultiplayerSessionCoordinator();
		}

		public static User FindUser(FastList<User> users, IOnlineMultiplayerSessionUserId sessionId = null, User.MachineID machineId = User.MachineID.Count, EngagementSlot slot = EngagementSlot.Count, TeamID team = TeamID.Count, User.SplitStatus splitStatus = User.SplitStatus.Count)
		{
			User[] array = FindUsers(users, sessionId, machineId, slot, team, splitStatus);
			if (array.Length > 0)
			{
				return array[0];
			}
			return null;
		}

		public static User[] FindUsers(FastList<User> users, IOnlineMultiplayerSessionUserId sessionId = null, User.MachineID machineId = User.MachineID.Count, EngagementSlot slot = EngagementSlot.Count, TeamID team = TeamID.Count, User.SplitStatus splitStatus = User.SplitStatus.Count)
		{
			if (users == null)
			{
				return null;
			}
			List<User> list = new List<User>();
			int count = users.Count;
			for (int i = 0; i < count; i++)
			{
				User user = users._items[i];
				if ((slot == EngagementSlot.Count || slot == user.Engagement) && (sessionId == null || sessionId == user.SessionId) && (machineId == User.MachineID.Count || machineId == user.Machine) && (slot == EngagementSlot.Count || slot == user.Engagement) && (team == TeamID.Count || team == user.Team) && (splitStatus == User.SplitStatus.Count || splitStatus == user.Split))
				{
					list.Add(user);
				}
			}
			return list.ToArray();
		}

		public static void ChangeGameState(GameState state, GameStateMessage.GameStatePayload payload = null)
		{
			FastList<User> users = ServerUserSystem.m_Users;
			int count = users.Count;
			for (int i = 0; i < count; i++)
			{
				User user = users._items[i];
				user.GameState = state;
			}
			ServerMessenger.GameState(state, payload);
			if (OnServerChangedGameState != null)
			{
				OnServerChangedGameState(state, payload);
			}
		}

		public static bool AreAllUsersInGameState(FastList<User> users, GameState state)
		{
			int count = users.Count;
			for (int i = 0; i < count; i++)
			{
				User user = users._items[i];
				if (user.GameState != state)
				{
					return false;
				}
			}
			return true;
		}

		public static bool AreAnyUsersInGameState(FastList<User> users, GameState state)
		{
			int count = users.Count;
			for (int i = 0; i < count; i++)
			{
				User user = users._items[i];
				if (user.GameState == state)
				{
					return true;
				}
			}
			return false;
		}

		public static void ResetUsersToOffline(ref FastList<User> users)
		{
			FastList<User> fastList = new FastList<User>(4);
			int count = users.Count;
			for (int i = 0; i < count; i++)
			{
				User user = users._items[i];
				if (user.IsLocal)
				{
					user.Machine = User.MachineID.One;
					user.SessionId = null;
					user.EntityID = 0u;
					user.Entity2ID = 0u;
					user.Team = TeamID.None;
				}
				else
				{
					fastList.Add(user);
				}
			}
			for (int j = 0; j < fastList.Count; j++)
			{
				users.Remove(fastList._items[j]);
			}
		}

		public static IOnlineMultiplayerSessionUserId GetSessionLocalUser()
		{
			if (m_iOnlineMultiplayerSessionCoordinator != null)
			{
				IOnlineMultiplayerSessionUserId[] array = m_iOnlineMultiplayerSessionCoordinator.Members();
				if (array != null)
				{
					for (int i = 0; i < array.Length; i++)
					{
						if (array[i].IsLocal)
						{
							return array[i];
						}
					}
				}
			}
			return null;
		}

		public static IOnlineMultiplayerSessionUserId GetSessionHostUser()
		{
			if (m_iOnlineMultiplayerSessionCoordinator != null)
			{
				IOnlineMultiplayerSessionUserId[] array = m_iOnlineMultiplayerSessionCoordinator.Members();
				if (array != null)
				{
					for (int i = 0; i < array.Length; i++)
					{
						if (array[i].IsHost)
						{
							return array[i];
						}
					}
				}
			}
			return null;
		}

		public static IOnlineMultiplayerSessionUserId GetSessionUserFromUniqueId(FastList<User> users, byte uniqueId)
		{
			IOnlineMultiplayerSessionUserId onlineMultiplayerSessionUserId = null;
			if (m_iOnlineMultiplayerSessionCoordinator != null)
			{
				IOnlineMultiplayerSessionUserId[] array = m_iOnlineMultiplayerSessionCoordinator.Members();
				if (array != null)
				{
					onlineMultiplayerSessionUserId = Array.Find(array, (IOnlineMultiplayerSessionUserId x) => x != null && x.UniqueId == uniqueId);
					if (onlineMultiplayerSessionUserId == null)
					{
					}
				}
			}
			return onlineMultiplayerSessionUserId;
		}

		public static bool AnyRemoteUsers()
		{
			if (m_iOnlineMultiplayerSessionCoordinator != null)
			{
				IOnlineMultiplayerSessionUserId[] array = m_iOnlineMultiplayerSessionCoordinator.Members();
				if (array != null)
				{
					IOnlineMultiplayerSessionUserId onlineMultiplayerSessionUserId = Array.Find(array, (IOnlineMultiplayerSessionUserId x) => x != null && !x.IsLocal);
					if (onlineMultiplayerSessionUserId != null)
					{
						return true;
					}
				}
			}
			return false;
		}

		public static bool AnySplitPadUsers()
		{
			FastList<User> users = ClientUserSystem.m_Users;
			for (int i = 0; i < users.Count; i++)
			{
				if (users._items[i].Split == User.SplitStatus.SplitPadHost || users._items[i].Split == User.SplitStatus.SplitPadGuest)
				{
					return true;
				}
			}
			return false;
		}

		public static uint LocalUserCount(FastList<User> users, bool includeSplitPadGuests)
		{
			uint num = 0u;
			int count = users.Count;
			for (int i = 0; i < count; i++)
			{
				User user = users._items[i];
				if (!user.IsLocal)
				{
					continue;
				}
				if (user.Split == User.SplitStatus.SplitPadGuest)
				{
					if (includeSplitPadGuests)
					{
						num++;
					}
				}
				else
				{
					num++;
				}
			}
			return num;
		}

		public static bool AtMaxUserCount()
		{
			return ServerUserSystem.m_Users.Count == 4;
		}

		public static void BuildGameInputConfig()
		{
			PlayerManager playerManager = GameUtils.RequireManager<PlayerManager>();
			if (playerManager == null)
			{
				return;
			}
			List<GameInputConfig.ConfigEntry> _configEntries = new List<GameInputConfig.ConfigEntry>();
			FastList<User> users = ClientUserSystem.m_Users;
			FastList<User> fastList = new FastList<User>();
			FastList<User> fastList2 = new FastList<User>();
			for (int i = 0; i < users.Count; i++)
			{
				User user = users._items[i];
				if (user.IsLocal)
				{
					fastList.Add(user);
				}
				else
				{
					fastList2.Add(user);
				}
			}
			for (int j = 0; j < fastList.Count; j++)
			{
				SetupUserConfig(playerManager, ref _configEntries, fastList._items[j], j);
			}
			for (int k = 0; k < fastList2.Count; k++)
			{
				SetupUserConfig(playerManager, ref _configEntries, fastList2._items[k], fastList.Count + k);
			}
			PadSplitManager.FixupConfigList(_configEntries, playerManager.UnsidedAmbiMapping);
			GameInputConfig baseInputConfig = new GameInputConfig(_configEntries.ToArray());
			PlayerInputLookup.SetBaseInputConfig(baseInputConfig);
		}

		private static void SetupUserConfig(PlayerManager _playerManager, ref List<GameInputConfig.ConfigEntry> _configEntries, User _user, int _index)
		{
			ControlPadInput.PadNum engagement = (ControlPadInput.PadNum)_user.Engagement;
			PadSide padSide = _user.PadSide;
			User.MachineID machine = _user.Machine;
			AmbiControlsMappingData mappingData = ((_user.PadSide != PadSide.Both) ? _playerManager.SidedAmbiMapping : _playerManager.UnsidedAmbiMapping);
			GameInputConfig.ConfigEntry item = new GameInputConfig.ConfigEntry((PlayerInputLookup.Player)_index, engagement, padSide, machine, mappingData);
			_configEntries.Add(item);
		}

		public static void RemoveAllSplitPadGuestUsers()
		{
			FastList<User> users = ServerUserSystem.m_Users;
			FastList<User> fastList = new FastList<User>();
			for (int i = 0; i < users.Count; i++)
			{
				if (users._items[i].Split == User.SplitStatus.SplitPadGuest)
				{
					fastList.Add(users._items[i]);
				}
			}
			for (int j = 0; j < fastList.Count; j++)
			{
				ServerUserSystem.RemoveUser(fastList._items[j]);
			}
		}

		public static void DisengageNonRequiredUsersForOnline(bool allowAllActiveLocalUsers)
		{
			RemoveAllSplitPadGuestUsers();
			if (!allowAllActiveLocalUsers)
			{
				bool engagementsLocked = ServerUserSystem.EngagementsLocked;
				ServerUserSystem.UnlockEngagement();
				IPlayerManager playerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
				FastList<User> users = ServerUserSystem.m_Users;
				while (users.Count > 1)
				{
					playerManager.DisengagePad(users._items[users.Count - 1].Engagement);
				}
				if (engagementsLocked)
				{
					ServerUserSystem.LockEngagement();
				}
			}
		}
	}
}
