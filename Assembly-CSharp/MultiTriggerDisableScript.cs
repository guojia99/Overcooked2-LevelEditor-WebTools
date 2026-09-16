using UnityEngine;

public class MultiTriggerDisableScript : MonoBehaviour
{
	[SerializeField]
	public Behaviour m_script;

	[SerializeField]
	public TriggerPair[] m_triggers = new TriggerPair[0];

	[SerializeField]
	public bool m_startEnabled = true;
}
