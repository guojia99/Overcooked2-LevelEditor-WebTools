using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class WashableMessage : Serialisable
{
	public enum MsgType
	{
		Progress = 0,
		Rate = 1
	}

	private const int c_msgTypeBits = 1;

	public MsgType m_msgType;

	public float m_progress;

	public float m_target;

	public float m_rate;

	public void Initialise_Progress(float _progress, float _targetProgress)
	{
		m_msgType = MsgType.Progress;
		m_progress = _progress;
		m_target = _targetProgress;
	}

	public void Initialise_Rate(float _rate, float _currentProgress)
	{
		m_msgType = MsgType.Rate;
		m_rate = _rate;
		m_progress = _currentProgress;
	}

	public void Serialise(BitStreamWriter _writer)
	{
		_writer.Write((uint)m_msgType, 1);
		switch (m_msgType)
		{
		case MsgType.Progress:
			_writer.Write(m_progress);
			_writer.Write(m_target);
			break;
		case MsgType.Rate:
			_writer.Write(m_rate);
			_writer.Write(m_progress);
			break;
		}
	}

	public bool Deserialise(BitStreamReader _reader)
	{
		m_msgType = (MsgType)_reader.ReadUInt32(1);
		switch (m_msgType)
		{
		case MsgType.Progress:
			m_progress = _reader.ReadFloat32();
			m_target = _reader.ReadFloat32();
			break;
		case MsgType.Rate:
			m_rate = _reader.ReadFloat32();
			m_progress = _reader.ReadFloat32();
			break;
		}
		return true;
	}
}
