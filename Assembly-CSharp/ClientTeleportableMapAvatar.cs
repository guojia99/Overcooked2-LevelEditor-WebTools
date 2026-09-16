using UnityEngine;

public class ClientTeleportableMapAvatar : ClientBaseTeleportable
{
	private MapAvatarControls m_controls;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_controls = base.gameObject.RequireComponent<MapAvatarControls>();
	}

	public override bool CanTeleport(ClientTeleportal _portal)
	{
		return m_controls != null && base.CanTeleport(_portal);
	}
}
