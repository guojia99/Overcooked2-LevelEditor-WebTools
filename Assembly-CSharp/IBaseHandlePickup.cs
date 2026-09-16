public interface IBaseHandlePickup
{
	bool CanHandlePickup(ICarrier _carrier);

	int GetPickupPriority();
}
