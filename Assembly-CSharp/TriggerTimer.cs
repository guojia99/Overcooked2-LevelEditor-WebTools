using UnityEngine;

[AddComponentMenu("Scripts/Core/Components/TriggerTimer")]
public class TriggerTimer : MonoBehaviour
{
	[SerializeField]
	public string m_startTrigger;

	[SerializeField]
	public string m_completeTrigger;

	[SerializeField]
	public float m_time;

	[SerializeField]
	public bool m_startTiming;

	[SerializeField]
	public bool m_triggerAtStart;
}
