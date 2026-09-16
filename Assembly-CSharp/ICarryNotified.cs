public interface ICarryNotified
{
	void OnCarryBegun(ICarrier _carrier);

	void OnCarryEnded(ICarrier _carrier);
}
