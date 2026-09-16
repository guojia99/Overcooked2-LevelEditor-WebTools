using BitStream;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

namespace GameModes.Horde
{
	public struct HordeFlowMessage : Serialisable
	{
		public enum Kind
		{
			Invalid = 0,
			BeginWave = 1,
			EndWave = 2,
			Spawn = 3,
			EntryAdded = 4,
			SuccessfulDelivery = 5,
			IncorrectDelivery = 6,
			ScoreOnly = 7,
			Count = 8
		}

		private const int k_indexMax = 32;

		private static readonly int k_indexBitCount = GameUtils.GetRequiredBitCount(32);

		private static readonly int k_waveIndexBitCount = GameUtils.GetRequiredBitCount(32);

		private static readonly int k_kindBitCount = GameUtils.GetRequiredBitCount(8);

		public Kind m_kind;

		public TeamScoreStats m_score;

		public int m_index;

		public GameObject m_enemy;

		public RecipeList.Entry m_entry;

		public void Serialise(BitStreamWriter writer)
		{
			writer.Write((uint)m_kind, k_kindBitCount);
			switch (m_kind)
			{
			case Kind.BeginWave:
			case Kind.EndWave:
				writer.Write((uint)m_index, k_waveIndexBitCount);
				m_score.Serialise(writer);
				break;
			case Kind.Spawn:
			{
				writer.Write((uint)m_index, k_indexBitCount);
				uint id = EntitySerialisationRegistry.GetId(m_enemy);
				writer.Write(id, 10);
				break;
			}
			case Kind.EntryAdded:
				writer.Write((uint)m_index, k_indexBitCount);
				m_entry.Serialise(writer);
				break;
			case Kind.SuccessfulDelivery:
			case Kind.IncorrectDelivery:
				writer.Write((uint)m_index, k_indexBitCount);
				m_score.Serialise(writer);
				break;
			case Kind.ScoreOnly:
				m_score.Serialise(writer);
				break;
			}
		}

		public bool Deserialise(BitStreamReader reader)
		{
			bool flag = true;
			m_kind = (Kind)reader.ReadUInt32(k_kindBitCount);
			flag &= m_kind != Kind.Invalid;
			switch (m_kind)
			{
			case Kind.BeginWave:
			case Kind.EndWave:
				m_index = (int)reader.ReadUInt32(k_waveIndexBitCount);
				flag &= m_index != -1;
				flag &= m_score.Deserialise(reader);
				break;
			case Kind.Spawn:
				m_index = (int)reader.ReadUInt32(k_indexBitCount);
				flag &= m_index != -1;
				flag &= NetworkUtils.DeserialiseGameObject(out m_enemy, reader);
				break;
			case Kind.EntryAdded:
				m_index = (int)reader.ReadUInt32(k_indexBitCount);
				flag &= m_index != -1;
				if (m_entry == null)
				{
					m_entry = new RecipeList.Entry();
				}
				flag &= m_entry.Deserialise(reader);
				break;
			case Kind.SuccessfulDelivery:
			case Kind.IncorrectDelivery:
				m_index = (int)reader.ReadUInt32(k_indexBitCount);
				flag &= m_index != -1;
				flag &= m_score.Deserialise(reader);
				break;
			case Kind.ScoreOnly:
				flag &= m_score.Deserialise(reader);
				break;
			}
			return flag;
		}

		public static void BeginWave(ref HordeFlowMessage message, int waveIndex, TeamScoreStats score)
		{
			ScoreOnly(ref message, score);
			message.m_kind = Kind.BeginWave;
			message.m_index = waveIndex;
		}

		public static void EndWave(ref HordeFlowMessage message, int waveIndex, TeamScoreStats score)
		{
			ScoreOnly(ref message, score);
			message.m_kind = Kind.EndWave;
			message.m_index = waveIndex;
		}

		public static void Spawn(ref HordeFlowMessage message, int index, GameObject enemy)
		{
			message.m_kind = Kind.Spawn;
			message.m_index = index;
			message.m_enemy = enemy;
		}

		public static void EntryAdded(ref HordeFlowMessage message, int index, RecipeList.Entry entry)
		{
			message.m_kind = Kind.EntryAdded;
			message.m_index = index;
			message.m_entry = entry;
		}

		public static void SuccessfulDelivery(ref HordeFlowMessage message, int index, TeamScoreStats score)
		{
			ScoreOnly(ref message, score);
			message.m_kind = Kind.SuccessfulDelivery;
			message.m_index = index;
		}

		public static void IncorrectDelivery(ref HordeFlowMessage message, int index, TeamScoreStats score)
		{
			ScoreOnly(ref message, score);
			message.m_kind = Kind.IncorrectDelivery;
			message.m_index = index;
		}

		public static void ScoreOnly(ref HordeFlowMessage message, TeamScoreStats score)
		{
			message.m_kind = Kind.ScoreOnly;
			message.m_score.Copy(score);
		}
	}
}
