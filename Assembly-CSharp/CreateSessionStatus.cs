using Team17.Online;

public class CreateSessionStatus : BaseStatus
{
	public OnlineMultiplayerLocalUserId currentUser;

	public OnlineMultiplayerReturnCode<OnlineMultiplayerSessionCreateResult> sessionCreateResult = new OnlineMultiplayerReturnCode<OnlineMultiplayerSessionCreateResult>();

	public override string GetLocalisedProgressDescription()
	{
		switch (Progress)
		{
		case eConnectionModeSwitchProgress.NotStarted:
			return Localization.Get("Online.ConnectionMode.Progress.NotStarted");
		case eConnectionModeSwitchProgress.InProgress:
			return Localization.Get("Online.ConnectionMode.CreateSession.InProgress", new LocToken("[NAME]", currentUser.m_userName));
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
			return GetSpecificErrorCodeDescription();
		case eConnectionModeSwitchResult.Success:
			return Localization.Get("Online.ConnectionMode.Result.Success");
		default:
			return Localization.Get("Online.ConnectionMode.Result.Unhandled");
		}
	}

	public string GetSpecificErrorCodeDescription()
	{
		return Localization.Get("Online.ConnectionMode.CreateSession.Result." + sessionCreateResult.m_returnCode);
	}

	public override bool DisplayPlatformDialog()
	{
		return sessionCreateResult.DisplayPlatformSpecificError();
	}

	public override IConnectionModeSwitchStatus Clone()
	{
		CreateSessionStatus createSessionStatus = new CreateSessionStatus();
		createSessionStatus.Result = Result;
		createSessionStatus.Progress = Progress;
		createSessionStatus.currentUser = currentUser;
		createSessionStatus.sessionCreateResult = sessionCreateResult;
		return createSessionStatus;
	}
}
