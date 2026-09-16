using UnityEngine;

public class ServerTeleportablePlayer : ServerBaseTeleportable
{
	private PlayerControls m_playerControls;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_playerControls = base.gameObject.RequireComponent<PlayerControls>();
	}

	public override bool CanTeleport(ServerTeleportal _portal)
	{
		return m_playerControls != null && m_playerControls.enabled && base.CanTeleport(_portal);
	}
}
