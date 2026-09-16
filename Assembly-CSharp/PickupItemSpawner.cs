using UnityEngine;

[RequireComponent(typeof(HandlePickupReferral))]
public class PickupItemSpawner : MonoBehaviour
{
	[SerializeField]
	public GameObject m_itemPrefab;

	[SerializeField]
	public int m_pickupPriority;

	[SerializeField]
	public int m_spawnCost = 1;
}
