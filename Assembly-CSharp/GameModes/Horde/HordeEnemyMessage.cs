using BitStream;
using Team17.Online.Multiplayer.Messaging;

namespace GameModes.Horde
{
	public struct HordeEnemyMessage : Serialisable
	{
		public enum Kind
		{
			Invalid = 0,
			Transition = 1,
			Count = 2
		}

		public static readonly int k_kindBitCount = GameUtils.GetRequiredBitCount(2);

		public static readonly int k_stateBitCount = GameUtils.GetRequiredBitCount(8);

		public Kind m_kind;

		public HordeEnemyBehaviorState m_toState;

		public void Serialise(BitStreamWriter writer)
		{
			writer.Write((uint)m_kind, k_kindBitCount);
			writer.Write((uint)m_toState, k_stateBitCount);
		}

		public bool Deserialise(BitStreamReader reader)
		{
			bool flag = true;
			m_kind = (Kind)reader.ReadUInt32(k_kindBitCount);
			m_toState = (HordeEnemyBehaviorState)reader.ReadUInt32(k_stateBitCount);
			return flag & (m_toState != HordeEnemyBehaviorState.Start);
		}

		public static void Transition(ref HordeEnemyMessage message, HordeEnemyBehaviorState state)
		{
			message.m_kind = Kind.Transition;
			message.m_toState = state;
		}
	}
}
