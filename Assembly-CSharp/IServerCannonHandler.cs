using UnityEngine;

public interface IServerCannonHandler
{
	void Load(GameObject _obj);

	void Unload(GameObject _obj);

	void ExitCannonRoutine(GameObject _obj);

	bool CanHandle(GameObject _obj);
}
