using UnityEngine;

[RequireComponent(typeof(Interactable))]
public class HandlePickupAsInteraction : MonoBehaviour
{
	[SerializeField]
	private int m_pickupPriority;
}
