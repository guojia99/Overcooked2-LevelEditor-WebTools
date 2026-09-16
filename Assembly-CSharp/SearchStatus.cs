public class SearchStatus : BaseStatus
{
	public override string GetLocalisedProgressDescription()
	{
		switch (Progress)
		{
		case eConnectionModeSwitchProgress.NotStarted:
			return Localization.Get("Online.ConnectionMode.Progress.NotStarted");
		case eConnectionModeSwitchProgress.InProgress:
			return Localization.Get("Online.ConnectionMode.Matchmaking.Search.InProgress");
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
		SearchStatus searchStatus = new SearchStatus();
		searchStatus.Result = Result;
		searchStatus.Progress = Progress;
		return searchStatus;
	}
}
