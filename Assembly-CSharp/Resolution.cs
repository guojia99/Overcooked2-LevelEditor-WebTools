using System;
using System.Collections.Generic;
using UnityEngine;

internal class Resolution : INameListOption, OptionsData.IInit, IOption
{
	private int m_resolutionIndex;

	private UnityEngine.Resolution m_resolution;

	public OptionsData.Categories Category
	{
		get
		{
			return OptionsData.Categories.Graphics;
		}
	}

	public string Label
	{
		get
		{
			return "OptionType.Resolution";
		}
	}

	private UnityEngine.Resolution[] GetResolutions()
	{
		if (Application.isEditor)
		{
			return new UnityEngine.Resolution[1] { GetWindowResolution() };
		}
		List<UnityEngine.Resolution> resolutions = new List<UnityEngine.Resolution>(Screen.resolutions);
		resolutions.Sort(CompareResolution);
		Predicate<UnityEngine.Resolution> match = delegate(UnityEngine.Resolution res)
		{
			int num = resolutions.FindIndex((UnityEngine.Resolution x) => x.width == res.width && x.height == res.height && x.refreshRate == res.refreshRate);
			if (res.width * res.height <= 480000)
			{
				return true;
			}
			if (num < resolutions.Count - 1)
			{
				UnityEngine.Resolution resolution = resolutions[num + 1];
				if (res.width == resolution.width && res.height == resolution.height)
				{
					return true;
				}
			}
			return false;
		};
		resolutions.RemoveAll(match);
		if (resolutions.Count == 0)
		{
			return new UnityEngine.Resolution[1] { GetWindowResolution() };
		}
		return resolutions.ToArray();
	}

	private static int CompareResolution(UnityEngine.Resolution _a, UnityEngine.Resolution _b)
	{
		return (int)(((float)_a.width + 0.001f) * (float)_a.height - ((float)_b.width + 0.001f) * (float)_b.height);
	}

	private UnityEngine.Resolution GetWindowResolution()
	{
		return new UnityEngine.Resolution
		{
			width = Screen.width,
			height = Screen.height,
			refreshRate = Screen.currentResolution.refreshRate
		};
	}

	public string[] GetNames()
	{
		return GetResolutions().ConvertAll((UnityEngine.Resolution x) => x.width + " x " + x.height);
	}

	public void SetOption(int _value)
	{
		int width = Screen.width;
		int height = Screen.height;
		UnityEngine.Resolution[] resolutions = GetResolutions();
		int num = Prefs.GetInt("ResolutionHash", 0);
		int num2 = 0;
		for (int i = 0; i < resolutions.Length; i++)
		{
			num2 ^= resolutions[i].width.GetHashCode();
			num2 ^= resolutions[i].height.GetHashCode();
		}
		if (num2 == num)
		{
			UnityEngine.Resolution resolution = resolutions[Mathf.Clamp(_value, 0, resolutions.Length - 1)];
			width = resolution.width;
			height = resolution.height;
		}
		Prefs.SetInt("ResolutionHash", num2);
		Generic<float, UnityEngine.Resolution> scoreFunction = (UnityEngine.Resolution _r) => (float)Mathf.Abs(_r.width - width) + 0.01f * (float)Mathf.Abs(_r.height - height);
		m_resolution = resolutions.FindLowestScoring(scoreFunction).Value;
	}

	public int GetOption()
	{
		UnityEngine.Resolution current = m_resolution;
		UnityEngine.Resolution[] resolutions = GetResolutions();
		Generic<float, UnityEngine.Resolution> scoreFunction = (UnityEngine.Resolution _r) => (float)Mathf.Abs(_r.width - current.width) + 0.01f * (float)Mathf.Abs(_r.height - current.height);
		return resolutions.FindLowestScoring(scoreFunction).Key;
	}

	public void Commit()
	{
		if (m_resolution.width != Screen.width || m_resolution.height != Screen.height)
		{
			Screen.SetResolution(m_resolution.width, m_resolution.height, Screen.fullScreen);
		}
	}

	public void Init()
	{
		m_resolution.width = Screen.width;
		m_resolution.height = Screen.height;
	}
}
