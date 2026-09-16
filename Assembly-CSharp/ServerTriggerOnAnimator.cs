using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerTriggerOnAnimator : ServerSynchroniserBase, ITriggerReceiver
{
	private TriggerOnAnimator m_triggerOnAnimator;

	private TriggerOnAnimatorMessage m_data = new TriggerOnAnimatorMessage();

	public override EntityType GetEntityType()
	{
		return EntityType.TriggerOnAnimator;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_triggerOnAnimator = (TriggerOnAnimator)synchronisedObject;
	}

	public void OnTrigger(string _trigger)
	{
		if (m_triggerOnAnimator.enabled && m_triggerOnAnimator.m_targetAnimator != null && m_triggerOnAnimator.m_triggerToReceive == _trigger)
		{
			SendServerEvent(m_data);
		}
	}
}
