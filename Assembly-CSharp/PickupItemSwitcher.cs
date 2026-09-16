using UnityEngine;

[RequireComponent(typeof(PickupItemSpawner))]
public class PickupItemSwitcher : MonoBehaviour
{
	[SerializeField]
	public GameObject[] m_itemPrefabs;

	[SerializeField]
	public string m_switchTrigger;
}
