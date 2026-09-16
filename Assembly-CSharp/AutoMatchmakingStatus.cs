public class AutoMatchmakingStatus : JoinSessionStatus
{
	public override string GetLocalisedProgressDescription()
	{
		switch (Progress)
		{
		case eConnectionModeSwitchProgress.NotStarted:
			return Localization.Get("Online.ConnectionMode.Progress.NotStarted");
		case eConnectionModeSwitchProgress.InProgress:
			return Localization.Get("Online.ConnectionMode.AutoMatchmaking.InProgress", new LocToken("[NAME]", currentUser.m_userName));
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
			return GetSpecificDescription();
		case eConnectionModeSwitchResult.Success:
			return Localization.Get("Online.ConnectionMode.Result.Success");
		default:
			return Localization.Get("Online.ConnectionMode.Result.Unhandled");
		}
	}
}
