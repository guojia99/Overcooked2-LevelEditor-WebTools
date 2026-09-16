using Team17.Online;
using Team17.Online.Multiplayer.Messaging;

public class JoinData
{
	public User.MachineID machine;

	public UsersChangedMessage usersChanged = new UsersChangedMessage();

	public TimeSyncMessage timeSync = new TimeSyncMessage();

	public GameSetupMessage gameSetup = new GameSetupMessage();
}
