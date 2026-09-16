using System.Text;
using BitStream;

namespace Team17.Online.Multiplayer.Messaging
{
	public class UserData : Serialisable
	{
		public bool online;

		public byte sessionUserUniqueId;

		public User.MachineID machine;

		public uint entity;

		public uint entity2;

		public EngagementSlot slot;

		public TeamID team;

		public uint colour;

		public uint selectedChefAvatar;

		public GameState gameState;

		public PadSide padSide;

		public User.SplitStatus splitStatus;

		public User.PartyPersistance partyPersist;

		public string displayName;

		public OnlineUserPlatformId platformId;

		public void Initialise(User user)
		{
			if (user.SessionId != null)
			{
				online = true;
				sessionUserUniqueId = user.SessionId.UniqueId;
			}
			else
			{
				online = false;
				sessionUserUniqueId = 0;
			}
			machine = user.Machine;
			entity = user.EntityID;
			entity2 = user.Entity2ID;
			slot = user.Engagement;
			team = user.Team;
			colour = user.Colour;
			selectedChefAvatar = user.SelectedChefAvatar;
			gameState = user.GameState;
			padSide = user.PadSide;
			splitStatus = user.Split;
			partyPersist = user.PartyPersist;
			displayName = user.DisplayName;
			platformId = user.PlatformID;
		}

		public void Serialise(BitStreamWriter writer)
		{
			writer.Write(online);
			if (online)
			{
				writer.Write(sessionUserUniqueId, 8);
			}
			writer.Write((uint)machine, 2);
			writer.Write(entity, 10);
			writer.Write(entity2, 10);
			writer.Write((uint)slot, 3);
			writer.Write(selectedChefAvatar, 7);
			writer.Write((uint)gameState, 5);
			writer.Write((uint)team, 2);
			writer.Write(colour, 3);
			writer.Write((uint)padSide, 2);
			writer.Write((uint)splitStatus, 2);
			writer.Write((uint)partyPersist, 2);
			bool flag = null != platformId;
			writer.Write(flag);
			if (flag)
			{
				platformId.Serialise(writer);
			}
			writer.Write(displayName, Encoding.Unicode);
		}

		public bool Deserialise(BitStreamReader reader)
		{
			online = reader.ReadBit();
			if (online)
			{
				sessionUserUniqueId = reader.ReadByte(8);
			}
			machine = (User.MachineID)reader.ReadUInt32(2);
			entity = reader.ReadUInt32(10);
			entity2 = reader.ReadUInt32(10);
			slot = (EngagementSlot)reader.ReadUInt32(3);
			selectedChefAvatar = reader.ReadUInt32(7);
			gameState = (GameState)reader.ReadUInt32(5);
			team = (TeamID)reader.ReadUInt32(2);
			colour = reader.ReadUInt32(3);
			padSide = (PadSide)reader.ReadUInt32(2);
			splitStatus = (User.SplitStatus)reader.ReadUInt32(2);
			partyPersist = (User.PartyPersistance)reader.ReadUInt32(2);
			if (reader.ReadBit())
			{
				if (platformId == null)
				{
					platformId = new OnlineUserPlatformId();
				}
				platformId.Deserialise(reader);
			}
			displayName = reader.ReadString(Encoding.Unicode);
			return true;
		}
	}
}
