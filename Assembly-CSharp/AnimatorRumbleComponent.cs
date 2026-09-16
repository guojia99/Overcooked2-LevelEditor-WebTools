using System;
using UnityEngine;

public class AnimatorRumbleComponent : MonoBehaviour
{
	private void RumbleTrigger(string _tag)
	{
		Animator animator = base.gameObject.RequireComponent<Animator>();
		if (AnimatorAudioComponent.ShouldPlayEvent(animator, "RumbleTrigger", _tag))
		{
			GameOneShotAudioTag audio = (GameOneShotAudioTag)Enum.Parse(typeof(GameOneShotAudioTag), _tag, true);
			GameUtils.TriggerNXRumble(audio);
		}
	}

	private void RumbleStart(string _tag)
	{
		Animator animator = base.gameObject.RequireComponent<Animator>();
		if (AnimatorAudioComponent.ShouldPlayEvent(animator, "RumbleTrigger", _tag))
		{
			GameLoopingAudioTag audio = (GameLoopingAudioTag)Enum.Parse(typeof(GameLoopingAudioTag), _tag, true);
			GameUtils.StartNXRumble(audio);
		}
	}

	private void RumbleStop(string _tag)
	{
		GameLoopingAudioTag audio = (GameLoopingAudioTag)Enum.Parse(typeof(GameLoopingAudioTag), _tag, true);
		GameUtils.StopNXRumble(audio);
	}
}
