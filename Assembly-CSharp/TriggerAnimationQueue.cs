using System;
using UnityEngine;

[RequireComponent(typeof(TriggerAnimationCoordinator))]
public class TriggerAnimationQueue : TimedQueue, ITriggerAnimation
{
	[Serializable]
	public class AnimationQueue
	{
		[SerializeField]
		public AnimationClip[] m_clips = new AnimationClip[0];

		[SerializeField]
		public float[] m_delays = new float[0];
	}

	[SerializeField]
	public AnimationQueue m_queue = new AnimationQueue();

	private GenericVoid<AnimationClip> m_animFinishedCallback = delegate
	{
	};

	private GenericVoid<ITriggerAnimation, AnimationClip> m_animTriggeredCallback = delegate
	{
	};

	public void RegisterAnimationFinishedCallback(GenericVoid<AnimationClip> _callback)
	{
		m_animFinishedCallback = (GenericVoid<AnimationClip>)Delegate.Combine(m_animFinishedCallback, _callback);
	}

	public void DeregisterAnimationFinishedCallback(GenericVoid<AnimationClip> _callback)
	{
		m_animFinishedCallback = (GenericVoid<AnimationClip>)Delegate.Remove(m_animFinishedCallback, _callback);
	}

	public void RegisterAnimationTriggeredCallback(GenericVoid<ITriggerAnimation, AnimationClip> _callback)
	{
		m_animTriggeredCallback = (GenericVoid<ITriggerAnimation, AnimationClip>)Delegate.Combine(m_animTriggeredCallback, _callback);
	}

	public void DeregisterAnimationTriggeredCallback(GenericVoid<ITriggerAnimation, AnimationClip> _callback)
	{
		m_animTriggeredCallback = (GenericVoid<ITriggerAnimation, AnimationClip>)Delegate.Remove(m_animTriggeredCallback, _callback);
	}

	public override float GetQueueLength()
	{
		return m_queue.m_clips.Length;
	}

	public override float GetDelay(int _index)
	{
		return m_queue.m_delays[_index];
	}

	public void OnAnimationFinished(AnimationClip _clip)
	{
		m_animFinishedCallback(_clip);
	}

	public void OnNotifyAnimationTriggered(ITriggerAnimation _animator, AnimationClip _clip)
	{
		m_animTriggeredCallback(_animator, _clip);
	}
}
