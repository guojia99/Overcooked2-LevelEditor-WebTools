using UnityEngine;

[ExecuteInEditMode]
public class FaceCamera : MonoBehaviour
{
	private Camera m_camera;

	private void Start()
	{
		FaceTheCamera();
	}

	[ContextMenu("Face Camera")]
	private void FaceTheCamera()
	{
		if (m_camera == null)
		{
			m_camera = Camera.main;
		}
		base.transform.LookAt(base.transform.position + m_camera.transform.forward, Vector3.up);
	}
}
