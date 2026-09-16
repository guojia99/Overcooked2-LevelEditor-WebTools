using UnityEngine;

public interface IHandleGridTransfer
{
	bool CanHandleTransfer(GridIndex _index, GameObject _object);

	void HandleTransfer(GridIndex _index, GameObject _object);
}
