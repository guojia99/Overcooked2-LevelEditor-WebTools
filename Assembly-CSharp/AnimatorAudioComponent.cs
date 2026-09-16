using System;
using UnityEngine;

public class AnimatorAudioComponent : MonoBehaviour
{
	private void AudioTrigger(string _tag)
	{
		Animator animator = base.gameObject.RequireComponent<Animator>();
		if (ShouldPlayEvent(animator, "AudioTrigger", _tag))
		{
			GameOneShotAudioTag audio = (GameOneShotAudioTag)Enum.Parse(typeof(GameOneShotAudioTag), _tag, true);
			GameUtils.TriggerAudio(audio, base.gameObject.layer);
		}
	}

	private void AudioStart(string _tag)
	{
		Animator animator = base.gameObject.RequireComponent<Animator>();
		if (ShouldPlayEvent(animator, "AudioTrigger", _tag))
		{
			GameLoopingAudioTag audio = (GameLoopingAudioTag)Enum.Parse(typeof(GameLoopingAudioTag), _tag, true);
			GameUtils.StartAudio(audio, this, base.gameObject.layer);
		}
	}

	private void AudioStop(string _tag)
	{
		GameLoopingAudioTag audio = (GameLoopingAudioTag)Enum.Parse(typeof(GameLoopingAudioTag), _tag, true);
		GameUtils.StopAudio(audio, this);
	}

	public static bool ShouldPlayEvent(Animator _animator, string _eventName, string _eventArguement)
	{
		bool flag = false;
		int layerCount = _animator.layerCount;
		for (int i = 0; i < layerCount; i++)
		{
			AnimatorClipInfo[] currentAnimatorClipInfo = _animator.GetCurrentAnimatorClipInfo(i);
			for (int j = 0; j < currentAnimatorClipInfo.Length; j++)
			{
				AnimationClip clip = currentAnimatorClipInfo[j].clip;
				AnimationEvent[] events = clip.events;
				foreach (AnimationEvent animationEvent in events)
				{
					if (animationEvent.functionName == _eventName && animationEvent.stringParameter == _eventArguement)
					{
						flag = true;
						if (currentAnimatorClipInfo[j].weight > 0.5f)
						{
							return true;
						}
					}
				}
			}
		}
		if (flag)
		{
			return false;
		}
		return true;
	}
}
