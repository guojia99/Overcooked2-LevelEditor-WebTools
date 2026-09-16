using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;

public abstract class ServerBaseTeleportable : ServerSynchroniserBase, ITeleportable
{
	private List<Generic<bool>> m_allowTeleportCallback = new List<Generic<bool>>();

	private TeleportState m_teleportState;

	public virtual bool CanTeleport(ServerTeleportal _portal)
	{
		return !m_allowTeleportCallback.CallForResult(false);
	}

	public bool IsTeleporting()
	{
		return m_teleportState != TeleportState.None;
	}

	public void StartTeleport(ITeleportalSender _sender, ITeleportalReceiver _receiver)
	{
		m_teleportState = TeleportState.Teleported;
	}

	public void EndTeleport(ITeleportalReceiver _receiver, ITeleportalSender _sender)
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
