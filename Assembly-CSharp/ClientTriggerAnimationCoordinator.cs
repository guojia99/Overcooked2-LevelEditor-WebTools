using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientTriggerAnimationCoordinator : ClientSynchroniserBase, ITriggerReceiver
{
	private TriggerAnimationCoordinator m_coordinator;

	private ITriggerAnimation m_lastTriggerAnimation;

	private AnimationClip m_lastClip;

	private Animator m_animator;

	private AnimatorOverrideController m_animatorOverride;

	private List<KeyValuePair<AnimationClip, AnimationClip>> m_overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();

	private int m_overrideIndex = -1;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_coordinator = (TriggerAnimationCoordinator)synchronisedObject;
		Initialise();
	}

	private void Initialise()
	{
		m_animatorOverride = new AnimatorOverrideController(m_coordinator.m_templateAnimator);
		m_animatorOverride.name = "Generic Override Animator";
		m_animatorOverride.GetOverrides(m_overrides);
		m_overrideIndex = m_overrides.FindIndex((KeyValuePair<AnimationClip, AnimationClip> x) => x.Key == m_coordinator.m_templateClip);
		m_animator = base.gameObject.AddComponent<Animator>();
		m_animator.hideFlags = HideFlags.NotEditable;
		m_animator.runtimeAnimatorController = m_animatorOverride;
		base.gameObject.AddComponent<AnimatorAudioComponent>();
	}

	public void TriggerAnimation(ITriggerAnimation _animator, AnimationClip _clip)
	{
		m_lastTriggerAnimation = _animator;
		m_lastClip = _clip;
		m_animator.CrossFade(m_coordinator.m_iAnimationReadyStateHash, 0f, 0, 0f);
		OverrideTemplateClip(_clip);
		if (m_coordinator.m_triggerOnAnimator)
		{
			m_animator.SetTrigger(m_coordinator.m_iAnimationStartTriggerHash);
		}
		else
		{
			base.gameObject.SendTrigger(m_coordinator.m_animationStartTrigger);
		}
		ITriggerAnimation[] array = base.gameObject.RequestInterfaces<ITriggerAnimation>();
		for (int i = 0; i < array.Length; i++)
		{
			array[i].OnNotifyAnimationTriggered(_animator, _clip);
		}
	}

	private void OverrideTemplateClip(AnimationClip _clip)
	{
		KeyValuePair<AnimationClip, AnimationClip> value = new KeyValuePair<AnimationClip, AnimationClip>(m_coordinator.m_templateClip, _clip);
		m_overrides[m_overrideIndex] = value;
		m_animatorOverride.ApplyOverrides(m_overrides);
	}

	public void OnTrigger(string _trigger)
	{
		if (m_coordinator.m_animationFinishedTrigger == _trigger && m_lastTriggerAnimation != null)
		{
			m_lastTriggerAnimation.OnAnimationFinished(m_lastClip);
		}
	}
}
