using UnityEngine;
using UnityEngine.Rendering;

public static class StartScreenBackgroundDataUtils
{
	public static bool SwapBackgroundAudio(SerializedSceneData.AudioData _settings, ref PersistentMusic _musicSource, ref AudioSource _ambienceSource, bool _killPrevious)
	{
		if (_settings.Directories != null && _settings.Directories.Length > 0)
		{
			AudioManager audioManager = GameUtils.RequestManager<AudioManager>();
			if (audioManager != null)
			{
				for (int i = 0; i < _settings.Directories.Length; i++)
				{
					audioManager.AddAudioDirectory(_settings.Directories[i]);
				}
			}
		}
		if (_settings.Music != null && _musicSource != null && _musicSource.IsAlive() && _musicSource.GetAudioSource().clip != _settings.Music)
		{
			_musicSource.StopMusic(_killPrevious);
			_musicSource = Object.Instantiate(_musicSource);
			if (_musicSource != null)
			{
				AudioSource audioSource = _musicSource.GetAudioSource();
				audioSource.clip = _settings.Music;
				audioSource.Play();
			}
		}
		if (_ambienceSource != null)
		{
			bool isPlaying = _ambienceSource.isPlaying;
			_ambienceSource.Stop();
			_ambienceSource.clip = _settings.Ambience;
			if (isPlaying || (_ambienceSource.playOnAwake && _ambienceSource.time <= 0f))
			{
				_ambienceSource.Play();
			}
		}
		return true;
	}

	public static bool SetRenderData(SerializedSceneData.RenderData _data)
	{
		return LerpRenderData(_data, _data, 1f);
	}

	public static bool LerpRenderData(SerializedSceneData.RenderData _start, SerializedSceneData.RenderData _end, float _progress)
	{
		bool flag = _progress < 0.5f;
		RenderSettings.ambientMode = ((!flag) ? _end.AmbientSource : _start.AmbientSource);
		switch (_end.AmbientSource)
		{
		case AmbientMode.Skybox:
			RenderSettings.ambientSkyColor = Color.Lerp(_start.SkyboxData.SkyboxColour, _end.SkyboxData.SkyboxColour, _progress);
			break;
		case AmbientMode.Trilight:
			RenderSettings.ambientSkyColor = Color.Lerp(_start.GradientData.SkyColour, _end.GradientData.SkyColour, _progress);
			RenderSettings.ambientEquatorColor = Color.Lerp(_start.GradientData.EquatorColour, _end.GradientData.EquatorColour, _progress);
			RenderSettings.ambientGroundColor = Color.Lerp(_start.GradientData.GroundColour, _end.GradientData.GroundColour, _progress);
			break;
		case AmbientMode.Flat:
			RenderSettings.ambientSkyColor = Color.Lerp(_start.ColorData.Colour, _end.ColorData.Colour, _progress);
			break;
		default:
			return false;
		}
		RenderSettings.defaultReflectionMode = ((!flag) ? _end.ReflectionSource : _start.ReflectionSource);
		RenderSettings.skybox = ((!flag) ? _end.SkyboxMaterial : _start.SkyboxMaterial);
		switch (_end.ReflectionSource)
		{
		case DefaultReflectionMode.Skybox:
			RenderSettings.defaultReflectionResolution = (int)Mathf.Lerp(_start.SkyboxReflectionData.Resolution, _end.SkyboxReflectionData.Resolution, _progress);
			RenderSettings.reflectionIntensity = Mathf.Lerp(_start.SkyboxReflectionData.IntensityMultiplier, _end.SkyboxReflectionData.IntensityMultiplier, _progress);
			RenderSettings.reflectionBounces = (int)Mathf.Lerp(_start.SkyboxReflectionData.Bounces, _end.SkyboxReflectionData.Bounces, _progress);
			break;
		case DefaultReflectionMode.Custom:
			RenderSettings.customReflection = ((!flag) ? _end.CustomReflectionData.Cubemap : _start.CustomReflectionData.Cubemap);
			RenderSettings.reflectionIntensity = Mathf.Lerp(_start.CustomReflectionData.IntensityMultiplier, _end.CustomReflectionData.IntensityMultiplier, _progress);
			RenderSettings.reflectionBounces = (int)Mathf.Lerp(_start.CustomReflectionData.Bounces, _end.CustomReflectionData.Bounces, _progress);
			break;
		default:
			return false;
		}
		return true;
	}

	public static SerializedSceneData.RenderData CopyCurrentRenderData()
	{
		SerializedSceneData.RenderData renderData = new SerializedSceneData.RenderData();
		renderData.SkyboxMaterial = RenderSettings.skybox;
		renderData.AmbientSource = RenderSettings.ambientMode;
		renderData.SkyboxData.SkyboxColour = RenderSettings.ambientSkyColor;
		renderData.GradientData.SkyColour = RenderSettings.ambientSkyColor;
		renderData.GradientData.EquatorColour = RenderSettings.ambientEquatorColor;
		renderData.GradientData.GroundColour = RenderSettings.ambientGroundColor;
		renderData.ColorData.Colour = RenderSettings.ambientSkyColor;
		renderData.ReflectionSource = RenderSettings.defaultReflectionMode;
		renderData.SkyboxReflectionData.Resolution = RenderSettings.defaultReflectionResolution;
		renderData.SkyboxReflectionData.IntensityMultiplier = RenderSettings.reflectionIntensity;
		renderData.SkyboxReflectionData.Bounces = RenderSettings.reflectionBounces;
		renderData.CustomReflectionData.Cubemap = RenderSettings.customReflection;
		renderData.CustomReflectionData.IntensityMultiplier = RenderSettings.reflectionIntensity;
		renderData.CustomReflectionData.Bounces = RenderSettings.reflectionBounces;
		return renderData;
	}
}
