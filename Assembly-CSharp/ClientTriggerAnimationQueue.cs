using UnityEngine;

public class ClientTriggerAnimationQueue : ClientTimedQueue
{
	private TriggerAnimationQueue m_triggerQueue;

	private ClientTriggerAnimationCoordinator m_coordinator;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_triggerQueue = (TriggerAnimationQueue)synchronisedObject;
		m_coordinator = base.gameObject.RequireComponent<ClientTriggerAnimationCoordinator>();
	}

	protected override void DoEvent(int _index)
	{
		base.DoEvent(_index);
		AnimationClip clip = m_triggerQueue.m_queue.m_clips[_index];
		m_coordinator.TriggerAnimation(m_triggerQueue, clip);
	}
}
