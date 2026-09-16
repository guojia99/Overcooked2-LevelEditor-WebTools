using BitStream;
using Team17.Online.Multiplayer.Messaging;

namespace GameModes.Horde
{
	public struct HordeTargetMessage : Serialisable
	{
		public enum Kind
		{
			Invalid = 0,
			Health = 1,
			Count = 2
		}

		private static readonly int k_kindBitCount = GameUtils.GetRequiredBitCount(2);

		public Kind m_kind;

		public float m_health;

		public void Serialise(BitStreamWriter writer)
		{
			writer.Write((uint)m_kind, k_kindBitCount);
			Kind kind = m_kind;
			if (kind == Kind.Health)
			{
				writer.Write(m_health);
			}
		}

		public bool Deserialise(BitStreamReader reader)
		{
			bool result = true;
			m_kind = (Kind)reader.ReadUInt32(k_kindBitCount);
			Kind kind = m_kind;
			if (kind == Kind.Health)
			{
				m_health = reader.ReadFloat32();
			}
			return result;
		}

		public static void Health(ref HordeTargetMessage message, float health)
		{
			message.m_kind = Kind.Health;
			message.m_health = health;
		}
	}
}
