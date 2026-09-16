using OrderController;

namespace GameModes
{
	public delegate void OnSuccessfulDeliveryClient(TeamID teamId, OrderID orderId, float timePropRemainingPercentage, int tip, bool wasCombo, ClientPlateStation station);
}
