using Team17.Online;

public class JoinSessionStatus : BaseStatus
{
	public OnlineMultiplayerLocalUserId currentUser;

	public OnlineMultiplayerReturnCode<OnlineMultiplayerSessionJoinResult> sessionJoinResult = new OnlineMultiplayerReturnCode<OnlineMultiplayerSessionJoinResult>();

	public override string GetLocalisedProgressDescription()
	{
		switch (Progress)
		{
		case eConnectionModeSwitchProgress.NotStarted:
			return Localization.Get("Online.ConnectionMode.Progress.NotStarted");
		case eConnectionModeSwitchProgress.InProgress:
			return Localization.Get("Online.ConnectionMode.JoinSession.InProgress", new LocToken("[NAME]", (currentUser == null) ? string.Empty : currentUser.m_userName));
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
		case eConnectionModeSwitchResult.Success:
		case eConnectionModeSwitchResult.Failure:
			return GetSpecificDescription();
		default:
			return Localization.Get("Online.ConnectionMode.Result.Unhandled");
		}
	}

	public string GetSpecificDescription()
	{
		return Localization.Get("Online.ConnectionMode.JoinSession.Result." + sessionJoinResult.m_returnCode);
	}

	public override bool DisplayPlatformDialog()
	{
		return sessionJoinResult.DisplayPlatformSpecificError();
	}

	public override IConnectionModeSwitchStatus Clone()
	{
		JoinSessionStatus joinSessionStatus = new JoinSessionStatus();
		joinSessionStatus.Result = Result;
		joinSessionStatus.Progress = Progress;
		joinSessionStatus.currentUser = currentUser;
		joinSessionStatus.sessionJoinResult = sessionJoinResult;
		return joinSessionStatus;
	}
}
