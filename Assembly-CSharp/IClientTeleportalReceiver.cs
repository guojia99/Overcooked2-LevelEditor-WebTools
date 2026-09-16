using System.Collections;

public interface IClientTeleportalReceiver
{
	IEnumerator TeleportToMe(ClientTeleportal _entrancePortal, IClientTeleportalSender _sender, IClientTeleportable _object);

	bool CanTeleportTo(IClientTeleportable _object);

	bool IsReceiving();

	void RegisterCanTeleportToCallback(Generic<bool> _callback);

	void UnregisterCanTeleportToCallback(Generic<bool> _callback);

	void RegisterStartedTeleportCallback(ClientTeleportCallback _callback);

	void UnregisterStartedTeleportCallback(ClientTeleportCallback _callback);

	void RegisterFinishedTeleportCallback(ClientTeleportCallback _callback);

	void UnregisterFinishedTeleportCallback(ClientTeleportCallback _callback);
}
