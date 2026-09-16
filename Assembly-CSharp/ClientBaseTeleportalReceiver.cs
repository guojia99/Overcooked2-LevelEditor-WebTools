using System;
using System.Collections;
using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;

public abstract class ClientBaseTeleportalReceiver : ClientSynchroniserBase, IClientTeleportalReceiver
{
	private TeleportalPlayerReceiver m_receiver;

	private bool m_receiving;

	private List<Generic<bool>> m_canTeleportCallbacks = new List<Generic<bool>>();

	private ClientTeleportCallback m_teleportStartedCallback = delegate
	{
	};

	private ClientTeleportCallback m_teleportFinishedCallback = delegate
	{
	};

	protected virtual void Awake()
	{
	}

	public bool CanTeleportTo(IClientTeleportable _object)
	{
		return !m_receiving && !m_canTeleportCallbacks.CallForResult(false);
	}

	public bool IsReceiving()
	{
		return m_receiving;
	}

	protected abstract IEnumerator TeleportRoutine(ClientTeleportal _entrancePortal, IClientTeleportalSender _sender, IClientTeleportable _object);

	public IEnumerator TeleportToMe(ClientTeleportal _entrancePortal, IClientTeleportalSender _sender, IClientTeleportable _object)
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

	public void RegisterCanTeleportToCallback(Generic<bool> _callback)
	{
		m_canTeleportCallbacks.Add(_callback);
	}

	public void UnregisterCanTeleportToCallback(Generic<bool> _callback)
	{
		m_canTeleportCallbacks.Remove(_callback);
	}

	public void RegisterStartedTeleportCallback(ClientTeleportCallback _callback)
	{
		m_teleportStartedCallback = (ClientTeleportCallback)Delegate.Combine(m_teleportStartedCallback, _callback);
	}

	public void UnregisterStartedTeleportCallback(ClientTeleportCallback _callback)
	{
		m_teleportStartedCallback = (ClientTeleportCallback)Delegate.Remove(m_teleportStartedCallback, _callback);
	}

	public void RegisterFinishedTeleportCallback(ClientTeleportCallback _callback)
	{
		m_teleportFinishedCallback = (ClientTeleportCallback)Delegate.Combine(m_teleportFinishedCallback, _callback);
	}

	public void UnregisterFinishedTeleportCallback(ClientTeleportCallback _callback)
	{
		m_teleportFinishedCallback = (ClientTeleportCallback)Delegate.Remove(m_teleportFinishedCallback, _callback);
	}
}
