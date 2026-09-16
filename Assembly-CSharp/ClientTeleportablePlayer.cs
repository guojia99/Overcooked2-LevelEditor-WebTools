using UnityEngine;

public class ClientTeleportablePlayer : ClientBaseTeleportable
{
	private PlayerControls m_playerControls;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_playerControls = base.gameObject.RequireComponent<PlayerControls>();
	}

	public override bool CanTeleport(ClientTeleportal _portal)
	{
		return m_playerControls != null && base.CanTeleport(_portal);
	}
}
