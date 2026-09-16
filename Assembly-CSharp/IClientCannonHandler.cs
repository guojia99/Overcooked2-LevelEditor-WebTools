using System.Collections;
using UnityEngine;

public interface IClientCannonHandler
{
	void Load(GameObject _obj);

	IEnumerator ExitCannonRoutine(GameObject _obj, Vector3 _exitPosition, Quaternion _exitRotation);

	bool CanHandle(GameObject _obj);

	void Launch(GameObject _obj);

	void Land(GameObject _obj);
}
