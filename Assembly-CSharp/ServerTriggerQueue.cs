using UnityEngine;

public class ServerTriggerQueue : ServerTimedQueue
{
	private TriggerQueue m_triggerQueue;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_triggerQueue = (TriggerQueue)synchronisedObject;
		m_triggerQueue.RegisterFinishedCallback(OnTriggerFinished);
	}

	protected override void DoEvent(int _index)
	{
		base.DoEvent(_index);
		if (m_triggerQueue.m_targetType == TriggerQueue.TriggerType.Object || !m_triggerQueue.m_waitForFinished)
		{
			AdvanceQueue();
		}
	}

	public void OnTriggerFinished()
	{
		if (m_triggerQueue.m_targetType == TriggerQueue.TriggerType.Animator && m_triggerQueue.m_waitForFinished && IsActive())
		{
			AdvanceQueue();
		}
	}
}
