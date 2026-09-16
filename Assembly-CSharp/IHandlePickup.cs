using UnityEngine;

public interface IHandlePickup : IBaseHandlePickup
{
	void HandlePickup(ICarrier _carrier, Vector2 _directionXZ);
}
