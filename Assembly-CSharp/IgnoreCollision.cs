using UnityEngine;

public class IgnoreCollision : MonoBehaviour
{
	[SerializeField]
	private GameObject m_target;

	private void Awake()
	{
		Collider[] array = m_target.RequestComponentsRecursive<Collider>();
		Collider[] array2 = base.gameObject.RequestComponentsRecursive<Collider>();
		for (int i = 0; i < array.Length; i++)
		{
			for (int j = 0; j < array2.Length; j++)
			{
				Physics.IgnoreCollision(array[i], array2[j]);
			}
		}
	}
}
