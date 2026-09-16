using UnityEngine;

public class CampaignFlowController : KitchenFlowControllerBase
{
	public abstract class OutroFlowroutine : FlowroutineComponent<OutroData>
	{
	}

	public class OutroData
	{
		public object ScoreData;

		public int Points;

		public int StarsAwarded;

		public GameProgress.UnlockData[] Unlocks;

		public OutroData(object _scoreData, int _points, int _starsAwarded, GameProgress.UnlockData[] _unlocks)
		{
			ScoreData = _scoreData;
			Points = _points;
			StarsAwarded = _starsAwarded;
			Unlocks = _unlocks;
		}
	}

	public interface IOutroFlowSceneProvider
	{
		string GetNextScene(out GameState o_loadState, out GameState o_loadEndState, out bool o_useLoadingScreen);
	}

	[SerializeField]
	public TeamMonitor m_teamMonitor = new TeamMonitor();
}
