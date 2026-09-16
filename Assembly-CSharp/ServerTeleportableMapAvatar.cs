using UnityEngine;

public class ServerTeleportableMapAvatar : ServerBaseTeleportable
{
	private MapAvatarControls m_controls;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_controls = base.gameObject.RequireComponent<MapAvatarControls>();
	}

	public override bool CanTeleport(ServerTeleportal _portal)
	{
		return m_controls != null && m_controls.enabled && base.CanTeleport(_portal);
	}
}
