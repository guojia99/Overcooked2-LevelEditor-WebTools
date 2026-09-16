using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Canvas))]
public class FixedAspectRatioRootCanvas : MonoBehaviour
{
	private CanvasScaler m_canvasScaler;

	private FixedAspectRatioManager m_ratioManager;

	private void Awake()
	{
		m_canvasScaler = base.gameObject.RequireComponent<CanvasScaler>();
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
			Rect rect = FixedAspectRatio.ComputeAspectRatioRect(defaultAspect.x, defaultAspect.y, Screen.width, Screen.height);
			m_canvasScaler.matchWidthOrHeight = ((!IsWiderThanDesiredAspect(rect)) ? 0f : 1f);
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
		m_canvasScaler.matchWidthOrHeight = ((!IsWiderThanDesiredAspect(_rect)) ? 0f : 1f);
	}

	private bool IsWiderThanDesiredAspect(Rect _rect)
	{
		return _rect.width < 1f;
	}
}
