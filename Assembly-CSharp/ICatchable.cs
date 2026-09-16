using UnityEngine;

public interface ICatchable
{
	bool AllowCatch(IHandleCatch _catcher, Vector2 _directionXZ);

	GameObject AccessGameObject();
}
