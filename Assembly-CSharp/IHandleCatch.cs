using UnityEngine;

public interface IHandleCatch
{
	bool CanHandleCatch(ICatchable _object, Vector2 _directionXZ);

	void HandleCatch(ICatchable _object, Vector2 _directionXZ);

	void AlertToThrownItem(ICatchable _thrown, IThrower _thrower, Vector2 _directionXZ);

	int GetCatchingPriority();
}
