using System.Text;
using BitStream;

namespace Team17.Online.Multiplayer.Messaging
{
	public class TriggerEventMessage : Serialisable
	{
		public EntityMessageHeader m_SourceHeader = new EntityMessageHeader();

		public EntityMessageHeader m_TargetHeader = new EntityMessageHeader();

		public string m_Event;

		public float m_Time;

		public void Initialise(EntityMessageHeader _source, EntityMessageHeader _target, string _event, float _time)
		{
			m_SourceHeader = _source;
			m_TargetHeader = _target;
			m_Event = _event;
			m_Time = _time;
		}

		public void Serialise(BitStreamWriter writer)
		{
			m_SourceHeader.Serialise(writer);
			m_TargetHeader.Serialise(writer);
			writer.Write(m_Time);
			writer.Write(m_Event, Encoding.ASCII);
		}

		public bool Deserialise(BitStreamReader reader)
		{
			if (m_SourceHeader.Deserialise(reader) && m_TargetHeader.Deserialise(reader))
			{
				m_Time = reader.ReadFloat32();
				m_Event = reader.ReadString(Encoding.ASCII);
				return true;
			}
			return false;
		}
	}
}
