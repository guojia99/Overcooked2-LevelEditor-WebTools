using UnityEngine;

public class BoundContainer : MonoBehaviour
{
	[SerializeField]
	public Vector3 m_boundSize;

	private void OnDrawGizmosSelected()
	{
		Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
		Gizmos.DrawCube(base.transform.position, m_boundSize);
	}
}
