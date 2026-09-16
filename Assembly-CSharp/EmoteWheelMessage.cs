using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class EmoteWheelMessage : Serialisable
{
	public int m_emoteIdx;

	private const int kBitsPerEmoteIdx = 3;

	public PlayerInputLookup.Player m_player;

	private const int kBitsPerPlayerId = 5;

	public bool m_forUI;

	public void InitialiseStartEmote(int _emoteIdx, PlayerInputLookup.Player _player, bool _forUI)
	{
		m_emoteIdx = _emoteIdx;
		m_player = _player;
		m_forUI = _forUI;
	}

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write((uint)m_emoteIdx, 3);
		writer.Write((uint)m_player, 5);
		writer.Write(m_forUI);
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_emoteIdx = (int)reader.ReadUInt32(3);
		m_player = (PlayerInputLookup.Player)reader.ReadUInt32(5);
		m_forUI = reader.ReadBit();
		return true;
	}
}
