using System;
using UnityEngine;

public class RecipeSuccessAnimation : WidgetAnimation
{
	private Color m_color = Color.green;

	private float m_time;

	private readonly float c_totalTime = 0.5f;

	private readonly float c_oscillations = 10f;

	private readonly float c_amplitude = 3f;

	public override void Advance(float _deltaTime)
	{
		m_time += _deltaTime;
	}

	public override bool IsFinished()
	{
		return m_time > c_totalTime;
	}

	private float SCurve(float _prop)
	{
		return 0.5f * (1f - Mathf.Cos((float)Math.PI * Mathf.Clamp01(_prop)));
	}

	public override Color GetColourModifier()
	{
		Color result = Color.Lerp(Color.white, m_color, Mathf.Sin((float)Math.PI / 2f * Mathf.Clamp01(2f * m_time / c_totalTime)));
		result.a = Mathf.Lerp(result.a, 0f, SCurve(2f * m_time / c_totalTime - 1f));
		return result;
	}
}
