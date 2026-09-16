using BitStream;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;

public class HighScoresMessage : Serialisable
{
	public GameProgress.HighScores HighScores = new GameProgress.HighScores();

	public int DLC = -1;

	public User.MachineID m_Machine;

	private const int BitsRequiredForLevelCount = 8;

	private readonly int BitsRequiredForHighScore = GameUtils.GetRequiredBitCount(65535);

	private readonly int BitsRequiredForTime = GameUtils.GetRequiredBitCount(5999);

	private readonly int BitsRequiredForMachineID = GameUtils.GetRequiredBitCount(4);

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write((uint)m_Machine, BitsRequiredForMachineID);
		bool flag = DLC != -1;
		writer.Write(flag);
		if (flag)
		{
			writer.Write((uint)DLC, 4);
		}
		uint count = (uint)HighScores.Scores.Count;
		writer.Write(count, 8);
		for (int i = 0; i < count; i++)
		{
			writer.Write((uint)HighScores.Scores[i].iLevelID, 8);
			int iHighScore = HighScores.Scores[i].iHighScore;
			int iSurvivalModeTime = HighScores.Scores[i].iSurvivalModeTime;
			bool flag2 = iHighScore >= 0;
			writer.Write(flag2);
			writer.Write((uint)((!flag2) ? (-iHighScore) : iHighScore), BitsRequiredForHighScore);
			writer.Write((uint)iSurvivalModeTime, BitsRequiredForTime);
		}
	}

	public bool Deserialise(BitStreamReader reader)
	{
		m_Machine = (User.MachineID)reader.ReadUInt32(BitsRequiredForMachineID);
		if (reader.ReadBit())
		{
			DLC = (int)reader.ReadUInt32(4);
		}
		else
		{
			DLC = -1;
		}
		uint num = reader.ReadUInt32(8);
		HighScores.Scores.Clear();
		for (int i = 0; i < num; i++)
		{
			int iLevelID = (int)reader.ReadUInt32(8);
			bool flag = reader.ReadBit();
			int num2 = (int)reader.ReadUInt32(BitsRequiredForHighScore);
			int iSurvivalModeTime = (int)reader.ReadUInt32(BitsRequiredForTime);
			if (!flag)
			{
				num2 = -num2;
			}
			HighScores.Scores.Add(new GameProgress.HighScores.Score
			{
				iLevelID = iLevelID,
				iHighScore = num2,
				iSurvivalModeTime = iSurvivalModeTime
			});
		}
		return true;
	}
}
