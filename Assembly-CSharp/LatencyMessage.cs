using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class LatencyMessage : Serialisable
{
	public enum Stage
	{
		Ping = 0,
		Pong = 1
	}

	public Stage m_Stage;

	public bool m_bReliable;

	public float m_fTime;

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write((uint)m_Stage, 1);
		writer.Write(m_bReliable);
		writer.Write(m_fTime);
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_Stage = (Stage)reader.ReadUInt32(1);
		m_bReliable = reader.ReadBit();
		m_fTime = reader.ReadFloat32();
		return true;
	}
}
