using UnityEngine;

public interface IThrowable
{
	bool CanHandleThrow(IThrower _thrower, Vector2 _directionXZ);

	void HandleThrow(IThrower _thrower, Vector2 _directionXZ);

	bool IsFlying();

	float GetFlightTime();

	IThrower GetThrower();

	IThrower GetPreviousThrower();

	void RegisterLandedCallback(GenericVoid<GameObject> _callback);

	void UnregisterLandedCallback(GenericVoid<GameObject> _callback);
}
