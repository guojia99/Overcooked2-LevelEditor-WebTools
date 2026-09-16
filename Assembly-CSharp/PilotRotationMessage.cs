using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class PilotRotationMessage : Serialisable
{
	public float m_angle;

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write(m_angle);
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_angle = reader.ReadFloat32();
		return true;
	}
}
