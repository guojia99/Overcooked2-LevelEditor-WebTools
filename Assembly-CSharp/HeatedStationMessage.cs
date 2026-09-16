using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class HeatedStationMessage : Serialisable
{
	public enum MsgType
	{
		Heat = 0,
		ItemAdded = 1
	}

	public MsgType m_msgType;

	public float m_heat;

	public void Serialise(BitStreamWriter _writer)
	{
		_writer.Write((uint)m_msgType, 2);
		if (m_msgType == MsgType.Heat)
		{
			_writer.Write(m_heat);
		}
	}

	public bool Deserialise(BitStreamReader _reader)
	{
		m_msgType = (MsgType)_reader.ReadUInt32(2);
		if (m_msgType == MsgType.Heat)
		{
			m_heat = _reader.ReadFloat32();
		}
		return true;
	}
}
