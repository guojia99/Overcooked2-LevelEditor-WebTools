using OrderController;

namespace GameModes
{
	public delegate void OnSuccessfulDeliveryServer(OrderID orderID, RecipeList.Entry entry, float timePropRemainingPercentage, bool wasCombo, ServerPlateStation station);
}
