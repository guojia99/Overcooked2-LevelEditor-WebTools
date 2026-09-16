using UnityEngine;

public class TriggerCounter : MonoBehaviour
{
	[SerializeField]
	public string m_inputTrigger;

	[SerializeField]
	public string m_outputTrigger;

	[SerializeField]
	public int m_count = 1;

	[SerializeField]
	public bool m_ResetOnCountReached = true;
}
