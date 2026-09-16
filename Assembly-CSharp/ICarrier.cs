using UnityEngine;

public interface ICarrier : ICarrierPlacement
{
	void RegisterCarriedItemChangeCallback(VoidGeneric<GameObject, GameObject> _callback);

	void UnregisterCarriedItemChangeCallback(VoidGeneric<GameObject, GameObject> _callback);

	void CarryItem(GameObject _object);

	GameObject AccessGameObject();
}
