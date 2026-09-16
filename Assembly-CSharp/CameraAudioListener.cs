using UnityEngine;

[RequireComponent(typeof(AudioListener))]
[RequireComponent(typeof(Camera))]
public class CameraAudioListener : MonoBehaviour
{
	[SerializeField]
	[AssignComponent(Editorbility.NonEditable)]
	private AudioListener m_audiolistener;

	[SerializeField]
	[AssignComponent(Editorbility.NonEditable)]
	private Camera m_camera;

	private void OnEnable()
	{
		m_audiolistener.enabled = Camera.main == m_camera;
	}

	private void LateUpdate()
	{
		Camera main = Camera.main;
		if (main != null && main.enabled)
		{
			m_audiolistener.enabled = Camera.main == m_camera;
		}
		else
		{
			m_audiolistener.enabled = m_camera.enabled;
		}
	}

	private void OnDisable()
	{
		m_audiolistener.enabled = false;
	}
}
