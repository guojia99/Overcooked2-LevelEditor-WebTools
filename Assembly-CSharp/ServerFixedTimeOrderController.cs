using OrderController;

public class ServerFixedTimeOrderController : ServerOrderControllerBase
{
	public struct OrdersConfig
	{
		public int m_maxActiveOrders;

		public RoundDataBase m_roundData;

		public float m_orderLifetime;

		public float m_timeBetweenOrders;
	}

	private OrdersConfig m_ordersConfig;

	protected OrdersConfig AccessOrdersConfig
	{
		get
		{
			return m_ordersConfig;
		}
	}

	public ServerFixedTimeOrderController(ref OrdersConfig _ordersConfig, VoidGeneric<OrderID> _addedCallback, VoidGeneric<OrderID> _timeoutCallback)
		: base(_ordersConfig.m_roundData, _ordersConfig.m_maxActiveOrders, _addedCallback, _timeoutCallback)
	{
		m_ordersConfig = _ordersConfig;
	}

	protected override float GetNextOrderLifetime()
	{
		return m_ordersConfig.m_orderLifetime;
	}

	protected override float GetNextTimeBetweenOrders()
	{
		return m_ordersConfig.m_timeBetweenOrders;
	}
}
