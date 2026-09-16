using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class PlayerSlipMessage : Serialisable
{
	public enum MsgType
	{
		Slip = 0,
		Stand = 1,
		Finished = 2
	}

	private const int kBitsPerMsgType = 2;

	public MsgType m_msgType;

	public void Initialise(MsgType _msgType)
	{
		m_msgType = _msgType;
	}

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write((uint)m_msgType, 2);
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_msgType = (MsgType)reader.ReadUInt32(2);
		return true;
	}
}
