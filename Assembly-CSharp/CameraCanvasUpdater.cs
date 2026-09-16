using System;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Camera))]
public class CameraCanvasUpdater : MonoBehaviour
{
	[SerializeField]
	[AssignComponent(Editorbility.NonEditable)]
	private Camera m_camera;

	private Canvas[] m_allSceneCanvases;

	private CanvasScaler[] m_allSceneCanvasScalers;

	private Vector2[] m_referenceResolutions;

	private void Awake()
	{
		GameObject[] array = GameObject.FindGameObjectsWithTag("Canvas");
		array = array.AllRemoved_Predicate((GameObject x) => x.RequestComponent<CanvasCameraAssignment>() != null);
		m_allSceneCanvases = array.ConvertAll(GameObjectUtils.RequireComponent<Canvas>);
		m_allSceneCanvasScalers = array.ConvertAll(GameObjectUtils.RequireComponent<CanvasScaler>);
		m_referenceResolutions = m_allSceneCanvasScalers.ConvertAll((CanvasScaler x) => x.referenceResolution);
		Camera.onPreRender = (Camera.CameraCallback)Delegate.Combine(Camera.onPreRender, new Camera.CameraCallback(OnPreRenderCamera));
		for (int num = 0; num < m_allSceneCanvases.Length; num++)
		{
			if (m_allSceneCanvases[num] != null)
			{
				m_allSceneCanvases[num].worldCamera = m_camera;
			}
		}
	}

	private void OnDestroy()
	{
		Camera.onPreRender = (Camera.CameraCallback)Delegate.Remove(Camera.onPreRender, new Camera.CameraCallback(OnPreRenderCamera));
		for (int i = 0; i < m_allSceneCanvases.Length; i++)
		{
			m_allSceneCanvasScalers[i].referenceResolution = m_referenceResolutions[i];
		}
	}

	private void OnPreRenderCamera(Camera _camera)
	{
		if (!(_camera == m_camera))
		{
			return;
		}
		for (int i = 0; i < m_allSceneCanvases.Length; i++)
		{
			if (m_allSceneCanvases[i] != null)
			{
				m_allSceneCanvases[i].worldCamera = m_camera;
				Vector2 referenceResolution = m_referenceResolutions[i].DividedBy(m_camera.rect.size);
				m_allSceneCanvasScalers[i].referenceResolution = referenceResolution;
			}
		}
	}
}
