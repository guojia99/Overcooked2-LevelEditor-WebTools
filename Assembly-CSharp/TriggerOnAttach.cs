using UnityEngine;

[RequireComponent(typeof(AttachStation))]
public class TriggerOnAttach : MonoBehaviour
{
	[SerializeField]
	public string m_attachTrigger;

	[SerializeField]
	public string m_detachTrigger;

	[SerializeField]
	public GameObject m_triggerTarget;
}
