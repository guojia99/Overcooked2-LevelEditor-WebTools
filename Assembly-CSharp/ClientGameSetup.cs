using Team17.Online;
using Team17.Online.Multiplayer.Messaging;

public static class ClientGameSetup
{
	public static GameMode Mode = GameMode.COUNT;

	public static string PrevScene = string.Empty;

	public static void Initialise()
	{
		Mailbox.Client.RegisterForMessageType(MessageType.GameSetup, OnGameSetupMessage);
	}

	public static void Shutdown()
	{
		Mailbox.Client.UnregisterForMessageType(MessageType.GameSetup, OnGameSetupMessage);
	}

	public static void OnGameSetupMessage(IOnlineMultiplayerSessionUserId sessionUser, Serialisable message)
	{
		Mode = ((GameSetupMessage)message).m_Mode;
		RichPresenceManager.SetGameMode(Mode);
	}
}
