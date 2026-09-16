using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class DynamicLevelMessage : Serialisable
{
	public const int kBitsPerPhaseNumber = 8;

	public int m_phase;

	public void Initialise(int _phase)
	{
		m_phase = _phase;
	}

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write((uint)m_phase, 8);
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_phase = (int)reader.ReadUInt32(8);
		return true;
	}
}
