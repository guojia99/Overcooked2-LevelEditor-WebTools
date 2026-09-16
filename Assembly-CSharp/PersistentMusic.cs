using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AudioSource))]
public class PersistentMusic : MonoBehaviour
{
	private enum DeathType
	{
		None = 0,
		Sudden = 1,
		FadeOut = 2
	}

	[SerializeField]
	private float m_fadeTime = 2f;

	private static PersistentMusic[] s_allmusic = new PersistentMusic[0];

	private static float s_volume = 0.65f;

	private AudioSource m_audioSource;

	private string m_levelOfOrigin;

	private DeathType m_markedForDeath;

	private bool m_alive;

	private bool m_dead;

	private float m_lifeTime;

	private void Awake()
	{
		UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
		m_audioSource = base.gameObject.RequireComponent<AudioSource>();
		m_audioSource.volume = s_volume;
		m_alive = true;
		m_levelOfOrigin = SceneManager.GetActiveScene().name;
		SceneManager.sceneLoaded += OnSceneLoaded;
		for (int i = 0; i < s_allmusic.Length; i++)
		{
			s_allmusic[i].m_lifeTime += 0.071071f;
		}
		ArrayUtils.PushBack(ref s_allmusic, this);
	}

	private void Start()
	{
		if (!m_alive)
		{
			return;
		}
		for (int i = 0; i < s_allmusic.Length; i++)
		{
			if (s_allmusic[i] != this)
			{
				s_allmusic[i].OnMusicAdded(this);
			}
		}
	}

	private void OnDestroy()
	{
		s_allmusic = s_allmusic.AllRemoved_Predicate(Equals);
		SceneManager.sceneLoaded -= OnSceneLoaded;
	}

	private void Update()
	{
		if (m_dead)
		{
			Kill();
		}
		if (m_alive && m_markedForDeath != DeathType.None)
		{
			m_alive = false;
			switch (m_markedForDeath)
			{
			case DeathType.FadeOut:
				StartCoroutine(KillMeSoftly());
				break;
			case DeathType.Sudden:
				Kill();
				break;
			}
		}
		m_lifeTime += TimeManager.GetDeltaTime(base.gameObject);
	}

	public bool IsAlive()
	{
		return m_alive;
	}

	private void OnMusicAdded(PersistentMusic _otherMusic)
	{
		if (!m_alive)
		{
			return;
		}
		DeathType deathType = BattleOfTheBands(_otherMusic);
		if (deathType == DeathType.None)
		{
			return;
		}
		switch (m_markedForDeath)
		{
		case DeathType.FadeOut:
			if (deathType == DeathType.Sudden)
			{
				m_markedForDeath = deathType;
			}
			break;
		case DeathType.None:
			m_markedForDeath = deathType;
			break;
		}
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		if (!m_alive || mode == LoadSceneMode.Additive)
		{
			return;
		}
		PersistentMusic[] array = s_allmusic.FindAll((PersistentMusic x) => x.IsAlive());
		if (array.Length > 2)
		{
			if (Debug.isDebugBuild)
			{
				string text = SceneManager.GetActiveScene().name;
				string text2 = "{\n";
				for (int num = 0; num < array.Length; num++)
				{
					string text3 = text2;
					text2 = text3 + "     name: " + base.name + " level: " + array[num].m_levelOfOrigin + " track: " + array[num].ClipName() + " lifetime: " + array[num].m_lifeTime + "\n";
				}
				text2 += "}";
			}
			Array.Sort(array, (PersistentMusic x, PersistentMusic y) => x.m_lifeTime.CompareTo(y.m_lifeTime));
			for (int num2 = 2; num2 < array.Length; num2++)
			{
				array[num2].Kill();
			}
			Array.Resize(ref array, 2);
		}
		PersistentMusic otherMusic = array.TryAtIndex(array.FindIndex_Predicate((PersistentMusic x) => x != this));
		m_markedForDeath = BattleOfTheBands(otherMusic, true);
		if (m_markedForDeath == DeathType.None && !m_audioSource.isPlaying)
		{
			m_audioSource.Play();
		}
	}

	private DeathType BattleOfTheBands(PersistentMusic _otherMusic, bool _newLevel = false)
	{
		if (_newLevel)
		{
			string text = SceneManager.GetActiveScene().name;
			if (m_levelOfOrigin != text)
			{
				if (_otherMusic == null || _otherMusic.m_audioSource.clip != m_audioSource.clip)
				{
					return DeathType.FadeOut;
				}
			}
			else if (_otherMusic != null)
			{
				if (_otherMusic.m_levelOfOrigin == m_levelOfOrigin)
				{
					if (m_lifeTime <= _otherMusic.m_lifeTime)
					{
						return DeathType.Sudden;
					}
				}
				else if (_otherMusic.m_audioSource.clip == m_audioSource.clip)
				{
					return DeathType.Sudden;
				}
			}
			else if (m_lifeTime > 0f)
			{
				return DeathType.Sudden;
			}
		}
		else
		{
			if (_otherMusic == null || _otherMusic.m_audioSource.clip != m_audioSource.clip)
			{
				return DeathType.FadeOut;
			}
			if (m_lifeTime <= _otherMusic.m_lifeTime)
			{
				return DeathType.Sudden;
			}
		}
		return DeathType.None;
	}

	private IEnumerator KillMeSoftly()
	{
		float volumeStart = m_audioSource.volume;
		while (m_audioSource.volume > 0f)
		{
			yield return null;
			float deltaTime = TimeManager.GetDeltaTime(base.gameObject);
			m_audioSource.volume = Mathf.Clamp01(m_audioSource.volume - volumeStart / m_fadeTime * deltaTime);
		}
		Kill();
	}

	private void Kill()
	{
		m_alive = false;
		m_dead = true;
		UnityEngine.Object.Destroy(base.gameObject);
	}

	private string ClipName()
	{
		if (m_audioSource.clip != null)
		{
			return m_audioSource.clip.name + m_audioSource.playOnAwake + m_audioSource.isPlaying;
		}
		return "null";
	}

	public AudioSource GetAudioSource()
	{
		return m_audioSource;
	}

	public void StopMusic(bool _kill)
	{
		m_markedForDeath = (_kill ? DeathType.Sudden : BattleOfTheBands(null));
	}
}
