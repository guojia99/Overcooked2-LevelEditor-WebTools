using System;
using UnityEngine;
using UnityStandardAssets.ImageEffects;

[Serializable]
public class PerformanceSettings
{
	public bool UseSSAO;

	public bool UseColorCorrection;

	public RenderingPath RenderPath;

	public void ApplyToScene()
	{
		Camera main = Camera.main;
		if (main.renderingPath == RenderingPath.UsePlayerSettings)
		{
			main.renderingPath = RenderPath;
		}
		SetComponentEnabled<ScreenSpaceAmbientOcclusion>(main.gameObject, UseSSAO);
		SetComponentEnabled<ColorCorrectionCurves>(main.gameObject, UseColorCorrection);
	}

	private void SetComponentEnabled<T>(GameObject _object, bool _enabled) where T : Behaviour
	{
		T val = _object.RequestComponent<T>();
		if (val != null)
		{
			val.enabled = _enabled;
		}
	}
}
