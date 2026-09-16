using UnityEngine;

public class CompetitiveFlowController : KitchenFlowControllerBase
{
	public abstract class OutroFlowroutine : FlowroutineComponent<OutroData>
	{
	}

	public class OutroData
	{
		public object ScoreData;

		public OutroData(object _scoreData)
		{
			ScoreData = _scoreData;
		}
	}

	[SerializeField]
	[AssignComponent(Visibility.Show)]
	public OutroFlowroutine m_outroFlowroutine;

	[SerializeField]
	public TeamMonitor m_teamOneData;

	[SerializeField]
	public TeamMonitor m_teamTwoData;
}
