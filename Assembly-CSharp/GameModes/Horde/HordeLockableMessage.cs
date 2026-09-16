using BitStream;
using Team17.Online.Multiplayer.Messaging;

namespace GameModes.Horde
{
	public class HordeLockableMessage : Serialisable
	{
		public bool m_locked = true;

		public void Serialise(BitStreamWriter writer)
		{
			writer.Write(m_locked);
		}

		public bool Deserialise(BitStreamReader reader)
		{
			m_locked = reader.ReadBit();
			return true;
		}

		public static void Lock(ref HordeLockableMessage message)
		{
			message.m_locked = true;
		}

		public static void Unlock(ref HordeLockableMessage message)
		{
			message.m_locked = false;
		}
	}
}
