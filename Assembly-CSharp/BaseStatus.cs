public abstract class BaseStatus : IConnectionModeSwitchStatus
{
	public eConnectionModeSwitchResult Result;

	public eConnectionModeSwitchProgress Progress;

	public eConnectionModeSwitchProgress GetProgress()
	{
		return Progress;
	}

	public virtual string GetLocalisedProgressDescription()
	{
		switch (Progress)
		{
		case eConnectionModeSwitchProgress.NotStarted:
			return Localization.Get("Online.ConnectionMode.Progress.NotStarted");
		case eConnectionModeSwitchProgress.InProgress:
			return Localization.Get("Online.ConnectionMode.LeaveSession.InProgress");
		case eConnectionModeSwitchProgress.Complete:
			return Localization.Get("Online.ConnectionMode.Progress.Complete");
		default:
			return Localization.Get("Online.ConnectionMode.Progress.Unhandled");
		}
	}

	public eConnectionModeSwitchResult GetResult()
	{
		return Result;
	}

	public virtual bool DisplayPlatformDialog()
	{
		return false;
	}

	public abstract IConnectionModeSwitchStatus Clone();

	public abstract string GetLocalisedResultDescription();
}
