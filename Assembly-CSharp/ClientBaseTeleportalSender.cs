using System.Collections;
using Team17.Online.Multiplayer.Messaging;

public abstract class ClientBaseTeleportalSender : ClientSynchroniserBase, IClientTeleportalSender
{
	private BaseTeleportalSender m_sender;

	protected bool m_sending;

	protected virtual void Awake()
	{
	}

	public bool IsSending()
	{
		return m_sending;
	}

	protected abstract IEnumerator TeleportRoutine(ClientTeleportal _exitPortal, IClientTeleportalReceiver _receiver, IClientTeleportable _object);

	public IEnumerator TeleportFromMe(ClientTeleportal _exitPortal, IClientTeleportalReceiver _receiver, IClientTeleportable _object)
	{
		m_sending = true;
		IEnumerator routine = TeleportRoutine(_exitPortal, _receiver, _object);
		while (routine.MoveNext())
		{
			yield return null;
		}
		m_sending = false;
	}
}
