using System.Text;
using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class LevelLoadByNameMessage : Serialisable
{
	public GameState m_StartLoadGameState;

	public GameState m_HideLoadingScreenGameState;

	public string m_Scene;

	public bool UseLoadingScreen = true;

	public void Initialise(GameState start, GameState stop, string _sceneName, bool bUseLoadingScreen)
	{
		m_StartLoadGameState = start;
		m_HideLoadingScreenGameState = stop;
		m_Scene = _sceneName;
		UseLoadingScreen = bUseLoadingScreen;
	}

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write((uint)m_StartLoadGameState, 6);
		writer.Write((uint)m_HideLoadingScreenGameState, 6);
		writer.Write(UseLoadingScreen);
		writer.Write(m_Scene, Encoding.ASCII);
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_StartLoadGameState = (GameState)reader.ReadUInt32(6);
		m_HideLoadingScreenGameState = (GameState)reader.ReadUInt32(6);
		UseLoadingScreen = reader.ReadBit();
		m_Scene = reader.ReadString(Encoding.ASCII);
		return true;
	}
}
