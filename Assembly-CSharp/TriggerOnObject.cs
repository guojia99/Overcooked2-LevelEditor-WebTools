using UnityEngine;

public class TriggerOnObject : MonoBehaviour
{
	[SerializeField]
	public string m_trigger;

	[SerializeField]
	public string m_triggerToFire;

	[SerializeField]
	public GameObject m_targetObject;

	[SerializeField]
	public GameObject[] m_targetObjects;
}
