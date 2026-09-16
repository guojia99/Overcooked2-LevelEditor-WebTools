using UnityEngine;

public interface ITriggerAnimation
{
	void OnNotifyAnimationTriggered(ITriggerAnimation _animator, AnimationClip _clip);

	void OnAnimationFinished(AnimationClip _clip);
}
