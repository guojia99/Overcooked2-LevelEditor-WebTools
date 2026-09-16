using UnityEngine;

public class ServerTeleportableItem : ServerBaseTeleportable
{
	private ServerPhysicalAttachment m_attachment;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_attachment = base.gameObject.RequireComponent<ServerPhysicalAttachment>();
	}

	public override bool CanTeleport(ServerTeleportal _portal)
	{
		return m_attachment != null && m_attachment.enabled && !m_attachment.IsAttached() && base.CanTeleport(_portal);
	}
}
