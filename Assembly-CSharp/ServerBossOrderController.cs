using OrderController;

public class ServerBossOrderController : ServerDynamicOrderController
{
	public ServerBossOrderController(ref OrdersConfig _ordersConfig, VoidGeneric<OrderID> _addedCallback, VoidGeneric<OrderID> _timeoutCallback)
		: base(ref _ordersConfig, _addedCallback, _timeoutCallback)
	{
	}

	public bool CurrentPhaseComplete()
	{
		BossRoundData bossRoundData = base.AccessOrdersConfig.m_roundData as BossRoundData;
		return bossRoundData.NoMoreRecipesToIssueInPhase(base.AccessRoundInstanceData) && IsEmpty();
	}

	public bool HasFinished()
	{
		return CurrentPhaseComplete() && !HasMorePhases();
	}
}
