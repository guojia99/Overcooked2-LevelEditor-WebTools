public interface IHeatTransferBehaviour
{
	bool CanTransferToContainer(IHeatContainer _container);

	void TransferToContainer(ICarrierPlacement _carrier, IHeatContainer _container);
}
