using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

namespace Team17.Online
{
	public class ClientUserSystem
	{
		private class AvatarImageCacheEntry
		{
			public Texture2D m_image;

			public ulong m_uniqueRequestId;

			public User.MachineID m_machineId = User.MachineID.Count;

			public EngagementSlot m_engagementSlot = EngagementSlot.Count;

			public void Clear()
			{
				if (null != m_image)
				{
					Object.Destroy(m_image);
				}
				m_image = null;
				m_uniqueRequestId = 0uL;
				m_machineId = User.MachineID.Count;
				m_engagementSlot = EngagementSlot.Count;
			}
		}

		private static IOnlineAvatarImageCoordinator m_avatarCoordinator = null;

		public static User.MachineID s_LocalMachineId = User.MachineID.One;

		public static FastList<User> m_Users = new FastList<User>(4);

		private static FastList<AvatarImageCacheEntry> m_avatarImageCache = new FastList<AvatarImageCacheEntry>(4);

		public static GenericVoid usersChanged;

		public static GenericVoid<uint, UserData> userAdded;

		private UsersChangedMessage m_UsersChanged = new UsersChangedMessage();

		public void Initialise()
		{
			Mailbox.Client.UnregisterForMessageType(MessageType.UsersChanged, OnUsersChanged);
			Mailbox.Client.RegisterForMessageType(MessageType.UsersChanged, OnUsersChanged);
			Mailbox.Client.UnregisterForMessageType(MessageType.UsersAdded, OnUserAdded);
			Mailbox.Client.RegisterForMessageType(MessageType.UsersAdded, OnUserAdded);
			GameUtils.RequireManager<PlayerManager>().EngagementChangeCallback += OnEngagementChanged;
		}

		private void OnEngagementChanged(EngagementSlot slot, GamepadUser user, GamepadUser gamepad)
		{
			FastList<User> users = m_Users;
			User.MachineID machineId = s_LocalMachineId;
			User user2 = UserSystemUtils.FindUser(users, null, machineId, slot);
			if (user2 != null)
			{
				user2.Engagement = slot;
			}
			if (user != null && gamepad != null && user.UID != gamepad.UID)
			{
				for (int num = m_avatarImageCache.Count - 1; num >= 0; num--)
				{
					AvatarImageCacheEntry avatarImageCacheEntry = m_avatarImageCache._items[num];
					if (avatarImageCacheEntry != null && avatarImageCacheEntry.m_engagementSlot == slot)
					{
						avatarImageCacheEntry.Clear();
						m_avatarImageCache.RemoveAt(num);
					}
				}
			}
			UpdateAvatarImageCache();
		}

		public void Clear()
		{
			Mailbox.Client.UnregisterForMessageType(MessageType.UsersChanged, OnUsersChanged);
			Mailbox.Client.UnregisterForMessageType(MessageType.UsersAdded, OnUserAdded);
			m_Users.Clear();
			GameUtils.RequireManager<PlayerManager>().EngagementChangeCallback -= OnEngagementChanged;
			ClearAvatarImageCache();
		}

		public static void Update()
		{
		}

		public static void SetMachineId(User.MachineID eMachine)
		{
			for (int i = 0; i < m_Users.Count; i++)
			{
				User user = m_Users._items[i];
				if (user.Machine == s_LocalMachineId)
				{
					user.Machine = eMachine;
				}
			}
			s_LocalMachineId = eMachine;
		}

		public static void ClearAvatarImageCache()
		{
			for (int i = 0; i < m_avatarImageCache.Count; i++)
			{
				m_avatarImageCache._items[i].Clear();
			}
			m_avatarImageCache.Clear();
		}

		public void OnUserAdded(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
		{
			UserAddedMessage userAddedMessage = (UserAddedMessage)message;
			if (userAdded != null)
			{
				userAdded(userAddedMessage.UserIndex, userAddedMessage.User);
			}
		}

		public void OnUsersChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
		{
			m_UsersChanged = (UsersChangedMessage)message;
			for (int i = 0; i < m_UsersChanged.m_Users.Count; i++)
			{
				UserData userData = m_UsersChanged.m_Users._items[i];
				IOnlineMultiplayerSessionUserId sessionUserFromUniqueId = UserSystemUtils.GetSessionUserFromUniqueId(m_Users, userData.sessionUserUniqueId);
				User user = null;
				if (i < m_Users.Count)
				{
					user = m_Users._items[i];
				}
				if (user != null)
				{
					user.IsLocal = userData.machine == s_LocalMachineId;
					user.Machine = userData.machine;
					user.SessionId = sessionUserFromUniqueId;
					user.EntityID = userData.entity;
					user.Entity2ID = userData.entity2;
					user.Engagement = userData.slot;
					user.GameState = userData.gameState;
					user.Team = userData.team;
					user.Colour = userData.colour;
					user.SelectedChefAvatar = userData.selectedChefAvatar;
					user.PadSide = userData.padSide;
					user.Split = userData.splitStatus;
					user.PartyPersist = userData.partyPersist;
					user.DisplayName = userData.displayName;
					user.PlatformID = userData.platformId;
					user.SelectedChefAvatar = userData.selectedChefAvatar;
				}
				else
				{
					AddUser(userData.machine == s_LocalMachineId, userData.machine, sessionUserFromUniqueId, userData.entity, userData.entity2, userData.slot, userData.team, userData.colour, userData.selectedChefAvatar, userData.padSide, userData.splitStatus, userData.partyPersist, userData.displayName, userData.platformId, userData.selectedChefAvatar);
				}
			}
			if (m_UsersChanged.m_Users.Count < m_Users.Count)
			{
				int count = m_Users.Count - m_UsersChanged.m_Users.Count;
				m_Users.RemoveRange(m_UsersChanged.m_Users.Count, count);
			}
			UpdateAvatarImageCache();
			if (usersChanged != null)
			{
				usersChanged();
			}
		}

		public static Texture2D GetAvatarImage(User user)
		{
			if (user != null)
			{
				for (int i = 0; i < m_avatarImageCache.Count; i++)
				{
					AvatarImageCacheEntry avatarImageCacheEntry = m_avatarImageCache._items[i];
					if (avatarImageCacheEntry.m_machineId == user.Machine && avatarImageCacheEntry.m_engagementSlot == user.Engagement)
					{
						return avatarImageCacheEntry.m_image;
					}
				}
			}
			return null;
		}

		private User AddUser(bool bLocal = false, User.MachineID machine = User.MachineID.Count, IOnlineMultiplayerSessionUserId sessionId = null, uint entityId = 0u, uint entity2Id = 0u, EngagementSlot engagement = EngagementSlot.Count, TeamID team = TeamID.None, uint colour = 7u, uint avatar = 127u, PadSide padSide = PadSide.Both, User.SplitStatus splitStatus = User.SplitStatus.Count, User.PartyPersistance partyPersist = User.PartyPersistance.NotSet, string displayName = "Unknown", OnlineUserPlatformId platformUserId = null, uint chefAvatarID = 127u)
		{
			User user = new User(bLocal, machine, sessionId, entityId, entity2Id, engagement, team, colour, avatar, padSide, splitStatus, partyPersist, displayName, platformUserId, chefAvatarID);
			m_Users.Add(user);
			return user;
		}

		private static void UpdateAvatarImageCache()
		{
			if (m_avatarCoordinator == null)
			{
				m_avatarCoordinator = GameUtils.RequireManagerInterface<IOnlinePlatformManager>().OnlineAvatarImageCoordinator();
			}
			if (m_avatarCoordinator == null)
			{
				return;
			}
			int count = m_avatarImageCache.Count;
			if (count > 0)
			{
				for (int num = count - 1; num >= 0; num--)
				{
					AvatarImageCacheEntry avatarImageCacheEntry = m_avatarImageCache._items[num];
					bool flag = false;
					for (int i = 0; i < m_Users.Count; i++)
					{
						if (flag)
						{
							break;
						}
						User user = m_Users._items[i];
						if (avatarImageCacheEntry.m_machineId == user.Machine && avatarImageCacheEntry.m_engagementSlot == user.Engagement)
						{
							flag = true;
						}
					}
					if (!flag)
					{
						avatarImageCacheEntry.Clear();
						m_avatarImageCache.RemoveAt(num);
					}
				}
			}
			User user2 = null;
			for (int j = 0; j < m_Users.Count; j++)
			{
				User user3 = m_Users._items[j];
				bool flag2 = false;
				for (int k = 0; k < m_avatarImageCache.Count; k++)
				{
					if (flag2)
					{
						break;
					}
					AvatarImageCacheEntry avatarImageCacheEntry2 = m_avatarImageCache._items[k];
					if (avatarImageCacheEntry2.m_machineId == user3.Machine && avatarImageCacheEntry2.m_engagementSlot == user3.Engagement)
					{
						flag2 = true;
					}
				}
				if (flag2)
				{
					continue;
				}
				if (user3.IsLocal)
				{
					ulong uniqueRequestId = 0uL;
					if (m_avatarCoordinator.RequestAvatarImage(user3.GamepadUser, OnAvatarImageRequestCompletionCallback, out uniqueRequestId))
					{
						m_avatarImageCache.Add(new AvatarImageCacheEntry
						{
							m_uniqueRequestId = uniqueRequestId,
							m_machineId = user3.Machine,
							m_engagementSlot = user3.Engagement
						});
					}
					continue;
				}
				if (user2 == null)
				{
					for (int l = 0; l < m_Users.Count; l++)
					{
						if (user2 != null)
						{
							break;
						}
						User user4 = m_Users._items[l];
						if (user4.IsLocal && user4.Engagement == EngagementSlot.One)
						{
							user2 = user4;
						}
					}
				}
				if (user2 != null && null != user2.GamepadUser)
				{
					ulong uniqueRequestId2 = 0uL;
					if (m_avatarCoordinator.RequestAvatarImage(user2.GamepadUser, user3.PlatformID, OnAvatarImageRequestCompletionCallback, out uniqueRequestId2))
					{
						m_avatarImageCache.Add(new AvatarImageCacheEntry
						{
							m_uniqueRequestId = uniqueRequestId2,
							m_machineId = user3.Machine,
							m_engagementSlot = user3.Engagement
						});
					}
				}
			}
		}

		private static void OnAvatarImageRequestCompletionCallback(Texture2D image, ulong uniqueRequestId)
		{
			bool flag = false;
			for (int i = 0; i < m_avatarImageCache.Count; i++)
			{
				if (flag)
				{
					break;
				}
				AvatarImageCacheEntry avatarImageCacheEntry = m_avatarImageCache._items[i];
				if (uniqueRequestId == avatarImageCacheEntry.m_uniqueRequestId)
				{
					flag = true;
					if (null != avatarImageCacheEntry.m_image)
					{
						Object.Destroy(avatarImageCacheEntry.m_image);
					}
					avatarImageCacheEntry.m_image = image;
					avatarImageCacheEntry.m_uniqueRequestId = 0uL;
				}
			}
			if (flag)
			{
				if (usersChanged != null)
				{
					usersChanged();
				}
			}
			else
			{
				Object.Destroy(image);
			}
		}
	}
}
