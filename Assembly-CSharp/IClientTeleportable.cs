public interface IClientTeleportable
{
	bool CanTeleport(ClientTeleportal _portal);

	bool IsTeleporting();

	bool IsTeleported();

	void StartTeleportFrom(IClientTeleportalSender _sender, IClientTeleportalReceiver _receiver);

	void EndTeleportFrom(IClientTeleportalSender _sender, IClientTeleportalReceiver _receiver);

	void StartTeleportTo(IClientTeleportalReceiver _receiver, IClientTeleportalSender _sender);

	void EndTeleportTo(IClientTeleportalReceiver _receiver, IClientTeleportalSender _sender);

	void RegisterAllowTeleportCallback(Generic<bool> _callback);

	void UnregisterAllowTeleportCallback(Generic<bool> _callback);
}
