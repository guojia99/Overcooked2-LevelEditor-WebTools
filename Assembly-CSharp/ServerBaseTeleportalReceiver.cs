using System;
using System.Collections;
using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;

public abstract class ServerBaseTeleportalReceiver : ServerSynchroniserBase, ITeleportalReceiver
{
	private BaseTeleportalReceiver m_receiver;

	protected bool m_receiving;

	private List<Generic<bool>> m_allowTeleportCallbacks = new List<Generic<bool>>();

	private TeleportCallback m_teleportStartedCallback = delegate
	{
	};

	private TeleportCallback m_teleportFinishedCallback = delegate
	{
	};

	protected virtual void Awake()
	{
	}

	public bool CanTeleportTo(ITeleportable _object)
	{
		return !m_receiving && !m_allowTeleportCallbacks.CallForResult(false) && CanHandleTeleport(_object);
	}

	public bool IsReceiving()
	{
		return m_receiving;
	}

	public abstract bool CanHandleTeleport(ITeleportable _object);

	protected abstract IEnumerator TeleportRoutine(ServerTeleportal _entrancePortal, ITeleportalSender _sender, ITeleportable _object);

	public IEnumerator TeleportToMe(ServerTeleportal _entrancePortal, ITeleportalSender _sender, ITeleportable _object)
	{
		m_receiving = true;
		m_teleportStartedCallback(_object);
		IEnumerator routine = TeleportRoutine(_entrancePortal, _sender, _object);
		while (routine.MoveNext())
		{
			yield return null;
		}
		m_teleportFinishedCallback(_object);
		m_receiving = false;
	}

	public void RegisterAllowTeleportCallback(Generic<bool> _callback)
	{
		m_allowTeleportCallbacks.Add(_callback);
	}

	public void UnregisterAllowTeleportCallback(Generic<bool> _callback)
	{
		m_allowTeleportCallbacks.Add(_callback);
	}

	public void RegisterStartedTeleportCallback(TeleportCallback _callback)
	{
		m_teleportStartedCallback = (TeleportCallback)Delegate.Combine(m_teleportStartedCallback, _callback);
	}

	public void UnregisterStartedTeleportCallback(TeleportCallback _callback)
	{
		m_teleportStartedCallback = (TeleportCallback)Delegate.Remove(m_teleportStartedCallback, _callback);
	}

	public void RegisterFinishedTeleportCallback(TeleportCallback _callback)
	{
		m_teleportFinishedCallback = (TeleportCallback)Delegate.Combine(m_teleportFinishedCallback, _callback);
	}

	public void UnregisterFinishedTeleportCallback(TeleportCallback _callback)
	{
		m_teleportFinishedCallback = (TeleportCallback)Delegate.Remove(m_teleportFinishedCallback, _callback);
	}
}
