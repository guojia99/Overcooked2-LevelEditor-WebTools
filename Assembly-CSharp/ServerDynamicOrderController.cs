using OrderController;

public class ServerDynamicOrderController : ServerFixedTimeOrderController
{
	public ServerDynamicOrderController(ref OrdersConfig _ordersConfig, VoidGeneric<OrderID> _addedCallback, VoidGeneric<OrderID> _timeoutCallback)
		: base(ref _ordersConfig, _addedCallback, _timeoutCallback)
	{
	}

	public void MoveToNextPhase()
	{
		DynamicRoundData dynamicRoundData = base.AccessOrdersConfig.m_roundData as DynamicRoundData;
		dynamicRoundData.MoveToNextPhase(base.AccessRoundInstanceData);
		ResetOrderTimer();
	}

	public bool HasMorePhases()
	{
		DynamicRoundData dynamicRoundData = base.AccessOrdersConfig.m_roundData as DynamicRoundData;
		return dynamicRoundData.GetRemainingPhases(base.AccessRoundInstanceData) > 0;
	}

	public float GetCurrentPhaseDuration()
	{
		DynamicRoundData dynamicRoundData = base.AccessOrdersConfig.m_roundData as DynamicRoundData;
		return dynamicRoundData.GetCurrentPhaseDuration(base.AccessRoundInstanceData);
	}
}
