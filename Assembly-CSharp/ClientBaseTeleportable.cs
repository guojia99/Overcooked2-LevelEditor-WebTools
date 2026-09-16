using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;

public abstract class ClientBaseTeleportable : ClientSynchroniserBase, IClientTeleportable
{
	private List<Generic<bool>> m_allowTeleportCallback = new List<Generic<bool>>();

	private TeleportState m_teleportState;

	public virtual bool CanTeleport(ClientTeleportal _portal)
	{
		return !m_allowTeleportCallback.CallForResult(false);
	}

	public bool IsTeleporting()
	{
		return m_teleportState != TeleportState.None;
	}

	public bool IsTeleported()
	{
		return m_teleportState == TeleportState.Teleported;
	}

	public void StartTeleportFrom(IClientTeleportalSender _sender, IClientTeleportalReceiver _receiver)
	{
		m_teleportState = TeleportState.Entering;
	}

	public void EndTeleportFrom(IClientTeleportalSender _sender, IClientTeleportalReceiver _receiver)
	{
		m_teleportState = TeleportState.Teleported;
	}

	public void StartTeleportTo(IClientTeleportalReceiver _receiver, IClientTeleportalSender _sender)
	{
		m_teleportState = TeleportState.Exiting;
	}

	public void EndTeleportTo(IClientTeleportalReceiver _receiver, IClientTeleportalSender _sender)
	{
		m_teleportState = TeleportState.None;
	}

	public void RegisterAllowTeleportCallback(Generic<bool> _callback)
	{
		m_allowTeleportCallback.Add(_callback);
	}

	public void UnregisterAllowTeleportCallback(Generic<bool> _callback)
	{
		m_allowTeleportCallback.Remove(_callback);
	}
}
