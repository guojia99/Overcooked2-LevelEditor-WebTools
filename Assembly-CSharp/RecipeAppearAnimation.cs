using UnityEngine;

public class RecipeAppearAnimation : WidgetAnimation
{
	private float m_time;

	private AnimationCurve m_curve;

	public RecipeAppearAnimation(AnimationCurve curve)
	{
		m_curve = curve;
	}

	public override void Advance(float _deltaTime)
	{
		m_time += _deltaTime;
	}

	public override bool IsFinished()
	{
		return m_time > 1f;
	}

	public override Vector2 GetScaleModifier()
	{
		float num = m_curve.Evaluate(m_time % 1f);
		return new Vector2(num, num);
	}
}
