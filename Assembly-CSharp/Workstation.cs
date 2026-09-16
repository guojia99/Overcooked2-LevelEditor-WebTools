using UnityEngine;

[AddComponentMenu("Scripts/Game/Environment/Workstation")]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(AttachStation))]
[RequireComponent(typeof(Interactable))]
public class Workstation : MonoBehaviour
{
	[SerializeField]
	public string m_chopAnimState = "Chop";

	[SerializeField]
	public string m_chopTrigger = "Impact";

	[SerializeField]
	public GameObject m_chopPFX;
}
