using UnityEngine;
using UnityEngine.Playables;

public class MusicPlayableBehaviour : PlayableBehaviour
{
	private CampaignAudioManager m_audioManager;

	private AudioClip m_audioFile;

	private bool m_killPrevious;

	public void Setup(AudioClip _clip, bool _killPrevious)
	{
		m_audioFile = _clip;
		m_killPrevious = _killPrevious;
	}

	public override void OnBehaviourPlay(Playable playable, FrameData info)
	{
		base.OnBehaviourPlay(playable, info);
		if (Application.isPlaying && info.evaluationType == FrameData.EvaluationType.Playback && m_audioFile != null && info.weight > 0f)
		{
			m_audioManager = GameUtils.RequireManager<CampaignAudioManager>();
			m_audioManager.SetMusic(m_audioFile, m_killPrevious);
		}
	}

	public override void OnBehaviourPause(Playable playable, FrameData info)
	{
		base.OnBehaviourPause(playable, info);
		if (Application.isPlaying && info.evaluationType == FrameData.EvaluationType.Playback && m_audioFile != null && m_audioManager != null)
		{
			m_audioManager.SetMusic(null);
		}
	}
}
