using BitStream;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;

internal class ControllerSettingsMessage : Serialisable
{
	public PadSide m_side = PadSide.Both;

	public User.MachineID m_Machine = User.MachineID.Count;

	public EngagementSlot m_EngagementSlot = EngagementSlot.Count;

	public User.SplitStatus m_Split = User.SplitStatus.Count;

	public void Initialise(PadSide side, User.MachineID machine, EngagementSlot engagement, User.SplitStatus split)
	{
		m_side = side;
		m_Machine = machine;
		m_EngagementSlot = engagement;
		m_Split = split;
	}

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write((uint)m_side, 2);
		writer.Write((uint)m_Machine, 3);
		writer.Write((uint)m_EngagementSlot, 3);
		writer.Write((uint)m_Split, 2);
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_side = (PadSide)reader.ReadUInt32(2);
		m_Machine = (User.MachineID)reader.ReadUInt32(3);
		m_EngagementSlot = (EngagementSlot)reader.ReadUInt32(3);
		m_Split = (User.SplitStatus)reader.ReadUInt32(2);
		return true;
	}
}
