using System;
using BitStream;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

[Serializable]
public class TeamMonitor
{
	public class TeamScoreStats : Serialisable
	{
		private const int kBitsPerScore = 16;

		private const int kBitsPerMultiplier = 3;

		private const int kBitsPerCombo = 8;

		private const int kBitsPerDelivery = 8;

		public int TotalBaseScore;

		public int TotalTipsScore;

		public int TotalMultiplier;

		public int TotalCombo;

		public int TotalTimeExpireDeductions;

		public bool ComboMaintained = true;

		public int TotalSuccessfulDeliveries;

		public int GetTotalScore()
		{
			return TotalBaseScore + TotalTipsScore - TotalTimeExpireDeductions;
		}

		public bool Copy(TeamScoreStats _other)
		{
			TotalBaseScore = _other.TotalBaseScore;
			TotalTipsScore = _other.TotalTipsScore;
			TotalMultiplier = _other.TotalMultiplier;
			TotalCombo = _other.TotalCombo;
			TotalTimeExpireDeductions = _other.TotalTimeExpireDeductions;
			ComboMaintained = _other.ComboMaintained;
			TotalSuccessfulDeliveries = _other.TotalSuccessfulDeliveries;
			return true;
		}

		public void Serialise(BitStreamWriter writer)
		{
			writer.Write((uint)TotalBaseScore, 16);
			writer.Write((uint)TotalTipsScore, 16);
			writer.Write((uint)TotalMultiplier, 3);
			writer.Write((uint)TotalCombo, 8);
			writer.Write((uint)TotalTimeExpireDeductions, 16);
			writer.Write(ComboMaintained);
			writer.Write((uint)TotalSuccessfulDeliveries, 8);
		}

		public bool Deserialise(BitStreamReader reader)
		{
			TotalBaseScore = (int)reader.ReadUInt32(16);
			TotalTipsScore = (int)reader.ReadUInt32(16);
			TotalMultiplier = (int)reader.ReadUInt32(3);
			TotalCombo = (int)reader.ReadUInt32(8);
			TotalTimeExpireDeductions = (int)reader.ReadUInt32(16);
			ComboMaintained = reader.ReadBit();
			TotalSuccessfulDeliveries = (int)reader.ReadUInt32(8);
			return true;
		}
	}

	[SerializeField]
	public RecipeFlowGUI m_recipeBarUIController;
}
