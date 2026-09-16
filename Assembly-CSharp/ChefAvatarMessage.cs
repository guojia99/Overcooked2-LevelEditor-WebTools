using BitStream;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;

public class ChefAvatarMessage : Serialisable
{
	public uint m_ChefAvatar;

	public User.MachineID m_Machine = User.MachineID.Count;

	public EngagementSlot m_EngagementSlot = EngagementSlot.Count;

	public User.SplitStatus m_Split = User.SplitStatus.Count;

	public void Initialise(uint chefAvatar, User.MachineID machine, EngagementSlot engagement, User.SplitStatus split)
	{
		m_ChefAvatar = chefAvatar;
		m_Machine = machine;
		m_EngagementSlot = engagement;
		m_Split = split;
	}

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write(m_ChefAvatar, 7);
		writer.Write((uint)m_Machine, 3);
		writer.Write((uint)m_EngagementSlot, 3);
		writer.Write((uint)m_Split, 3);
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_ChefAvatar = reader.ReadUInt32(7);
		m_Machine = (User.MachineID)reader.ReadUInt32(3);
		m_EngagementSlot = (EngagementSlot)reader.ReadUInt32(3);
		m_Split = (User.SplitStatus)reader.ReadUInt32(3);
		return true;
	}
}
