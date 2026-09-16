using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using BitStream;
using Team17.Online;

public static class JoinDataProvider
{
	public class GameUserData
	{
		[DefaultValue(null)]
		public string DisplayName { get; set; }

		[DefaultValue(EngagementSlot.Count)]
		public EngagementSlot Slot { get; set; }

		[DefaultValue(NetConnectionState.COUNT)]
		public NetConnectionState JoinMethod { get; set; }

		[DefaultValue(null)]
		public OnlineUserPlatformId PlatformId { get; set; }

		[DefaultValue(127L)]
		public uint AvatarId { get; set; }

		[DefaultValue(PadSide.Both)]
		public PadSide ControllerSide { get; set; }

		[DefaultValue(User.SplitStatus.Count)]
		public User.SplitStatus ControllerSplitStatus { get; set; }

		public void Clear()
		{
			Slot = EngagementSlot.Count;
			JoinMethod = NetConnectionState.COUNT;
			PlatformId = null;
			AvatarId = 127u;
			DisplayName = null;
			ControllerSide = PadSide.Both;
			ControllerSplitStatus = User.SplitStatus.Count;
		}

		public bool Serialize(BitStreamWriter writer)
		{
			try
			{
				writer.Write(DisplayName, Encoding.Unicode);
				writer.Write((uint)Slot, 3);
				writer.Write((uint)JoinMethod, 4);
				PlatformId.Serialise(writer);
				writer.Write(AvatarId, 8);
				writer.Write((uint)ControllerSide, 3);
				writer.Write((uint)ControllerSplitStatus, 3);
				return true;
			}
			catch (Exception)
			{
			}
			return false;
		}

		public bool Deserialize(BitStreamReader reader)
		{
			try
			{
				PlatformId = new OnlineUserPlatformId();
				DisplayName = reader.ReadString(Encoding.Unicode);
				Slot = (EngagementSlot)reader.ReadUInt32(3);
				JoinMethod = (NetConnectionState)reader.ReadUInt32(4);
				PlatformId.Deserialise(reader);
				AvatarId = reader.ReadUInt32(8);
				ControllerSide = (PadSide)reader.ReadUInt32(3);
				ControllerSplitStatus = (User.SplitStatus)reader.ReadUInt32(3);
				return true;
			}
			catch (Exception)
			{
			}
			Clear();
			return false;
		}
	}

	private static FastList<byte> m_buffer = new FastList<byte>(256);

	private static BitStreamWriter m_streamWriter = new BitStreamWriter(m_buffer);

	private static GameUserData m_gameUserData = new GameUserData();

	public static OnlineMultiplayerSessionJoinLocalUserData BuildJoinRequestData(EngagementSlot slot, NetConnectionState requestingJoinState, OnlineMultiplayerLocalUserId requestingLocalUser)
	{
		try
		{
			m_gameUserData.Clear();
			m_buffer.Clear();
			m_streamWriter.Reset(m_buffer);
			for (int i = 0; i < ClientUserSystem.m_Users.Count; i++)
			{
				User user = ClientUserSystem.m_Users._items[i];
				if (user.IsLocal && slot == user.Engagement)
				{
					m_gameUserData.DisplayName = requestingLocalUser.m_userName;
					m_gameUserData.Slot = slot;
					m_gameUserData.JoinMethod = requestingJoinState;
					m_gameUserData.PlatformId = requestingLocalUser.m_platformId;
					m_gameUserData.AvatarId = user.SelectedChefAvatar;
					m_gameUserData.ControllerSide = user.PadSide;
					m_gameUserData.ControllerSplitStatus = user.Split;
					if (m_gameUserData.Serialize(m_streamWriter))
					{
						m_gameUserData.Clear();
						byte[] array = new byte[m_buffer.Count];
						Buffer.BlockCopy(m_buffer._items, 0, array, 0, m_buffer.Count);
						OnlineMultiplayerSessionJoinLocalUserData onlineMultiplayerSessionJoinLocalUserData = new OnlineMultiplayerSessionJoinLocalUserData();
						onlineMultiplayerSessionJoinLocalUserData.Id = requestingLocalUser;
						onlineMultiplayerSessionJoinLocalUserData.GameData = array;
						onlineMultiplayerSessionJoinLocalUserData.GameDataSize = (uint)array.Length;
						return onlineMultiplayerSessionJoinLocalUserData;
					}
					throw new Exception("GameUserData.Serialize() : Failed!");
				}
			}
			throw new Exception("JoinDataProvider.BuildJoinRequestData() : Failed to find local user!");
		}
		catch (Exception)
		{
		}
		m_gameUserData.Clear();
		return null;
	}
}
