using UnityEngine;

[RequireComponent(typeof(Camera))]
[ExecuteInEditMode]
public class OrthoCameraResizer : MonoBehaviour
{
	protected Camera m_camera;

	[SerializeField]
	protected RectTransform m_resizableRect;

	public Vector2 TargetDimensions
	{
		get
		{
			if (m_resizableRect != null)
			{
				return m_resizableRect.GetSize();
			}
			return new Vector2(Screen.width, Screen.height);
		}
	}

	private void Start()
	{
		m_camera = base.gameObject.RequireComponent<Camera>();
	}

	private void Update()
	{
		if (m_resizableRect != null)
		{
			Vector3 size = m_resizableRect.GetSize();
			m_camera.orthographicSize = size.x * 0.5f / (size.x / size.y);
		}
		else
		{
			m_camera.orthographicSize = (float)Screen.width * 0.5f / m_camera.aspect;
		}
	}
}
