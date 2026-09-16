using BitStream;
using Team17.Online.Multiplayer.Messaging;

internal class ExampleNetworkMessage : Serialisable
{
	public float m_ExampleFloat;

	public bool m_ExampleBool;

	public void Initialise(float fFloat, bool bBool)
	{
		m_ExampleFloat = fFloat;
		m_ExampleBool = bBool;
	}

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write(m_ExampleFloat);
		writer.Write(m_ExampleBool);
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_ExampleFloat = reader.ReadFloat32();
		m_ExampleBool = reader.ReadBit();
		return true;
	}
}
