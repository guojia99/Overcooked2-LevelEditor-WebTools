using UnityEngine;

[RequireComponent(typeof(Camera))]
public class FixedAspectRatioCamera : MonoBehaviour
{
	private FixedAspectRatioManager m_ratioManager;

	private Camera m_camera;

	private void Awake()
	{
		m_camera = base.gameObject.RequireComponent<Camera>();
	}

	private void Start()
	{
		m_ratioManager = GameUtils.RequestManager<FixedAspectRatioManager>();
		if (m_ratioManager != null)
		{
			m_ratioManager.RegisterOnResolutionChanged(OnResolutionChanged);
			m_ratioManager.InitialiseComponent(OnResolutionChanged);
		}
		else
		{
			Vector2 defaultAspect = FixedAspectRatio.DefaultAspect;
			m_camera.rect = FixedAspectRatio.ComputeAspectRatioRect(defaultAspect.x, defaultAspect.y, Screen.width, Screen.height);
		}
	}

	private void OnDestroy()
	{
		if (m_ratioManager != null)
		{
			m_ratioManager.UnregisterOnResolutionChanged(OnResolutionChanged);
		}
	}

	private void OnResolutionChanged(Rect _rect, float _screenWidth, float _screenHeight)
	{
		m_camera.rect = _rect;
	}
}
