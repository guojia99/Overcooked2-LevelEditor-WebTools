using UnityEngine;

public class ClientTriggerQueue : ClientTimedQueue
{
	private TriggerQueue m_triggerQueue;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_triggerQueue = (TriggerQueue)synchronisedObject;
	}

	protected override void DoEvent(int _index)
	{
		base.DoEvent(_index);
		switch (m_triggerQueue.m_targetType)
		{
		case TriggerQueue.TriggerType.Animator:
			m_triggerQueue.m_animator.SetTrigger(m_triggerQueue.m_queue.m_triggerHashs[_index]);
			break;
		case TriggerQueue.TriggerType.Object:
			m_triggerQueue.m_targetObject.SendTrigger(m_triggerQueue.m_queue.m_triggers[_index]);
			break;
		}
	}
}
