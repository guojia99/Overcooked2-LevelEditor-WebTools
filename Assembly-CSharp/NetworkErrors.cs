using Team17.Online;

public static class NetworkErrors
{
	public static string CachedErrorTitle;

	public static string CachedErrorMessage;

	public static string GetDisconnectionMessageText(OnlineMultiplayerSessionDisconnectionResult reason)
	{
		string empty = string.Empty;
		switch (reason)
		{
		case OnlineMultiplayerSessionDisconnectionResult.eGeneric:
			return "Online.Disconnection.Generic";
		case OnlineMultiplayerSessionDisconnectionResult.eLostNetwork:
			return "Online.Disconnection.LostNetwork";
		case OnlineMultiplayerSessionDisconnectionResult.eGoneOffline:
			return "Online.Disconnection.GoneOffline";
		case OnlineMultiplayerSessionDisconnectionResult.eLoggedOut:
			return "Online.Disconnection.LoggedOut";
		case OnlineMultiplayerSessionDisconnectionResult.eKicked:
			return "Online.Disconnection.Kicked";
		case OnlineMultiplayerSessionDisconnectionResult.eApplicationSuspended:
			return "Online.Disconnection.ApplicationSuspended";
		case OnlineMultiplayerSessionDisconnectionResult.eHostDisconnected:
			return "Online.Disconnection.HostDisconnected";
		default:
			return "Online.Disconnection.Generic";
		}
	}

	public static string GetDisconnectionMessageText(OnlineMultiplayerConnectionModeErrorResult reason)
	{
		return "Online.Disconnection.ConnectionMode";
	}
}
