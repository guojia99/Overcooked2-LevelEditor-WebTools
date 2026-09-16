public interface IFlowController
{
	bool InRound { get; }

	event CallbackVoid RoundActivatedCallback;

	event CallbackVoid RoundDeactivatedCallback;

	LevelConfigBase GetLevelConfig();

	GameConfig GetGameConfig();
}
