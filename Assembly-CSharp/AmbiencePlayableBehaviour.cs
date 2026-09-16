using UnityEngine;
using UnityEngine.Playables;

public class AmbiencePlayableBehaviour : PlayableBehaviour
{
	private CampaignAudioManager m_audioManager;

	private GameLoopingAudioTag m_tag = GameLoopingAudioTag.COUNT;

	public void Setup(GameLoopingAudioTag _tag)
	{
		m_tag = _tag;
	}

	public override void OnBehaviourPlay(Playable playable, FrameData info)
	{
		base.OnBehaviourPlay(playable, info);
		if (Application.isPlaying && info.evaluationType == FrameData.EvaluationType.Playback && m_tag != GameLoopingAudioTag.COUNT && info.weight > 0f)
		{
			m_audioManager = GameUtils.RequireManager<CampaignAudioManager>();
			m_audioManager.StartAmbience(m_tag);
		}
	}

	public override void OnBehaviourPause(Playable playable, FrameData info)
	{
		base.OnBehaviourPause(playable, info);
		if (Application.isPlaying && info.evaluationType == FrameData.EvaluationType.Playback && m_audioManager != null)
		{
			m_audioManager.StopAmbience(m_tag);
		}
	}
}
