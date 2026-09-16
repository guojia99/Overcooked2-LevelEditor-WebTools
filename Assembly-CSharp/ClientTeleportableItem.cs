using UnityEngine;

public class ClientTeleportableItem : ClientBaseTeleportable
{
	private ClientPhysicalAttachment m_attachment;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_attachment = base.gameObject.RequireComponent<ClientPhysicalAttachment>();
	}

	public override bool CanTeleport(ClientTeleportal _portal)
	{
		return m_attachment != null && base.CanTeleport(_portal);
	}
}
