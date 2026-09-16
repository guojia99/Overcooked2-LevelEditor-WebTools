using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class WorkableMessage : Serialisable
{
	public bool m_onWorkstation;

	public int m_progress;

	public int m_subProgress;

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write(m_onWorkstation);
		writer.Write((uint)m_progress, 4);
		writer.Write((uint)m_subProgress, 4);
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_onWorkstation = reader.ReadBit();
		m_progress = (int)reader.ReadUInt32(4);
		m_subProgress = (int)reader.ReadUInt32(4);
		return true;
	}
}
