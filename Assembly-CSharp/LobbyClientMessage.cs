using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class LobbyClientMessage : Serialisable
{
	public enum LobbyMessageType
	{
		ThemeSelected = 0,
		StateRequest = 1,
		TeamChangeRequest = 2
	}

	public const int kBitsPerLobbyMsgType = 2;

	public LobbyMessageType m_type;

	public SceneDirectoryData.LevelTheme m_theme;

	public const int kBitsPerChefIndex = 2;

	public int m_chefIndex;

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write((uint)m_type, 2);
		switch (m_type)
		{
		case LobbyMessageType.ThemeSelected:
			writer.Write((uint)m_theme, SceneDirectoryData.c_bitsPerTheme);
			writer.Write((uint)m_chefIndex, 2);
			break;
		case LobbyMessageType.StateRequest:
			break;
		case LobbyMessageType.TeamChangeRequest:
			writer.Write((uint)m_chefIndex, 2);
			break;
		}
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_type = (LobbyMessageType)reader.ReadUInt32(2);
		switch (m_type)
		{
		case LobbyMessageType.ThemeSelected:
			m_theme = (SceneDirectoryData.LevelTheme)reader.ReadUInt32(SceneDirectoryData.c_bitsPerTheme);
			m_chefIndex = (int)reader.ReadUInt32(2);
			break;
		case LobbyMessageType.TeamChangeRequest:
			m_chefIndex = (int)reader.ReadUInt32(2);
			break;
		}
		return true;
	}

	public override string ToString()
	{
		string text = string.Concat(GetType(), "(", m_type);
		switch (m_type)
		{
		case LobbyMessageType.ThemeSelected:
		{
			string text2 = text;
			text = string.Concat(text2, ", ThemeSelection(", m_theme, ", ", m_chefIndex, ")");
			break;
		}
		}
		return text + ")";
	}
}
