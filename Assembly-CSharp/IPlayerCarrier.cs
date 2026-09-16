using UnityEngine;

public interface IPlayerCarrier : ICarrier, ICarrierPlacement
{
	GameObject InspectCarriedItem(PlayerAttachTarget playerAttachTarget);

	GameObject TakeItem(PlayerAttachTarget playerAttachTarget);

	bool HasAttachment(PlayerAttachTarget playerAttachTarget);
}
