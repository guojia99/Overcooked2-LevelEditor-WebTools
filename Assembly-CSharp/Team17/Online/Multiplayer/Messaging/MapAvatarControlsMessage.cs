using BitStream;

namespace Team17.Online.Multiplayer.Messaging
{
	public class MapAvatarControlsMessage : Serialisable
	{
		public bool[] m_bHorns = new bool[4];

		public bool m_bDash;

		public uint CurrentSelectableEntityId;

		public void Serialise(BitStreamWriter writer)
		{
			for (int i = 0; i < 4; i++)
			{
				writer.Write(m_bHorns[i]);
			}
			writer.Write(m_bDash);
			writer.Write(CurrentSelectableEntityId, 10);
		}

		public bool Deserialise(BitStreamReader reader)
		{
			for (int i = 0; i < 4; i++)
			{
				m_bHorns[i] = reader.ReadBit();
			}
			m_bDash = reader.ReadBit();
			CurrentSelectableEntityId = reader.ReadUInt32(10);
			return true;
		}
	}
}
