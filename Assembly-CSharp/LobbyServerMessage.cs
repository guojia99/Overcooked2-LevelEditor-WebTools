using BitStream;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;

public class LobbyServerMessage : Serialisable
{
	public enum LobbyMessageType
	{
		StateChange = 0,
		TimerUpdate = 1,
		ResetTimer = 2,
		SelectionUpdate = 3,
		FinalSelection = 4,
		CreateGameSession = 5
	}

	public struct StateChange
	{
		public const int kBitsPerLobbyState = 3;

		public LobbyFlowController.LobbyState m_state;

		public bool m_bIsCoop;

		public const int kBitsPerVisiblity = 2;

		public OnlineMultiplayerSessionVisibility m_sessionVisibility;

		public const int kBitsPerConnectionMode = 2;

		public OnlineMultiplayerConnectionMode m_connectionMode;
	}

	public struct TimerInfo
	{
		public float m_timerVal;
	}

	public struct SelectionUpdate
	{
		public SceneDirectoryData.LevelTheme m_theme;

		public const int kBitsPerChefIndex = 2;

		public int m_chefIndex;
	}

	public const int kBitsPerLobbyMsgType = 3;

	public LobbyMessageType m_type;

	public StateChange m_stateChange;

	public TimerInfo m_timerInfo;

	public SelectionUpdate m_selectionUpdate;

	public int m_dlcID = -1;

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write((uint)m_type, 3);
		switch (m_type)
		{
		case LobbyMessageType.StateChange:
			writer.Write((uint)m_stateChange.m_state, 3);
			writer.Write(m_stateChange.m_bIsCoop);
			writer.Write((uint)m_stateChange.m_sessionVisibility, 2);
			writer.Write((uint)m_stateChange.m_connectionMode, 2);
			break;
		case LobbyMessageType.TimerUpdate:
			writer.Write(m_timerInfo.m_timerVal);
			break;
		case LobbyMessageType.ResetTimer:
			writer.Write(m_timerInfo.m_timerVal);
			break;
		case LobbyMessageType.SelectionUpdate:
			writer.Write((uint)m_selectionUpdate.m_theme, SceneDirectoryData.c_bitsPerTheme);
			writer.Write((uint)m_selectionUpdate.m_chefIndex, 2);
			break;
		case LobbyMessageType.FinalSelection:
			writer.Write((uint)m_selectionUpdate.m_theme, SceneDirectoryData.c_bitsPerTheme);
			writer.Write((uint)m_selectionUpdate.m_chefIndex, 2);
			break;
		case LobbyMessageType.CreateGameSession:
		{
			bool flag = m_dlcID != -1;
			writer.Write(flag);
			if (flag)
			{
				writer.Write((uint)m_dlcID, 4);
			}
			break;
		}
		}
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_type = (LobbyMessageType)reader.ReadUInt32(3);
		switch (m_type)
		{
		case LobbyMessageType.StateChange:
			m_stateChange = default(StateChange);
			m_stateChange.m_state = (LobbyFlowController.LobbyState)reader.ReadUInt32(3);
			m_stateChange.m_bIsCoop = reader.ReadBit();
			m_stateChange.m_sessionVisibility = (OnlineMultiplayerSessionVisibility)reader.ReadUInt32(2);
			m_stateChange.m_connectionMode = (OnlineMultiplayerConnectionMode)reader.ReadUInt32(2);
			break;
		case LobbyMessageType.TimerUpdate:
			m_timerInfo = default(TimerInfo);
			m_timerInfo.m_timerVal = reader.ReadFloat32();
			break;
		case LobbyMessageType.ResetTimer:
			m_timerInfo = default(TimerInfo);
			m_timerInfo.m_timerVal = reader.ReadFloat32();
			break;
		case LobbyMessageType.SelectionUpdate:
			m_selectionUpdate = default(SelectionUpdate);
			m_selectionUpdate.m_theme = (SceneDirectoryData.LevelTheme)reader.ReadUInt32(SceneDirectoryData.c_bitsPerTheme);
			m_selectionUpdate.m_chefIndex = (int)reader.ReadUInt32(2);
			break;
		case LobbyMessageType.FinalSelection:
			m_selectionUpdate = default(SelectionUpdate);
			m_selectionUpdate.m_theme = (SceneDirectoryData.LevelTheme)reader.ReadUInt32(SceneDirectoryData.c_bitsPerTheme);
			m_selectionUpdate.m_chefIndex = (int)reader.ReadUInt32(2);
			break;
		case LobbyMessageType.CreateGameSession:
			if (reader.ReadBit())
			{
				m_dlcID = (int)reader.ReadUInt32(4);
			}
			else
			{
				m_dlcID = -1;
			}
			break;
		default:
			return false;
		}
		return true;
	}

	public bool ToSendReliable()
	{
		return m_type != LobbyMessageType.TimerUpdate;
	}

	public override string ToString()
	{
		string text = string.Concat(GetType(), "(", m_type);
		switch (m_type)
		{
		case LobbyMessageType.ResetTimer:
			text = text + ", " + m_timerInfo;
			break;
		case LobbyMessageType.TimerUpdate:
			text = text + ", " + m_timerInfo;
			break;
		case LobbyMessageType.StateChange:
		{
			string text2 = text;
			text = string.Concat(text2, ", ", m_stateChange.m_state, ", ", m_stateChange.m_bIsCoop, ", ", m_stateChange.m_sessionVisibility, ", ", m_stateChange.m_connectionMode);
			break;
		}
		case LobbyMessageType.SelectionUpdate:
		{
			string text2 = text;
			text = string.Concat(text2, ", SelectionUpdate(", m_selectionUpdate.m_theme, ", ", m_selectionUpdate.m_chefIndex, ")");
			break;
		}
		}
		return text + ")";
	}
}
