using System;
using UnityEngine;

public class RecipeFailureAnimation : WidgetAnimation
{
	private Color m_color = Color.red;

	private float m_time;

	private readonly float c_totalTime = 0.5f;

	private readonly float c_oscillations = 10f;

	private readonly float c_amplitude = 3f;

	private static int m_iFail = Animator.StringToHash("Fail");

	public override void Init(Animator _animator)
	{
		_animator.SetTrigger(m_iFail);
	}

	public override void Advance(float _deltaTime)
	{
		m_time += _deltaTime;
	}

	public override bool IsFinished()
	{
		return m_time > c_totalTime;
	}

	public override Color GetColourModifier()
	{
		return Color.Lerp(Color.white, m_color, Mathf.Sin((float)Math.PI * Mathf.Clamp01(m_time / c_totalTime)));
	}

	public override Vector2 GetPosModifier()
	{
		float x = c_amplitude * Mathf.Sin(c_oscillations * 2f * (float)Math.PI * m_time / c_totalTime);
		return new Vector2(x, 0f);
	}
}
