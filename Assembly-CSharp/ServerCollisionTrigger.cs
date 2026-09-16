using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerCollisionTrigger : ServerSynchroniserBase
{
	private CollisionTrigger m_trigger;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_trigger = (CollisionTrigger)synchronisedObject;
	}

	private void OnTriggerEnter(Collider other)
	{
		if ((m_trigger.m_collisionFilter.value & (1 << other.gameObject.layer)) != 0 && m_trigger.m_trigger != string.Empty)
		{
			base.gameObject.SendTrigger(m_trigger.m_trigger);
		}
	}

	private void OnCollisionEnter(Collision other)
	{
		OnTriggerEnter(other.collider);
	}
}
