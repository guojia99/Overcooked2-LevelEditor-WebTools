using System.Collections;

public interface ITeleportalSender
{
	IEnumerator TeleportFromMe(ServerTeleportal _exitPortal, ITeleportalReceiver _receiver, ITeleportable _object);

	bool CanHandleTeleport(ITeleportable _object);

	bool CanTeleport(ITeleportable _object);

	bool IsSending();
}
