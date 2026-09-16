using OrderController;

public class ServerTeamMonitor
{
	private TeamMonitor m_monitor;

	private ServerOrderControllerBase m_ordersController;

	private TeamMonitor.TeamScoreStats m_score = new TeamMonitor.TeamScoreStats();

	public TeamMonitor.TeamScoreStats Score
	{
		get
		{
			return m_score;
		}
	}

	public ServerOrderControllerBase OrdersController
	{
		get
		{
			return m_ordersController;
		}
	}

	public virtual void Update()
	{
		m_ordersController.Update();
	}

	public virtual void Initialise(TeamMonitor _monitor, ServerOrderControllerBuilder _controllerBuilder, VoidGeneric<OrderID> _addedCallback, VoidGeneric<OrderID> _timeoutCallback)
	{
		m_monitor = _monitor;
		m_ordersController = _controllerBuilder(_addedCallback, _timeoutCallback);
	}

	public virtual bool OnFoodDelivered(AssembledDefinitionNode _definition, PlatingStepData _plateType, out OrderID o_orderID, out RecipeList.Entry o_entry, out float o_timePropRemainingPercentage, out bool o_wasCombo)
	{
		o_orderID = default(OrderID);
		o_entry = null;
		o_timePropRemainingPercentage = 0f;
		o_wasCombo = false;
		if (m_ordersController.FindBestOrderForRecipe(_definition, _plateType, out o_orderID, out o_timePropRemainingPercentage))
		{
			o_entry = m_ordersController.GetRecipe(o_orderID);
			bool restart = m_score.TotalCombo == 0;
			o_wasCombo = m_ordersController.IsComboOrder(o_orderID, restart);
			m_ordersController.RemoveOrder(o_orderID);
			return true;
		}
		return false;
	}
}
