using UnityEngine;

public abstract class WidgetAnimation
{
	public virtual void Init(Animator _animator)
	{
	}

	public abstract void Advance(float _deltaTime);

	public abstract bool IsFinished();

	public virtual Color GetColourModifier()
	{
		return Color.white;
	}

	public virtual Vector2 GetPosModifier()
	{
		return Vector2.zero;
	}

	public virtual Vector2 GetScaleModifier()
	{
		return Vector2.one;
	}
}
