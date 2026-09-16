using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class FlammableMessage : Serialisable
{
	public bool m_playerExtinguished;

	public bool m_onFire;

	public float m_fireStrength;

	public float m_fireStrengthVelocity;

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write(m_playerExtinguished);
		writer.Write(m_onFire);
		writer.Write(m_fireStrength);
		writer.Write(m_fireStrengthVelocity);
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_playerExtinguished = reader.ReadBit();
		m_onFire = reader.ReadBit();
		m_fireStrength = reader.ReadFloat32();
		m_fireStrengthVelocity = reader.ReadFloat32();
		return true;
	}
}
