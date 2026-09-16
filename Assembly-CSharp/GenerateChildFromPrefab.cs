using UnityEngine;

[ExecuteInEditMode]
public class GenerateChildFromPrefab : MonoBehaviour
{
	[SerializeField]
	private GameObject m_childPrefab;

	[SerializeField]
	[HideInInspector]
	private GameObject m_childInstance;

	private void OnEnable()
	{
		if (m_childPrefab != null && m_childInstance == null)
		{
			m_childInstance = m_childPrefab.InstantiateOnParent(base.transform);
		}
	}

	private void OnDisable()
	{
		if (m_childInstance != null && !Application.isPlaying)
		{
			Object.DestroyImmediate(m_childInstance);
		}
	}
}
