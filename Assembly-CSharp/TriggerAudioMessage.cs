using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class TriggerAudioMessage : Serialisable
{
	private int m_kBitsPerTag = GameUtils.GetRequiredBitCount(319);

	private GameOneShotAudioTag m_audioTag;

	private int m_kBitsPerLayer = 5;

	private int m_layer;

	public GameOneShotAudioTag AudioTag
	{
		get
		{
			return m_audioTag;
		}
	}

	public int Layer
	{
		get
		{
			return m_layer;
		}
	}

	public void Initialise(GameOneShotAudioTag _audioTag, int _layer)
	{
		m_audioTag = _audioTag;
		m_layer = _layer;
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_audioTag = (GameOneShotAudioTag)reader.ReadUInt32(m_kBitsPerTag);
		m_layer = (int)reader.ReadUInt32(m_kBitsPerLayer);
		return true;
	}

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write((uint)m_audioTag, m_kBitsPerTag);
		writer.Write((uint)m_layer, m_kBitsPerLayer);
	}
}
