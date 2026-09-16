using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class LevelLoadByIndexMessage : Serialisable
{
	public const uint kInvalidLevel = 255u;

	public GameState m_StartLoadGameState;

	public GameState m_HideLoadingScreenGameState;

	public uint LevelIndex = 255u;

	public uint Players = 255u;

	public bool UseLoadingScreen = true;

	public void Initialise(GameState start, GameState stop, uint uLevelIndex, uint uPlayers, bool bUseLoadingScreen)
	{
		m_StartLoadGameState = start;
		m_HideLoadingScreenGameState = stop;
		LevelIndex = uLevelIndex;
		Players = uPlayers;
		UseLoadingScreen = bUseLoadingScreen;
	}

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write((uint)m_StartLoadGameState, 6);
		writer.Write((uint)m_HideLoadingScreenGameState, 6);
		writer.Write(LevelIndex, 8);
		writer.Write(Players, 8);
		writer.Write(UseLoadingScreen);
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_StartLoadGameState = (GameState)reader.ReadUInt32(6);
		m_HideLoadingScreenGameState = (GameState)reader.ReadUInt32(6);
		LevelIndex = reader.ReadUInt32(8);
		Players = reader.ReadUInt32(8);
		UseLoadingScreen = reader.ReadBit();
		return true;
	}
}
