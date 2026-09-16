public interface IContainerTransferBehaviour
{
	void TransferToContainer(ICarrierPlacement _carrier, IIngredientContents _container, bool _dontRemove);

	bool CanTransferToContainer(IIngredientContents _container);
}
