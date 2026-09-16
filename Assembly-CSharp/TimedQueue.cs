using UnityEngine;

public abstract class TimedQueue : MonoBehaviour
{
	[SerializeField]
	public string m_startTrigger;

	[SerializeField]
	public string m_cancelTrigger;

	[SerializeField]
	public string m_endTrigger;

	[SerializeField]
	public GameObject m_endTriggerTarget;

	[SerializeField]
	public bool m_startOnAwake;

	[Space]
	[SerializeField]
	public bool m_loopWhenFinished;

	[SerializeField]
	public float m_loopDelay;

	public abstract float GetQueueLength();

	public abstract float GetDelay(int _index);
}
