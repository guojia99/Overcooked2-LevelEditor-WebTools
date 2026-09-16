using UnityEngine;

public interface ITileFlipAnimatorProvider
{
	Animator Begin(FlipDirection _direction);

	void End(FlipDirection _direction);

	bool IsComplete();
}
