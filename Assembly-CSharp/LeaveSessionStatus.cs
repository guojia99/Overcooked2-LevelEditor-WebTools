public class LeaveSessionStatus : BaseStatus
{
	public override string GetLocalisedProgressDescription()
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

	public override string GetLocalisedResultDescription()
	{
		switch (Result)
		{
		case eConnectionModeSwitchResult.NotAvailableYet:
			return Localization.Get("Online.ConnectionMode.Result.NotAvailable");
		case eConnectionModeSwitchResult.Failure:
			return Localization.Get("Online.ConnectionMode.Result.Failure");
		case eConnectionModeSwitchResult.Success:
			return Localization.Get("Online.ConnectionMode.Result.Success");
		default:
			return Localization.Get("Online.ConnectionMode.Result.Unhandled");
		}
	}

	public override IConnectionModeSwitchStatus Clone()
	{
		LeaveSessionStatus leaveSessionStatus = new LeaveSessionStatus();
		leaveSessionStatus.Result = Result;
		leaveSessionStatus.Progress = Progress;
		return leaveSessionStatus;
	}
}
