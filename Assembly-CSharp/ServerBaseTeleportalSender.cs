using System.Collections;
using Team17.Online.Multiplayer.Messaging;

public abstract class ServerBaseTeleportalSender : ServerSynchroniserBase, ITeleportalSender
{
	private BaseTeleportalSender m_sender;

	protected bool m_sending;

	protected virtual void Awake()
	{
	}

	public bool CanTeleport(ITeleportable _object)
	{
		return !m_sending && CanHandleTeleport(_object);
	}

	public bool IsSending()
	{
		return m_sending;
	}

	public abstract bool CanHandleTeleport(ITeleportable _object);

	protected abstract IEnumerator TeleportRoutine(ServerTeleportal _exitPortal, ITeleportalReceiver _receiver, ITeleportable _object);

	public IEnumerator TeleportFromMe(ServerTeleportal _exitPortal, ITeleportalReceiver _receiver, ITeleportable _object)
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
