using BitStream;
using Team17.Online.Multiplayer.Messaging;

public class GameProgressDataNetworkMessage : Serialisable
{
	public bool[] MetaDialogsShownStatus = new bool[2];

	public GameProgress.GameProgressData ProgressData = new GameProgress.GameProgressData();

	private const int BitsRequiredForLevelCount = 8;

	private int BitsRequiredForHighScore = GameUtils.GetRequiredBitCount(65535);

	private int BitsRequiredForStars = GameUtils.GetRequiredBitCount(4);

	private int BitsRequiredForTime = GameUtils.GetRequiredBitCount(5999);

	private const int BitsRequiredForSwitches = 8;

	private int BitsRequiredForTeleportals = GameUtils.GetRequiredBitCount(26);

	public void Serialise(BitStreamWriter writer)
	{
		writer.Write((uint)ProgressData.LastLevelEntered, 8);
		writer.Write(ProgressData.NewGamePlusEnabled);
		writer.Write(ProgressData.NewGamePlusDialogShown);
		for (int i = 0; i < 2; i++)
		{
			writer.Write(MetaDialogsShownStatus[i]);
		}
		uint bits = (uint)ProgressData.Levels.Length;
		writer.Write(bits, 8);
		for (int j = 0; j < ProgressData.Levels.Length; j++)
		{
			GameProgress.GameProgressData.LevelProgress levelProgress = ProgressData.Levels[j];
			writer.Write((uint)levelProgress.LevelId, 8);
			writer.Write(levelProgress.Completed);
			writer.Write(levelProgress.Purchased);
			writer.Write(levelProgress.Revealed);
			writer.Write(levelProgress.ObjectivesCompleted);
			writer.Write(levelProgress.NGPEnabled);
			int num = levelProgress.HighScore;
			if (num == int.MinValue)
			{
				num = 65535;
			}
			bool flag = num >= 0;
			writer.Write(flag);
			writer.Write((uint)((!flag) ? (-num) : num), BitsRequiredForHighScore);
			writer.Write((uint)levelProgress.ScoreStars, BitsRequiredForStars);
			writer.Write((uint)levelProgress.SurvivalModeTime, BitsRequiredForTime);
		}
		uint bits2 = (uint)ProgressData.Switches.Length;
		writer.Write(bits2, 8);
		for (int k = 0; k < ProgressData.Switches.Length; k++)
		{
			GameProgress.GameProgressData.SwitchState switchState = ProgressData.Switches[k];
			writer.Write((uint)switchState.SwitchId, 8);
			writer.Write(switchState.Activated);
		}
		uint bits3 = (uint)ProgressData.Teleportals.Length;
		writer.Write(bits3, BitsRequiredForTeleportals);
		for (int l = 0; l < ProgressData.Teleportals.Length; l++)
		{
			GameProgress.GameProgressData.TeleportalState teleportalState = ProgressData.Teleportals[l];
			writer.Write((uint)teleportalState.World, BitsRequiredForTeleportals);
		}
	}

	public bool Deserialise(BitStreamReader reader)
	{
		ProgressData.LastLevelEntered = (int)reader.ReadUInt32(8);
		if (ProgressData.LastLevelEntered == 255)
		{
			ProgressData.LastLevelEntered = -1;
		}
		ProgressData.NewGamePlusEnabled = reader.ReadBit();
		ProgressData.NewGamePlusDialogShown = reader.ReadBit();
		for (int i = 0; i < 2; i++)
		{
			MetaDialogsShownStatus[i] = reader.ReadBit();
		}
		uint num = reader.ReadUInt32(8);
		ProgressData.Levels = new GameProgress.GameProgressData.LevelProgress[num];
		for (int j = 0; j < num; j++)
		{
			GameProgress.GameProgressData.LevelProgress levelProgress = new GameProgress.GameProgressData.LevelProgress();
			levelProgress.LevelId = (int)reader.ReadUInt32(8);
			levelProgress.Completed = reader.ReadBit();
			levelProgress.Purchased = reader.ReadBit();
			levelProgress.Revealed = reader.ReadBit();
			levelProgress.ObjectivesCompleted = reader.ReadBit();
			levelProgress.NGPEnabled = reader.ReadBit();
			bool flag = reader.ReadBit();
			levelProgress.HighScore = (int)reader.ReadUInt32(BitsRequiredForHighScore);
			if (!flag)
			{
				levelProgress.HighScore = -levelProgress.HighScore;
			}
			levelProgress.ScoreStars = (int)reader.ReadUInt32(BitsRequiredForStars);
			levelProgress.SurvivalModeTime = (int)reader.ReadUInt32(BitsRequiredForTime);
			ProgressData.Levels[j] = levelProgress;
		}
		uint num2 = reader.ReadUInt32(8);
		ProgressData.Switches = new GameProgress.GameProgressData.SwitchState[num2];
		for (int k = 0; k < num2; k++)
		{
			GameProgress.GameProgressData.SwitchState switchState = new GameProgress.GameProgressData.SwitchState();
			switchState.SwitchId = (int)reader.ReadUInt32(8);
			switchState.Activated = reader.ReadBit();
			ProgressData.Switches[k] = switchState;
		}
		uint num3 = reader.ReadUInt32(BitsRequiredForTeleportals);
		ProgressData.Teleportals = new GameProgress.GameProgressData.TeleportalState[num3];
		for (int l = 0; l < num3; l++)
		{
			GameProgress.GameProgressData.TeleportalState teleportalState = new GameProgress.GameProgressData.TeleportalState();
			teleportalState.World = (SceneDirectoryData.World)reader.ReadUInt32(BitsRequiredForTeleportals);
			ProgressData.Teleportals[l] = teleportalState;
		}
		return true;
	}
}
