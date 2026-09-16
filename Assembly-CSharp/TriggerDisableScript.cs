using UnityEngine;

public class TriggerDisableScript : MonoBehaviour
{
	[SerializeField]
	public Behaviour m_script;

	[SerializeField]
	public string m_enableTrigger;

	[SerializeField]
	public string m_disableTrigger;

	[SerializeField]
	public bool m_startEnabled = true;
}
