using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientTriggerZone : ClientSynchroniserBase
{
	private TriggerZone m_triggerZone;

	private bool m_occupied;

	public override EntityType GetEntityType()
	{
		return EntityType.TriggerZone;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_triggerZone = (TriggerZone)synchronisedObject;
	}

	public bool IsOccupied()
	{
		return m_occupied;
	}
}
