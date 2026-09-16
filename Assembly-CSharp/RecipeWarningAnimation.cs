using UnityEngine;

public class RecipeWarningAnimation : WidgetAnimation
{
	private float m_time;

	private AnimationCurve m_curve;

	public RecipeWarningAnimation(AnimationCurve curve)
	{
		m_curve = curve;
	}

	public override void Advance(float _deltaTime)
	{
		m_time += _deltaTime;
	}

	public override bool IsFinished()
	{
		return m_time >= 1f;
	}

	public override Vector2 GetPosModifier()
	{
		float x = m_curve.Evaluate(m_time % 1f);
		return new Vector2(x, 0f);
	}
}
