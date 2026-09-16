using UnityEngine;
using UnityEngine.Playables;

public class AudioTriggerPlayableBehaviour : PlayableBehaviour
{
	private GameOneShotAudioTag m_tag = GameOneShotAudioTag.COUNT;

	public void Setup(GameOneShotAudioTag _tag)
	{
		m_tag = _tag;
	}

	public override void OnBehaviourPlay(Playable playable, FrameData info)
	{
		base.OnBehaviourPlay(playable, info);
		if (Application.isPlaying && m_tag != GameOneShotAudioTag.COUNT && info.weight > 0f)
		{
			PlayableDirector playableDirector = playable.GetGraph().GetResolver() as PlayableDirector;
			GameUtils.TriggerAudio(m_tag, playableDirector.gameObject.layer);
		}
	}
}
