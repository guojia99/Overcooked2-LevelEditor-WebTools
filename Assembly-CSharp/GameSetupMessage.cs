using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class GameSetupMessage : Serialisable
{
	public GameMode m_Mode = GameMode.COUNT;

	public void Initialise(GameMode mode)
	{
		m_Mode = mode;
	}

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write((byte)m_Mode, 3);
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_Mode = (GameMode)reader.ReadByte(3);
		return true;
	}
}
