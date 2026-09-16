using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerTriggerToggleOnAnimator : ServerSynchroniserBase, ITriggerReceiver
{
	private TriggerToggleOnAnimator m_triggerOnAnimator;

	private TriggerToggleOnAnimatorMessage m_data = new TriggerToggleOnAnimatorMessage();

	public override EntityType GetEntityType()
	{
		return EntityType.TriggerToggleOnAnimator;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_triggerOnAnimator = (TriggerToggleOnAnimator)synchronisedObject;
		m_data.m_value = m_triggerOnAnimator.m_initialValue;
	}

	public void OnTrigger(string _trigger)
	{
		if (m_triggerOnAnimator.enabled && m_triggerOnAnimator.m_targetAnimator != null && m_triggerOnAnimator.m_triggerToReceive == _trigger)
		{
			m_data.m_value = !m_triggerOnAnimator.m_targetAnimator.GetBool(m_triggerOnAnimator.m_targetParameterHash);
			SendServerEvent(m_data);
		}
	}
}
