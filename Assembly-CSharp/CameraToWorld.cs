using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraToWorld : MonoBehaviour
{
	private Camera myCamera;

	private void Start()
	{
		myCamera = GetComponent<Camera>();
	}

	private void OnPreCull()
	{
		Shader.SetGlobalMatrix("_Camera2World", myCamera.cameraToWorldMatrix);
	}
}
