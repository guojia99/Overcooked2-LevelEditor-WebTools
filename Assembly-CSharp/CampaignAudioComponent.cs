using System;
using UnityEngine;

public class CampaignAudioComponent : MonoBehaviour
{
	[SerializeField]
	private AudioClip[] m_musicFiles = new AudioClip[0];

	private CampaignAudioManager m_audioManager;

	private void Awake()
	{
		m_audioManager = GameUtils.RequireManager<CampaignAudioManager>();
	}

	private void MusicStart(int _musicTrack)
	{
		if (_musicTrack >= 0)
		{
			if (_musicTrack < m_musicFiles.Length)
			{
				AudioClip audioFile = m_musicFiles[_musicTrack];
				m_audioManager.SetMusic(audioFile);
			}
		}
		else
		{
			m_audioManager.SetMusic(null);
		}
	}

	private void AmbienceStart(string _tag)
	{
		GameLoopingAudioTag gameLoopingAudioTag = (GameLoopingAudioTag)Enum.Parse(typeof(GameLoopingAudioTag), _tag, true);
		m_audioManager.StartAmbience(gameLoopingAudioTag);
	}

	private void AmbienceStop(string _tag)
	{
		GameLoopingAudioTag gameLoopingAudioTag = (GameLoopingAudioTag)Enum.Parse(typeof(GameLoopingAudioTag), _tag, true);
		m_audioManager.StopAmbience(gameLoopingAudioTag);
	}
}
