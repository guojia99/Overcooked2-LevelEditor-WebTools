public interface ITeleportable
{
	bool CanTeleport(ServerTeleportal _portal);

	bool IsTeleporting();

	void StartTeleport(ITeleportalSender _sender, ITeleportalReceiver _receiver);

	void EndTeleport(ITeleportalReceiver _receiver, ITeleportalSender _sender);

	void RegisterAllowTeleportCallback(Generic<bool> _callback);

	void UnregisterAllowTeleportCallback(Generic<bool> _callback);
}
