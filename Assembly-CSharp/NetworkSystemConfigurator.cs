using Team17.Online;
using Team17.Online.Multiplayer;
using Team17.Online.Multiplayer.Connection;
using Team17.Online.Multiplayer.Messaging;

public static class NetworkSystemConfigurator
{
	public static void Server(Server server, Client client, OnlineMultiplayerLocalUserId localUserId)
	{
		ClientUserSystem.ClearAvatarImageCache();
		SetupServer(server, client);
		ServerGameSetup.Mode = ServerSessionPropertyValuesProvider.GetGameMode();
		server.GetUserSystem().ResetUsersToOnlineState(localUserId);
		Mailbox.Server.ResetSequenceNumbers();
		Mailbox.Client.ResetSequenceNumbers();
	}

	public static void Client(Client client, JoinData joinData, IOnlineMultiplayerSessionCoordinator session)
	{
		ClientUserSystem.ClearAvatarImageCache();
		RemoteConnection remoteConnection = new RemoteConnection();
		remoteConnection.Initialise(client, session, UserSystemUtils.GetSessionHostUser());
		client.Initialise(true);
		client.HandleOutgoingServerConnectionAccepted(remoteConnection);
		ClientMessenger.OnClientStarted(client);
		MultiplayerController multiplayerController = GameUtils.RequireManager<MultiplayerController>();
		multiplayerController.SwitchNodeType(MultiplayerController.NodeType.Client);
		if (joinData != null)
		{
			ClientUserSystem.SetMachineId(joinData.machine);
			client.HandleManuallyDeserialisedMessage(UserSystemUtils.GetSessionHostUser(), MessageType.TimeSync, joinData.timeSync);
			client.HandleManuallyDeserialisedMessage(UserSystemUtils.GetSessionHostUser(), MessageType.UsersChanged, joinData.usersChanged);
			client.HandleManuallyDeserialisedMessage(UserSystemUtils.GetSessionHostUser(), MessageType.GameSetup, joinData.gameSetup);
		}
		Mailbox.Server.ResetSequenceNumbers();
		Mailbox.Client.ResetSequenceNumbers();
		ServerGameSetup.BecomeClient();
	}

	public static void Offline(Server server, Client client)
	{
		ClientUserSystem.ClearAvatarImageCache();
		SetupServer(server, client);
		server.GetUserSystem().ResetUsersToOfflineState();
		Mailbox.Server.ResetSequenceNumbers();
		Mailbox.Client.ResetSequenceNumbers();
	}

	private static void SetupServer(Server server, Client client)
	{
		ServerUserSystem.s_LocalMachineId = User.MachineID.One;
		ClientUserSystem.SetMachineId(User.MachineID.One);
		LocalLoopbackConnection localLoopbackConnection = new LocalLoopbackConnection();
		LocalLoopbackConnection localLoopbackConnection2 = new LocalLoopbackConnection();
		localLoopbackConnection.Initialise(server, localLoopbackConnection2);
		localLoopbackConnection2.Initialise(client, localLoopbackConnection);
		server.EnsureLocalLoopbackClientConnection(localLoopbackConnection);
		client.HandleOutgoingServerConnectionAccepted(localLoopbackConnection2);
		ServerMessenger.OnServerStarted(server);
		ClientMessenger.OnClientStarted(client);
		MultiplayerController multiplayerController = GameUtils.RequireManager<MultiplayerController>();
		multiplayerController.SwitchNodeType(MultiplayerController.NodeType.Server);
		ServerTime.StartTime();
	}
}
