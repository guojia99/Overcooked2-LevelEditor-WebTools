using UnityEngine;

public class ServerTriggerAnimationQueue : ServerTimedQueue
{
	private TriggerAnimationQueue m_triggerQueue;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_triggerQueue = (TriggerAnimationQueue)synchronisedObject;
		m_triggerQueue.RegisterAnimationFinishedCallback(OnAnimationFinished);
		m_triggerQueue.RegisterAnimationTriggeredCallback(OnNotifyAnimationTriggered);
	}

	public void OnAnimationFinished(AnimationClip _clip)
	{
		AdvanceQueue();
	}

	public void OnNotifyAnimationTriggered(ITriggerAnimation _animator, AnimationClip _clip)
	{
		if (_animator != m_triggerQueue)
		{
			ResetQueue();
		}
	}
}
