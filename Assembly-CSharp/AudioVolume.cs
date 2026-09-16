using UnityEngine;
using UnityEngine.Audio;

internal abstract class AudioVolume : IQuantizedOption, OptionsData.IUnloadable, OptionsData.IUpdatable, OptionsData.IInit, IOption
{
	private AudioMixer m_mixer;

	private const float MinAudioVolume = -40f;

	private const float MuteAudioVolume = -80f;

	private float? m_volume;

	public abstract string Label { get; }

	public OptionsData.Categories Category
	{
		get
		{
			return OptionsData.Categories.Audio;
		}
	}

	public int Quanta
	{
		get
		{
			return 10;
		}
	}

	protected abstract string MixerControlName { get; }

	protected virtual float MaxAudioVolume
	{
		get
		{
			return 0f;
		}
	}

	public AudioVolume()
	{
	}

	public void Init()
	{
		m_mixer = GameUtils.RequireManager<AudioManager>().m_audioMixer;
	}

	public void Update()
	{
		if (m_volume.HasValue)
		{
			m_mixer.SetFloat(MixerControlName, m_volume.Value);
		}
	}

	public void Unload()
	{
		m_mixer.SetFloat(MixerControlName, 1f);
	}

	public void SetOption(int _value)
	{
		float value = ((_value != 0) ? MathUtils.ClampedRemap(_value, 0f, Quanta, -40f, MaxAudioVolume) : (-80f));
		m_volume = value;
	}

	public int GetOption()
	{
		float value = 0f;
		if (m_volume.HasValue)
		{
			value = m_volume.Value;
		}
		else if (m_mixer != null)
		{
			m_mixer.GetFloat(MixerControlName, out value);
		}
		float f = MathUtils.ClampedRemap(value, -40f, MaxAudioVolume, 0f, Quanta);
		return Mathf.RoundToInt(f);
	}

	public void Commit()
	{
		if (m_mixer != null && m_volume.HasValue)
		{
			float value = 0f;
			m_mixer.GetFloat(MixerControlName, out value);
			if (!Mathf.Approximately(m_volume.Value, value))
			{
				m_mixer.SetFloat(MixerControlName, m_volume.Value);
			}
		}
	}
}
