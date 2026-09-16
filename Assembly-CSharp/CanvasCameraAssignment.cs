using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class CanvasCameraAssignment : MonoBehaviour
{
	[SerializeField]
	[AssignComponent(Editorbility.NonEditable)]
	private Canvas m_canvas;

	[SerializeField]
	private string m_camera;

	private void Awake()
	{
		SetCamera(m_camera);
	}

	private void OnEnable()
	{
		SetCameraEnabled(true);
	}

	private void OnDisable()
	{
		SetCameraEnabled(false);
	}

	private void SetCameraEnabled(bool _enabled)
	{
	}

	public void SetCamera(string _camera)
	{
		m_camera = _camera;
		GameObject gameObject = GameObject.Find(m_camera);
		if (gameObject != null)
		{
			Camera worldCamera = gameObject.RequireComponent<Camera>();
			m_canvas.worldCamera = worldCamera;
			SetCameraEnabled(true);
		}
	}
}
