using System.Collections;

public interface ITeleportalReceiver
{
	IEnumerator TeleportToMe(ServerTeleportal _entrancePortal, ITeleportalSender _sender, ITeleportable _object);

	bool CanHandleTeleport(ITeleportable _object);

	bool CanTeleportTo(ITeleportable _object);

	bool IsReceiving();

	void RegisterAllowTeleportCallback(Generic<bool> _callback);

	void UnregisterAllowTeleportCallback(Generic<bool> _callback);

	void RegisterStartedTeleportCallback(TeleportCallback _callback);

	void UnregisterStartedTeleportCallback(TeleportCallback _callback);

	void RegisterFinishedTeleportCallback(TeleportCallback _callback);

	void UnregisterFinishedTeleportCallback(TeleportCallback _callback);
}
