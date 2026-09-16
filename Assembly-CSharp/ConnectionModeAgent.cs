using Team17.Online.Multiplayer;

public interface ConnectionModeAgent
{
	bool Start(Server server, Client client, object data, GenericVoid<IConnectionModeSwitchStatus> callback);

	void Stop();

	void InvalidateCallback(GenericVoid<IConnectionModeSwitchStatus> callback);

	IConnectionModeSwitchStatus GetStatus();

	object GetAgentData();

	void Update();
}
