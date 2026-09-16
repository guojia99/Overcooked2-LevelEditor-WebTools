using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientTriggerOnAnimator : ClientSynchroniserBase
{
	private TriggerOnAnimator m_triggerOnAnimator;

	public override EntityType GetEntityType()
	{
		return EntityType.TriggerOnAnimator;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_triggerOnAnimator = (TriggerOnAnimator)synchronisedObject;
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		DoEvent();
	}

	private void DoEvent()
	{
		if (m_triggerOnAnimator.enabled && m_triggerOnAnimator.m_targetAnimator != null)
		{
			m_triggerOnAnimator.m_targetAnimator.SetTrigger(m_triggerOnAnimator.m_triggerToFireHash);
		}
	}
}
