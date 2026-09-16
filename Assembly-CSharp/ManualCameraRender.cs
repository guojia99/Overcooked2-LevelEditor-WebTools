using UnityEngine;

[RequireComponent(typeof(Camera))]
public class ManualCameraRender : MonoBehaviour
{
	[SerializeField]
	[AssignComponent(Editorbility.NonEditable)]
	private Camera m_camera;

	private void Awake()
	{
		m_camera.enabled = false;
		m_camera.Render();
	}

	private void CanvasRender()
	{
		m_camera.Render();
	}
}
