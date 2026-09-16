using BitStream;
using Team17.Online.Multiplayer.Messaging;

namespace GameModes
{
	public class SessionConfigSyncMessage : Serialisable
	{
		public int k_gameModeKindBits = 3;

		public SessionConfig m_config = new SessionConfig();

		public void Initialise(SessionConfig config)
		{
			m_config = config;
		}

		public void Serialise(BitStreamWriter writer)
		{
			writer.Write((uint)m_config.m_kind, k_gameModeKindBits);
			for (int i = 0; i < 3; i++)
			{
				writer.Write(m_config.m_settings[i]);
			}
		}

		public bool Deserialise(BitStreamReader reader)
		{
			m_config.m_kind = (Kind)reader.ReadUInt32(k_gameModeKindBits);
			for (int i = 0; i < 3; i++)
			{
				m_config.m_settings[i] = reader.ReadBit();
			}
			return true;
		}
	}
}
