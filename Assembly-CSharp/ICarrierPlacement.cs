using UnityEngine;

public interface ICarrierPlacement
{
	GameObject InspectCarriedItem();

	void DestroyCarriedItem();

	GameObject TakeItem();
}
