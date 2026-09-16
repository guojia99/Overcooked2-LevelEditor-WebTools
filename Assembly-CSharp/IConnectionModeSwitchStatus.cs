public interface IConnectionModeSwitchStatus
{
	eConnectionModeSwitchProgress GetProgress();

	string GetLocalisedProgressDescription();

	eConnectionModeSwitchResult GetResult();

	string GetLocalisedResultDescription();

	bool DisplayPlatformDialog();

	IConnectionModeSwitchStatus Clone();
}
