using BitStream;

namespace Team17.Online.Multiplayer.Messaging
{
	public class MapAvatarHornMessage : Serialisable
	{
		public int m_playerIdx;

		public void Serialise(BitStreamWriter writer)
		{
			writer.Write((uint)m_playerIdx, 3);
		}

		public bool Deserialise(BitStreamReader reader)
		{
			m_playerIdx = (int)reader.ReadUInt32(3);
			return true;
		}
	}
}
