using UnityEngine;

public class Splattable : MonoBehaviour
{
	[SerializeField]
	public GameObject[] m_splatPrefab;

	[SerializeField]
	public bool m_alignToGrid = true;

	public int m_prefabIndex;

	private void Awake()
	{
		Object.Destroy(this);
	}
}
