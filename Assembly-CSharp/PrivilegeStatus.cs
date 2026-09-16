using Team17.Online;

public class PrivilegeStatus : BaseStatus
{
	public GamepadUser currentUser;

	public OnlineMultiplayerReturnCode<OnlineMultiplayerPrivilegeCheckResult> privilegeCheckResult = new OnlineMultiplayerReturnCode<OnlineMultiplayerPrivilegeCheckResult>();

	public override string GetLocalisedProgressDescription()
	{
		switch (Progress)
		{
		case eConnectionModeSwitchProgress.NotStarted:
			if (null != currentUser)
			{
				return Localization.Get("Online.ConnectionMode.Privilege.InProgress", new LocToken("[NAME]", currentUser.DisplayName));
			}
			return string.Empty;
		case eConnectionModeSwitchProgress.InProgress:
			if (null != currentUser)
			{
				return Localization.Get("Online.ConnectionMode.Privilege.InProgress", new LocToken("[NAME]", currentUser.DisplayName));
			}
			return "In progress with no user?!?!";
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
		switch (privilegeCheckResult.m_returnCode)
		{
		case OnlineMultiplayerPrivilegeCheckResult.eSuccess:
			return Localization.Get("Online.ConnectionMode.Privilege.Result.eSuccess");
		case OnlineMultiplayerPrivilegeCheckResult.eNoNetwork:
			return Localization.Get("Online.ConnectionMode.Privilege.Result.eNoNetwork");
		case OnlineMultiplayerPrivilegeCheckResult.ePatchRequired:
			return Localization.Get("Online.ConnectionMode.Privilege.Result.ePatchRequired");
		case OnlineMultiplayerPrivilegeCheckResult.eSystemSoftwareUpdateRequired:
			return Localization.Get("Online.ConnectionMode.Privilege.Result.eSystemSoftwareUpdateRequired");
		case OnlineMultiplayerPrivilegeCheckResult.eNotSignedInToPlatform:
			return Localization.Get("Online.ConnectionMode.Privilege.Result.eNotSignedInToPlatform", new LocToken("[NAME]", currentUser.DisplayName));
		case OnlineMultiplayerPrivilegeCheckResult.eNoOnlineAccount:
			return Localization.Get("Online.ConnectionMode.Privilege.Result.eNoOnlineAccount", new LocToken("[NAME]", currentUser.DisplayName));
		case OnlineMultiplayerPrivilegeCheckResult.eUnderAge:
			return Localization.Get("Online.ConnectionMode.Privilege.Result.eUnderAge", new LocToken("[NAME]", currentUser.DisplayName));
		case OnlineMultiplayerPrivilegeCheckResult.eApplicationSuspended:
			return Localization.Get("Online.ConnectionMode.Privilege.Result.eApplicationSuspended");
		case OnlineMultiplayerPrivilegeCheckResult.eGenericFailure:
			return Localization.Get("Online.ConnectionMode.Privilege.Result.eGenericFailure");
		default:
			return Localization.Get("Online.Privileges.CheckFailed", new LocToken("[NAME]", currentUser.DisplayName));
		}
	}

	public override bool DisplayPlatformDialog()
	{
		return privilegeCheckResult.DisplayPlatformSpecificError();
	}

	public override IConnectionModeSwitchStatus Clone()
	{
		PrivilegeStatus privilegeStatus = new PrivilegeStatus();
		privilegeStatus.Result = Result;
		privilegeStatus.Progress = Progress;
		privilegeStatus.currentUser = currentUser;
		privilegeStatus.privilegeCheckResult = privilegeCheckResult;
		return privilegeStatus;
	}
}
