using UnityEngine;

[AddComponentMenu("Scripts/Core/Components/InheritFromModelPrefab")]
public class InheritFromModelPrefab : MonoBehaviour
{
	[SerializeField]
	private GameObject m_prefab;

	public GameObject GetParentPrefab()
	{
		return m_prefab;
	}
}
