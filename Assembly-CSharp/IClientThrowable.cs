using UnityEngine;

public interface IClientThrowable
{
	bool CanHandleThrow(IClientThrower _thrower, Vector2 _directionXZ);

	bool IsFlying();

	IClientThrower GetThrower();
}
