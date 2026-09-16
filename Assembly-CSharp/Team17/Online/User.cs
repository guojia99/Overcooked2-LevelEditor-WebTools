namespace Team17.Online
{
	public class User
	{
		public enum MachineID
		{
			One = 0,
			Two = 1,
			Three = 2,
			Four = 3,
			Count = 4
		}

		public enum SplitStatus
		{
			SplitPadHost = 0,
			SplitPadGuest = 1,
			NotSplit = 2,
			Count = 3
		}

		public enum PartyPersistance
		{
			NotSet = 0,
			Remain = 1,
			Kick = 2
		}

		public const uint kInvalidChefAvatar = 127u;

		public const uint kInvalidChefColour = 7u;

		public bool ChangedThisFrame;

		private MachineID m_MachineID = MachineID.Count;

		private IOnlineMultiplayerSessionUserId m_SessionId;

		private uint m_uEntityID;

		private uint m_uEntity2ID;

		private EngagementSlot m_Engagement = EngagementSlot.Count;

		private TeamID m_Team = TeamID.None;

		private uint m_Colour = 7u;

		private GameState m_GameState;

		private uint m_uSelectedChefAvatar = 127u;

		private GameSession.SelectedChefData m_SelectedChefData;

		private bool m_bIsLocal = true;

		private GamepadUser m_GamepadUser;

		private PadSide m_padSide = PadSide.Both;

		private SplitStatus m_splitStatus = SplitStatus.Count;

		private PartyPersistance m_partyPersist;

		private string m_displayName = "Unknown";

		private OnlineUserPlatformId m_PlatformID;

		public MachineID Machine
		{
			get
			{
				return m_MachineID;
			}
			set
			{
				SetProperty(ref m_MachineID, value);
			}
		}

		public IOnlineMultiplayerSessionUserId SessionId
		{
			get
			{
				return m_SessionId;
			}
			set
			{
				SetProperty(ref m_SessionId, value);
			}
		}

		public uint EntityID
		{
			get
			{
				return m_uEntityID;
			}
			set
			{
				SetProperty(ref m_uEntityID, value);
			}
		}

		public uint Entity2ID
		{
			get
			{
				return m_uEntity2ID;
			}
			set
			{
				SetProperty(ref m_uEntity2ID, value);
			}
		}

		public EngagementSlot Engagement
		{
			get
			{
				return m_Engagement;
			}
			set
			{
				SetProperty(ref m_Engagement, value);
				if (IsLocal)
				{
					IPlayerManager playerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
					GamepadUser = playerManager.GetUser(Engagement);
					if (GamepadUser != null)
					{
						DisplayName = GamepadUser.DisplayName;
					}
				}
			}
		}

		public TeamID Team
		{
			get
			{
				return m_Team;
			}
			set
			{
				SetProperty(ref m_Team, value);
			}
		}

		public uint Colour
		{
			get
			{
				return m_Colour;
			}
			set
			{
				SetProperty(ref m_Colour, value);
				RefreshSelectedChefData();
			}
		}

		public GameState GameState
		{
			get
			{
				return m_GameState;
			}
			set
			{
				SetProperty(ref m_GameState, value);
			}
		}

		public uint SelectedChefAvatar
		{
			get
			{
				return m_uSelectedChefAvatar;
			}
			set
			{
				SetProperty(ref m_uSelectedChefAvatar, value);
				RefreshSelectedChefData();
			}
		}

		public GameSession.SelectedChefData SelectedChefData
		{
			get
			{
				return m_SelectedChefData;
			}
			set
			{
				m_SelectedChefData = value;
				if (m_SelectedChefData == null)
				{
					return;
				}
				if (SelectedChefAvatar == 127)
				{
					AvatarDirectoryData avatarDirectoryData = GameUtils.GetAvatarDirectoryData();
					for (int i = 0; i < avatarDirectoryData.Avatars.Length; i++)
					{
						if (avatarDirectoryData.Avatars[i] == m_SelectedChefData.Character)
						{
							m_uSelectedChefAvatar = (uint)i;
							break;
						}
					}
				}
				if (m_Colour != 7)
				{
					return;
				}
				AvatarDirectoryData avatarDirectoryData2 = GameUtils.GetAvatarDirectoryData();
				for (uint num = 0u; num < avatarDirectoryData2.Colours.Length; num++)
				{
					if (avatarDirectoryData2.Colours[num] == m_SelectedChefData.Colour)
					{
						m_Colour = num;
						break;
					}
				}
			}
		}

		public bool IsLocal
		{
			get
			{
				return m_bIsLocal;
			}
			set
			{
				m_bIsLocal = value;
			}
		}

		public GamepadUser GamepadUser
		{
			get
			{
				return m_GamepadUser;
			}
			private set
			{
				SetProperty(ref m_GamepadUser, value);
			}
		}

		public PadSide PadSide
		{
			get
			{
				return m_padSide;
			}
			set
			{
				SetProperty(ref m_padSide, value);
			}
		}

		public SplitStatus Split
		{
			get
			{
				return m_splitStatus;
			}
			set
			{
				SetProperty(ref m_splitStatus, value);
			}
		}

		public PartyPersistance PartyPersist
		{
			get
			{
				return m_partyPersist;
			}
			set
			{
				SetProperty(ref m_partyPersist, value);
			}
		}

		public string DisplayName
		{
			get
			{
				return m_displayName;
			}
			set
			{
				SetProperty(ref m_displayName, value);
			}
		}

		public OnlineUserPlatformId PlatformID
		{
			get
			{
				return m_PlatformID;
			}
			set
			{
				SetProperty(ref m_PlatformID, value);
			}
		}

		public User(bool bLocal = false, MachineID machine = MachineID.Count, IOnlineMultiplayerSessionUserId sessionId = null, uint entityId = 0u, uint entity2Id = 0u, EngagementSlot engagement = EngagementSlot.Count, TeamID team = TeamID.None, uint colour = 7u, uint uSelectedChefAvatar = 127u, PadSide padSide = PadSide.Both, SplitStatus splitStatus = SplitStatus.Count, PartyPersistance partyPersist = PartyPersistance.NotSet, string displayName = "Unknown", OnlineUserPlatformId platformUserId = null, uint chefAvatarID = 127u)
		{
			m_bIsLocal = bLocal;
			Machine = machine;
			SessionId = sessionId;
			EntityID = entityId;
			Entity2ID = entity2Id;
			Engagement = engagement;
			Team = team;
			Colour = colour;
			SelectedChefAvatar = chefAvatarID;
			PadSide = padSide;
			m_splitStatus = splitStatus;
			m_partyPersist = partyPersist;
			m_displayName = displayName;
			m_PlatformID = platformUserId;
		}

		private void SetProperty<T>(ref T member, T value)
		{
			if ((member != null && value == null) || (member == null && value != null) || (member != null && value != null && !member.Equals(value)))
			{
				member = value;
				ChangedThisFrame = true;
			}
		}

		private void RefreshSelectedChefData()
		{
			if (SelectedChefAvatar != 127 && Colour != 7)
			{
				AvatarDirectoryData avatarDirectoryData = GameUtils.GetAvatarDirectoryData();
				if (avatarDirectoryData != null)
				{
					if (m_SelectedChefData == null)
					{
						m_SelectedChefData = new GameSession.SelectedChefData(avatarDirectoryData.Avatars[SelectedChefAvatar], avatarDirectoryData.Colours[Colour]);
						return;
					}
					m_SelectedChefData.Character = avatarDirectoryData.Avatars[SelectedChefAvatar];
					m_SelectedChefData.Colour = avatarDirectoryData.Colours[Colour];
				}
			}
			else
			{
				m_SelectedChefData = null;
			}
		}
	}
}
