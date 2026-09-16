using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TriggerZone : MonoBehaviour
{
	[SerializeField]
	public string m_onOccupationTrigger;

	[SerializeField]
	public string m_onDeoccupationTrigger;

	[SerializeField]
	public bool m_fallPad;
}
