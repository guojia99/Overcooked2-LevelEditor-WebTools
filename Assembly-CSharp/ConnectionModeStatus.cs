using Team17.Online;

public class ConnectionModeStatus : BaseStatus
{
	public OnlineMultiplayerReturnCode<OnlineMultiplayerConnectionModeConnectResult> m_Result;

	public override string GetLocalisedProgressDescription()
	{
		switch (Progress)
		{
		case eConnectionModeSwitchProgress.NotStarted:
			return Localization.Get("Online.ConnectionMode.ConnectionMode.InProgress");
		case eConnectionModeSwitchProgress.InProgress:
			return Localization.Get("Online.ConnectionMode.ConnectionMode.InProgress");
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
		if (m_Result != null && m_Result.m_returnCode == OnlineMultiplayerConnectionModeConnectResult.eGenericFailure)
		{
			return Localization.Get("Online.ConnectionMode.ConnectionMode.Result.eGeneric");
		}
		return Localization.Get("Online.ConnectionMode.Result.Unhandled");
	}

	public override bool DisplayPlatformDialog()
	{
		return m_Result.DisplayPlatformSpecificError();
	}

	public override IConnectionModeSwitchStatus Clone()
	{
		ConnectionModeStatus connectionModeStatus = new ConnectionModeStatus();
		connectionModeStatus.Result = Result;
		connectionModeStatus.Progress = Progress;
		connectionModeStatus.m_Result = m_Result;
		return connectionModeStatus;
	}
}
