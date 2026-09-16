using System;
using System.Collections;
using GameModes;
using UnityEngine;

[ExecuteInEditMode]
public class CampaignAudioManager : AudioManager
{
	public enum AudioState
	{
		IntroState = 0,
		InLevel = 1,
		SummaryScreen = 2
	}

	public enum StopBehaviour
	{
		Cutoff = 0,
		ContinueToEnd = 1,
		Fade = 2
	}

	[Serializable]
	private class TimePair
	{
		public int TimeValue;

		public float MusicPitch;

		public GameOneShotAudioTag Tag;
	}

	[Header("Music")]
	[SerializeField]
	[Layer]
	private int m_musicLayer;

	[SerializeField]
	private PersistentMusic m_persistentMusicPrefab;

	[SerializeField]
	private AudioClip m_inLevelMusic;

	[SerializeField]
	private AudioClip m_summaryScreenMusic;

	[SerializeField]
	private TimePair[] m_timedMusicModifiers = new TimePair[0];

	[Header("Ambience")]
	[SerializeField]
	[Layer]
	private int m_ambienceLayer;

	[SerializeField]
	private GameLoopingAudioTag[] m_introAmbiences;

	[SerializeField]
	private GameLoopingAudioTag[] m_inLevelAmbiences;

	private AudioState? m_state;

	private PersistentMusic m_musicObject;

	private TimePair m_activeTimePair;

	private object[] m_ambienceTokens = new object[0];

	private DataStore m_dataStore;

	private static readonly DataStore.Id k_timeUpdatedId = new DataStore.Id("time.updated");

	protected override void Awake()
	{
		base.Awake();
		if (Application.isPlaying)
		{
			m_dataStore = GameUtils.RequireManager<DataStore>();
			m_dataStore.Register(k_timeUpdatedId, OnTimeUpdatedNotification);
		}
	}

	protected void OnDestroy()
	{
		if (m_dataStore != null)
		{
			m_dataStore.Unregister(k_timeUpdatedId, OnTimeUpdatedNotification);
		}
		AudioManager.m_activeOneShotTags.SetAll(false);
	}

	public void SetAudioState(AudioState _state)
	{
		if (m_state.HasValue && m_state.Value == _state)
		{
			return;
		}
		m_state = _state;
		switch (_state)
		{
		case AudioState.IntroState:
		{
			SetMusic_Internal(null);
			for (int num2 = 0; num2 < m_introAmbiences.Length; num2++)
			{
				object obj2 = StartAmbience_Internal(m_introAmbiences[num2], m_ambienceLayer);
				if (obj2 != null)
				{
					ArrayUtils.PushBack(ref m_ambienceTokens, obj2);
				}
			}
			break;
		}
		case AudioState.InLevel:
		{
			SetMusic_Internal(m_inLevelMusic);
			for (int num = 0; num < m_inLevelAmbiences.Length; num++)
			{
				object obj = StartAmbience_Internal(m_inLevelAmbiences[num], m_ambienceLayer);
				if (obj != null)
				{
					ArrayUtils.PushBack(ref m_ambienceTokens, obj);
				}
			}
			break;
		}
		case AudioState.SummaryScreen:
		{
			m_activeTimePair = null;
			SetMusic_Internal(null, true);
			if (m_summaryScreenMusic != null)
			{
				StartCoroutine(DelayedSetMusic(m_summaryScreenMusic, 6f));
			}
			for (int i = 0; i < m_ambienceTokens.Length; i++)
			{
				StopAudio(m_ambienceTokens[i]);
			}
			m_ambienceTokens.AllRemoved_Predicate((object x) => true);
			break;
		}
		}
	}

	private void SetMusic_Internal(AudioClip _audioFile, bool _killPrevious = false)
	{
		if (m_musicObject != null)
		{
			if (_killPrevious || _audioFile == null)
			{
				m_musicObject.StopMusic(_killPrevious);
			}
			m_musicObject = null;
		}
		if (!(_audioFile != null))
		{
			return;
		}
		m_musicObject = UnityEngine.Object.Instantiate(m_persistentMusicPrefab);
		if (m_musicObject != null)
		{
			AudioSource audioSource = m_musicObject.GetAudioSource();
			audioSource.gameObject.layer = m_musicLayer;
			audioSource.clip = _audioFile;
			if (m_activeTimePair != null)
			{
				audioSource.pitch = m_activeTimePair.MusicPitch;
			}
			audioSource.Play();
		}
	}

	private IEnumerator DelayedSetMusic(AudioClip _audioFile, float _delay)
	{
		yield return CoroutineUtils.TimerRoutine(_delay, base.gameObject.layer);
		SetMusic_Internal(m_summaryScreenMusic);
	}

	private object StartAmbience_Internal(GameLoopingAudioTag _tag, int _layer)
	{
		AudioDirectoryData.LoopingAudioDirectoryEntry loopingAudioDirectoryEntry = FindAmbienceData(_tag);
		if (loopingAudioDirectoryEntry != null)
		{
			object obj = _tag;
			AudioSource audioSource = StartAudio(obj, loopingAudioDirectoryEntry.AudioFile, loopingAudioDirectoryEntry.StartClip, loopingAudioDirectoryEntry.EndClip, loopingAudioDirectoryEntry, AudioGroup.Ambience, _layer);
			if (audioSource != null)
			{
				return obj;
			}
		}
		return null;
	}

	public void SetMusic(AudioClip _audioFile, bool _killPrevious = false)
	{
		SetMusic_Internal(_audioFile, _killPrevious);
	}

	public void StartAmbience(GameLoopingAudioTag _tag)
	{
		object obj = StartAmbience_Internal(_tag, m_ambienceLayer);
		if (obj != null)
		{
			ArrayUtils.PushBack(ref m_ambienceTokens, obj);
		}
	}

	public void StopAmbience(GameLoopingAudioTag _tag)
	{
		object token = _tag;
		StopAudio(token);
		m_ambienceTokens.AllRemoved_Predicate((object x) => x == token);
	}

	private void OnTimeUpdatedNotification(DataStore.Id id, object value)
	{
		if (GameUtils.GetGameSession().PendingGameModeSessionConfigChanges || GameUtils.GetGameSession().GameModeKind != Kind.Campaign || !(m_musicObject != null))
		{
			return;
		}
		float time = Convert.ToSingle(value);
		Generic<float, TimePair> scoreFunction = (TimePair _timePair) => (time < (float)_timePair.TimeValue) ? ((float)_timePair.TimeValue) : float.MaxValue;
		TimePair value2 = m_timedMusicModifiers.FindLowestScoring(scoreFunction).Value;
		if (value2 != m_activeTimePair)
		{
			m_activeTimePair = value2;
			if (m_activeTimePair != null)
			{
				TriggerAudio(m_activeTimePair.Tag, base.gameObject.layer);
			}
		}
		if (m_activeTimePair != null)
		{
			AudioSource audioSource = m_musicObject.GetAudioSource();
			if (audioSource != null)
			{
				audioSource.pitch = value2.MusicPitch;
			}
		}
	}
}
