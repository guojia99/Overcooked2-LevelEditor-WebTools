using UnityEngine;

public class FireHazardSpawner : MonoBehaviour
{
	[SerializeField]
	public GameObject m_hazardPrefab;

	[SerializeField]
	public string m_spawnTrigger;

	[SerializeField]
	public bool m_alignToGrid = true;
}
