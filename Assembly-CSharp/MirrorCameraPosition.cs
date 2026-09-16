using UnityEngine;

[ExecuteInEditMode]
public class MirrorCameraPosition : MonoBehaviour
{
	[SerializeField]
	private Transform m_mirrorTransform;

	private void Awake()
	{
		LateUpdate();
	}

	private void LateUpdate()
	{
		if (m_mirrorTransform != null)
		{
			Vector3 position = Camera.main.transform.position;
			Vector3 lhs = position - m_mirrorTransform.position;
			Vector3 up = m_mirrorTransform.up;
			base.transform.position = position - 2f * Vector3.Dot(lhs, up) * up;
		}
	}
}
