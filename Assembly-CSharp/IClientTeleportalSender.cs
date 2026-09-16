using System.Collections;

public interface IClientTeleportalSender
{
	IEnumerator TeleportFromMe(ClientTeleportal _exitPortal, IClientTeleportalReceiver _receiver, IClientTeleportable _object);

	bool IsSending();
}
