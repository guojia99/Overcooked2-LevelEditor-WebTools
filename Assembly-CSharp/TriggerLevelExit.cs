using UnityEngine;

public class TriggerLevelExit : MonoBehaviour, ITriggerReceiver
{
	[SerializeField]
	private string m_triggerToExit;

	public void OnTrigger(string _trigger)
	{
		if (m_triggerToExit == _trigger)
		{
			EndLevel();
		}
	}

	private void EndLevel()
	{
		IServerFlowController serverFlowController = GameUtils.RequestManagerInterface<IServerFlowController>();
		if (serverFlowController != null)
		{
			serverFlowController.SkipToEnd();
		}
	}
}
