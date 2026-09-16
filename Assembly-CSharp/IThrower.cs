using UnityEngine;

public interface IThrower
{
	void ThrowItem(GameObject _object, Vector2 _directionXZ);

	void OnFailedToThrowItem(GameObject _object);

	void RegisterThrowCallback(GenericVoid<GameObject> _callback);

	void UnregisterThrowCallback(GenericVoid<GameObject> _callback);
}
