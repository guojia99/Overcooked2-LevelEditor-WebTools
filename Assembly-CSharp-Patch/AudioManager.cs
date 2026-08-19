using LevelEditor;
using LevelEditorStub;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

[ExecuteInEditMode]
public class AudioManager : Manager
{
	public enum AudioGroup
	{
		SFX = 0,
		Ambience = 1,
		Music = 2
	}

	private struct TagAudioSourcePair
	{
		public GameOneShotAudioTag m_tag;

		public AudioSource m_source;

		public TagAudioSourcePair(GameOneShotAudioTag tag, AudioSource source)
		{
			m_tag = tag;
			m_source = source;
		}
	}

	private class LoopingAudioToken
	{
		public AudioDirectoryData.LoopingAudioDirectoryEntry m_params;

		public FastList<object> m_owners = new FastList<object>();
	}

	private const string c_sourceAttachName = "SharedSource";

	[SerializeField]
	public AudioMixer m_audioMixer;

	[SerializeField]
	private AudioDirectoryData[] m_audioDirectories;

	private AudioMixerGroup m_sfxAudioGroup;

	private AudioMixerGroup m_ambAudioGroup;

	private AudioMixerGroup m_musAudioGroup;

	private GameObject m_sharedSource;

	private List<TagAudioSourcePair> m_oneShotAudio = new List<TagAudioSourcePair>();

	private FastList<LoopingAudioToken> m_loopingAudioTokens = new FastList<LoopingAudioToken>();

	protected static BitArray m_activeOneShotTags = new BitArray(320);

	private List<AudioDirectoryEntry> m_findEntryList = new List<AudioDirectoryEntry>();

	private static Predicate<TagAudioSourcePair> m_destroyInactivePredicate = delegate(TagAudioSourcePair obj)
	{
		if (!obj.m_source.isPlaying && !TimeManager.IsPaused(obj.m_source.gameObject))
		{
			m_activeOneShotTags[(int)obj.m_tag] = false;
			DestroySource(obj.m_source);
			return true;
		}
		return false;
	};

	protected virtual void Awake()
	{
		if (Application.isPlaying)
		{
			AudioMixer audioMixer = m_audioMixer;
			m_sfxAudioGroup = audioMixer.FindMatchingGroups("SFX")[0];
			m_ambAudioGroup = audioMixer.FindMatchingGroups("Amb")[0];
			m_musAudioGroup = audioMixer.FindMatchingGroups("Music")[0];
		}
	}

	protected void OnEnable()
	{
		Transform transform = base.transform.Find("SharedSource");
		if (transform != null)
		{
			m_sharedSource = transform.gameObject;
		}
		else
		{
			m_sharedSource = GameObjectUtils.CreateOnParent(base.gameObject, "SharedSource");
		}
		if (!Application.isPlaying)
		{
			m_sharedSource.hideFlags = HideFlags.HideAndDontSave;
		}
	}

	private void OnDisable()
	{
		StopAllCoroutines();
	}

	private void EditorFriendlyStartCoroutine(IEnumerator _coroutine)
	{
		if (Application.isPlaying)
		{
			StartCoroutine(_coroutine);
		}
		else
		{
			DebugAdvanceSystem.Add(_coroutine);
		}
	}

	protected virtual void Update()
	{
		m_oneShotAudio.RemoveAll(m_destroyInactivePredicate);
	}

	public bool AddAudioDirectory(AudioDirectoryData _directory)
	{
		if (!m_audioDirectories.Contains(_directory))
		{
			ArrayUtils.PushBack(ref m_audioDirectories, _directory);
			return true;
		}
		return false;
	}

	public AudioSource TriggerAudio(GameOneShotAudioTag _tag, int _layer)
	{
		return TriggerAudio(_tag, FindEntry(_tag) as AudioDirectoryData.OneShotAudioDirectoryEntry, _layer);
	}

	public AudioSource TriggerAudio(AudioDirectoryData.OneShotAudioDirectoryEntry _entry, int _layer)
	{
		return TriggerAudio(GameOneShotAudioTag.COUNT, _entry, _layer);
	}

	public AudioSource TriggerAudio(GameOneShotAudioTag _tag, AudioDirectoryData.OneShotAudioDirectoryEntry _entry, int _layer)
	{
		if (m_activeOneShotTags[(int)_tag] && _entry.SingleInstance)
		{
			return null;
		}
		m_activeOneShotTags[(int)_tag] = true;
		AudioSource audioSource = StartAudio(null, AudioGroup.SFX, _entry.AudioFile, _entry, false, _layer);
		m_oneShotAudio.Add(new TagAudioSourcePair(_entry.Tag, audioSource));
		return audioSource;
	}

	public AudioSource StartAudio(GameLoopingAudioTag _tag, object _uniqueToken, int _layer)
	{
		return StartAudio(FindEntry(_tag), _uniqueToken, _layer);
	}

	public AudioSource StartAudio(AudioDirectoryData.LoopingAudioDirectoryEntry _entry, object _uniqueToken, int _layer)
	{
		return StartAudio(_uniqueToken, _entry.AudioFile, _entry.StartClip, _entry.EndClip, _entry, AudioGroup.SFX, _layer);
	}

	public void StopAudio(GameLoopingAudioTag _tag, object _uniqueToken)
	{
		StopAudio(_uniqueToken);
	}

	protected AudioSource StartAudio(object _uniqueToken, AudioClip _audioFile, AudioClip _startClip, AudioClip _endClip, AudioDirectoryData.LoopingAudioDirectoryEntry _parameters, AudioGroup _group, int _layer)
	{
		if (GetLoopingToken(_parameters, _uniqueToken) != null)
		{
			return null;
		}
		LoopingAudioToken loopingAudioToken = AddLoopingToken(_parameters, _uniqueToken);
		if (_parameters.SingleInstance && loopingAudioToken.m_owners.Count > 1)
		{
			return null;
		}
		AudioSource audioSource = AddSource(_group, _layer);
		EditorFriendlyStartCoroutine(LoopingAudioRoutine(audioSource, _uniqueToken, _audioFile, _startClip, _endClip, _parameters, _group, _layer));
		return audioSource;
	}

	private IEnumerator LoopingAudioRoutine(AudioSource _source, object _uniqueToken, AudioClip _audioFile, AudioClip _startClip, AudioClip _endClip, AudioDirectoryData.LoopingAudioDirectoryEntry _parameters, AudioGroup _group, int _layer)
	{
		AudioSource startEndSource = null;
		if (_startClip != null || _endClip != null)
		{
			startEndSource = AddSource(_group, _layer);
		}
		if (_startClip != null)
		{
			StartAudio(startEndSource, _group, _startClip, _parameters, false, _layer);
			IEnumerator wait = CoroutineUtils.TimerRoutine(startEndSource.clip.length - _parameters.m_fadeInTime, base.gameObject.layer);
			while (wait.MoveNext())
			{
				yield return null;
			}
		}
		StartAudio(_source, _group, _audioFile, _parameters, true, _layer);
		if (_parameters.m_fadeInTime > 0f)
		{
			float initLoopVolume = _source.volume;
			float initStartVolume = ((!(_startClip != null)) ? 0f : startEndSource.volume);
			float elapsedTime = 0f;
			IEnumerator wait2 = CoroutineUtils.TimerRoutine(_parameters.m_fadeInTime, base.gameObject.layer);
			while (wait2.MoveNext() && GetLoopingToken(_parameters, (!_parameters.SingleInstance) ? _uniqueToken : null) != null)
			{
				elapsedTime += TimeManager.GetDeltaTime(base.gameObject.layer);
				float fadeProgress = Mathf.Clamp01(elapsedTime / _parameters.m_fadeInTime);
				if (_startClip != null)
				{
					startEndSource.volume = initStartVolume * (1f - fadeProgress);
				}
				_source.volume = initLoopVolume * fadeProgress;
				yield return null;
			}
			if (_startClip != null)
			{
				startEndSource.volume = 0f;
			}
		}
		while (GetLoopingToken(_parameters, (!_parameters.SingleInstance) ? _uniqueToken : null) != null)
		{
			yield return null;
		}
		if (_endClip != null)
		{
			StartAudio(startEndSource, _group, _endClip, _parameters, false, _layer);
		}
		if (_parameters.m_fadeOutTime > 0f)
		{
			if (_endClip != null)
			{
			}
			float fadeDuration = _parameters.m_fadeOutTime;
			float initLoopVolume2 = _source.volume;
			float initEndVolume = ((!(startEndSource != null)) ? 0f : startEndSource.volume);
			float elapsedTime2 = 0f;
			IEnumerator wait3 = CoroutineUtils.TimerRoutine(fadeDuration, base.gameObject.layer);
			while (wait3.MoveNext())
			{
				elapsedTime2 += TimeManager.GetDeltaTime(base.gameObject.layer);
				float fadeProgress2 = Mathf.Clamp01(elapsedTime2 / _parameters.m_fadeOutTime);
				if (_endClip != null)
				{
					startEndSource.volume = initEndVolume * fadeProgress2;
				}
				_source.volume = initLoopVolume2 * (1f - fadeProgress2);
				yield return null;
			}
			if (_endClip != null)
			{
				startEndSource.volume = initEndVolume;
			}
			_source.volume = 0f;
		}
		_source.Stop();
		if (_endClip != null)
		{
			while (startEndSource.isPlaying)
			{
				yield return null;
			}
		}
		if (startEndSource != null)
		{
			DestroySource(startEndSource);
		}
		DestroySource(_source);
	}

	protected void StopAudio(object _uniqueToken)
	{
		for (int num = m_loopingAudioTokens.Count - 1; num >= 0; num--)
		{
			LoopingAudioToken loopingAudioToken = m_loopingAudioTokens._items[num];
			loopingAudioToken.m_owners.Remove(_uniqueToken);
			if (loopingAudioToken.m_owners.Count == 0)
			{
				m_loopingAudioTokens.RemoveAt(num);
			}
		}
	}

	private AudioSource StartAudio(AudioSource _source, AudioGroup _group, AudioClip _clip, ClipParameters _parameters, bool _isLooping, int _layer)
	{
		if (_clip != null && _parameters != null)
		{
			AudioSource audioSource = _source;
			if (audioSource == null)
			{
				audioSource = AddSource(_group, _layer);
			}
			audioSource.clip = _clip;
			audioSource.priority = _parameters.Priority;
			float randomPitchVariance = _parameters.RandomPitchVariance;
			float num = _parameters.Pitch + UnityEngine.Random.Range(0f - randomPitchVariance, randomPitchVariance);
			audioSource.pitch = num * Time.timeScale;
			audioSource.volume = _parameters.Volume;
			audioSource.loop = _isLooping;
			audioSource.Play();
			if (TimeManager.IsPaused(audioSource.gameObject))
			{
				audioSource.Pause();
			}
			return audioSource;
		}
		return null;
	}

	private AudioSource AddSource(AudioGroup _group, int _layer)
	{
		GameObject gameObject = GameObjectUtils.CreateOnParent(m_sharedSource, "Sound");
		if (!Application.isPlaying)
		{
			gameObject.hideFlags = HideFlags.HideAndDontSave;
		}
		gameObject.layer = _layer;
		AudioSource audioSource = gameObject.AddComponent<AudioSource>();
		audioSource.outputAudioMixerGroup = GetMixerGroup(_group);
		return audioSource;
	}

	private static void DestroySource(AudioSource _source)
	{
		UnityEngine.Object.DestroyImmediate(_source.gameObject);
	}

	private AudioMixerGroup GetMixerGroup(AudioGroup _group)
	{
		AudioMixerGroup result = null;
		switch (_group)
		{
		case AudioGroup.SFX:
			result = m_sfxAudioGroup;
			break;
		case AudioGroup.Ambience:
			result = m_ambAudioGroup;
			break;
		case AudioGroup.Music:
			result = m_musAudioGroup;
			break;
		}
		return result;
	}

	private AudioDirectoryEntry FindEntry(GameOneShotAudioTag _tag)
	{
		for (int i = 0; i < m_audioDirectories.Length; i++)
		{
			AudioDirectoryData audioDirectoryData = m_audioDirectories[i];
			for (int j = 0; j < audioDirectoryData.OneShotAudio.Length; j++)
			{
				AudioDirectoryData.OneShotAudioDirectoryEntry oneShotAudioDirectoryEntry = audioDirectoryData.OneShotAudio[j];
				if (oneShotAudioDirectoryEntry.Tag == _tag)
				{
					m_findEntryList.Add(oneShotAudioDirectoryEntry);
				}
			}
		}
		// patch
#if UNITY_EDITOR
		if (m_findEntryList.Count == 0)
		{
            string path = "Assets/common02/pseudo_prefab_so/audio/AudioDirectories";
            string[] guids = AssetDatabase.FindAssets("", new[] { path });

            List<PseudoPrefabSO> allAudioDirectorySOs = new List<PseudoPrefabSO>();
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                allAudioDirectorySOs.Add(AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(assetPath));
            }
			AudioDirectoryData[] allAudioDirectoryData = allAudioDirectorySOs.Select(x => PseudoPrefabManager.LoadAsset<AudioDirectoryData>(x)).ToArray();
			int index = -1;
            for (int i = 0; i < allAudioDirectoryData.Length; i++)
            {
                AudioDirectoryData audioDirectoryData = allAudioDirectoryData[i];
                for (int j = 0; j < audioDirectoryData.OneShotAudio.Length; j++)
                {
                    AudioDirectoryData.OneShotAudioDirectoryEntry oneShotAudioDirectoryEntry = audioDirectoryData.OneShotAudio[j];
                    if (oneShotAudioDirectoryEntry.Tag == _tag)
                    {
						index = i;
                    }
                }
            }
			if (index != -1)
			{
                bool ok = EditorUtility.DisplayDialog(
                    "Error",
                    "missing levelInfo.audioDirectorySO: " + allAudioDirectorySOs[index].name,
                    "Exit Play Mode", "Cancel");
                if (ok)
				{
					EditorApplication.isPlaying = false;
                }
            }
        }
#endif
		// patch
		AudioDirectoryEntry result = m_findEntryList[UnityEngine.Random.Range(0, m_findEntryList.Count)];
		m_findEntryList.Clear();
		return result;
	}

	private AudioDirectoryData.LoopingAudioDirectoryEntry FindEntry(GameLoopingAudioTag _tag)
	{
		for (int i = 0; i < m_audioDirectories.Length; i++)
		{
			AudioDirectoryData audioDirectoryData = m_audioDirectories[i];
			for (int j = 0; j < audioDirectoryData.LoopingAudio.Length; j++)
			{
				AudioDirectoryData.LoopingAudioDirectoryEntry loopingAudioDirectoryEntry = audioDirectoryData.LoopingAudio[j];
				if (loopingAudioDirectoryEntry.Tag == _tag)
				{
					m_findEntryList.Add(loopingAudioDirectoryEntry);
				}
			}
		}
        // patch
#if UNITY_EDITOR
        if (m_findEntryList.Count == 0)
        {
            string path = "Assets/common02/pseudo_prefab_so/audio/AudioDirectories";
            string[] guids = AssetDatabase.FindAssets("", new[] { path });

            List<PseudoPrefabSO> allAudioDirectorySOs = new List<PseudoPrefabSO>();
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                allAudioDirectorySOs.Add(AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(assetPath));
            }
            AudioDirectoryData[] allAudioDirectoryData = allAudioDirectorySOs.Select(x => PseudoPrefabManager.LoadAsset<AudioDirectoryData>(x)).ToArray();
            int index = -1;
            for (int i = 0; i < allAudioDirectoryData.Length; i++)
            {
                AudioDirectoryData audioDirectoryData = allAudioDirectoryData[i];
                for (int j = 0; j < audioDirectoryData.LoopingAudio.Length; j++)
                {
                    AudioDirectoryData.LoopingAudioDirectoryEntry loopingAudioDirectoryEntry = audioDirectoryData.LoopingAudio[j];
                    if (loopingAudioDirectoryEntry.Tag == _tag)
                    {
                        index = i;
                    }
                }
            }
            if (index != -1)
            {
                bool ok = EditorUtility.DisplayDialog(
                    "Error",
                    "missing levelInfo.audioDirectorySO: " + allAudioDirectorySOs[index].name,
                    "Exit Play Mode", "Cancel");
                if (ok)
                {
                    EditorApplication.isPlaying = false;
                }
            }
        }
#endif
        // patch
        AudioDirectoryData.LoopingAudioDirectoryEntry result = m_findEntryList[UnityEngine.Random.Range(0, m_findEntryList.Count)] as AudioDirectoryData.LoopingAudioDirectoryEntry;
		m_findEntryList.Clear();
		return result;
	}

	private LoopingAudioToken AddLoopingToken(AudioDirectoryData.LoopingAudioDirectoryEntry _parameters, object _uniqueToken)
	{
		LoopingAudioToken loopingAudioToken = null;
		if (_parameters.SingleInstance)
		{
			loopingAudioToken = GetLoopingToken(_parameters);
		}
		if (loopingAudioToken == null)
		{
			loopingAudioToken = new LoopingAudioToken();
			loopingAudioToken.m_params = _parameters;
			m_loopingAudioTokens.Add(loopingAudioToken);
		}
		loopingAudioToken.m_owners.Add(_uniqueToken);
		return loopingAudioToken;
	}

	private LoopingAudioToken GetLoopingToken(AudioDirectoryData.LoopingAudioDirectoryEntry _parameters, object _uniqueToken = null)
	{
		for (int i = 0; i < m_loopingAudioTokens.Count; i++)
		{
			LoopingAudioToken loopingAudioToken = m_loopingAudioTokens._items[i];
			if (loopingAudioToken.m_params == _parameters && (_uniqueToken == null || loopingAudioToken.m_owners.Contains(_uniqueToken)))
			{
				return loopingAudioToken;
			}
		}
		return null;
	}

	protected AudioDirectoryData.LoopingAudioDirectoryEntry FindAmbienceData(GameLoopingAudioTag _tag)
	{
		return FindEntry(_tag);
	}
}
