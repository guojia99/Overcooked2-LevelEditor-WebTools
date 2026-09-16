using UnityEngine;

[ExecuteInEditMode]
public class LateUpdateScale : MonoBehaviour
{
	[SerializeField]
	private Vector3 m_localScale = Vector3.one;

	private void OnValidate()
	{
		m_localScale.x = Mathf.Max(m_localScale.x, 0.1f);
		m_localScale.y = Mathf.Max(m_localScale.y, 0.1f);
		m_localScale.z = Mathf.Max(m_localScale.z, 0.1f);
	}

	private void LateUpdate()
	{
		base.transform.localScale = m_localScale;
	}
}
