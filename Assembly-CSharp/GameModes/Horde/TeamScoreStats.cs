using BitStream;
using Team17.Online.Multiplayer.Messaging;

namespace GameModes.Horde
{
	public struct TeamScoreStats : Serialisable
	{
		public const int k_healthMax = 255;

		public const int k_moneyMax = 9999;

		public const int k_defeatedEnemiesMax = 255;

		private static readonly int k_healthBitCount = GameUtils.GetRequiredBitCount(255);

		private static readonly int k_moneyBitCount = GameUtils.GetRequiredBitCount(9999);

		private static readonly int k_defeatedEnemiesBitCount = GameUtils.GetRequiredBitCount(255);

		public int TotalHealth;

		public int TotalMoneyEarned;

		public int TotalMoneySpent;

		public int TotalEnemiesDefeated;

		public int GetTotalMoney()
		{
			return TotalMoneyEarned - TotalMoneySpent;
		}

		public bool Copy(TeamScoreStats _other)
		{
			TotalHealth = _other.TotalHealth;
			TotalMoneyEarned = _other.TotalMoneyEarned;
			TotalMoneySpent = _other.TotalMoneySpent;
			TotalEnemiesDefeated = _other.TotalEnemiesDefeated;
			return true;
		}

		public void Serialise(BitStreamWriter writer)
		{
			writer.Write((uint)TotalHealth, k_healthBitCount);
			writer.Write((uint)TotalMoneyEarned, k_moneyBitCount);
			writer.Write((uint)TotalMoneySpent, k_moneyBitCount);
			writer.Write((uint)TotalEnemiesDefeated, k_defeatedEnemiesBitCount);
		}

		public bool Deserialise(BitStreamReader reader)
		{
			TotalHealth = (int)reader.ReadUInt32(k_healthBitCount);
			TotalMoneyEarned = (int)reader.ReadUInt32(k_moneyBitCount);
			TotalMoneySpent = (int)reader.ReadUInt32(k_moneyBitCount);
			TotalEnemiesDefeated = (int)reader.ReadUInt32(k_defeatedEnemiesBitCount);
			return true;
		}
	}
}
