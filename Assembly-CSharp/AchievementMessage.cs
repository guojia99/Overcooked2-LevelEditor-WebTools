using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class AchievementMessage : Serialisable
{
	public EntityMessageHeader m_Header = new EntityMessageHeader();

	public int m_statId = -1;

	public int m_increment = 1;

	private const int kBitsPerAchievementType = 10;

	private const int kBitsPerAchievementIncrement = 3;

	public void Initialise(EntityMessageHeader header, int statId, int increment = 1)
	{
		m_Header = header;
		m_statId = statId;
		m_increment = increment;
	}

	public void Serialise(BitStreamWriter writer)
	{
		m_Header.Serialise(writer);
		writer.Write((uint)m_statId, 10);
		writer.Write((uint)m_increment, 3);
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_Header.Deserialise(reader);
		m_statId = (int)reader.ReadUInt32(10);
		m_increment = (int)reader.ReadUInt32(3);
		return true;
	}
}
