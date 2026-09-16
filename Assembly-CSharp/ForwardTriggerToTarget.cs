using UnityEngine;

public class ForwardTriggerToTarget : MonoBehaviour
{
	[SerializeField]
	private GameObject m_target;

	[SerializeField]
	private string m_inputTrigger;

	[SerializeField]
	private string m_outputTrigger;

	private void OnTrigger(string _trigger)
	{
		if (m_inputTrigger == _trigger)
		{
			m_target.SendMessage("OnTrigger", m_outputTrigger, SendMessageOptions.DontRequireReceiver);
		}
	}
}
