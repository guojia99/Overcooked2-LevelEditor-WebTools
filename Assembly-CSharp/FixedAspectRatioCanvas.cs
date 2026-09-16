using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Canvas))]
public class FixedAspectRatioCanvas : MonoBehaviour
{
	[SerializeField]
	[AssignResource("FixedAspectRootCanvas", Editorbility.NonEditable)]
	private GameObject m_rootCanvasPrefab;

	private RectTransform m_transform;

	private Canvas m_canvas;

	private RectTransform m_rootCanvasTransform;

	private FixedAspectRatioManager m_ratioManager;

	private void Awake()
	{
		m_transform = base.transform as RectTransform;
		m_canvas = base.gameObject.RequireComponent<Canvas>();
	}

	private void Start()
	{
		if (!(m_canvas == null) && m_canvas.renderMode == RenderMode.ScreenSpaceOverlay)
		{
			EnsureRootCanvas();
			FixUpCanvas();
			m_ratioManager = GameUtils.RequestManager<FixedAspectRatioManager>();
			if (m_ratioManager != null)
			{
				m_ratioManager.RegisterOnResolutionChanged(OnResolutionChanged);
				m_ratioManager.InitialiseComponent(OnResolutionChanged);
				return;
			}
			Vector2 defaultAspect = FixedAspectRatio.DefaultAspect;
			Rect rect = FixedAspectRatio.ComputeAspectRatioRect(defaultAspect.x, defaultAspect.y, Screen.width, Screen.height);
			m_transform.anchorMin = new Vector2(rect.xMin, rect.yMin);
			m_transform.anchorMax = new Vector2(rect.xMax, rect.yMax);
		}
	}

	private void OnDestroy()
	{
		if (m_ratioManager != null)
		{
			m_ratioManager.UnregisterOnResolutionChanged(OnResolutionChanged);
		}
	}

	private void EnsureRootCanvas()
	{
		Transform transform = m_transform.FindParentRecursive("FixedAspectRootCanvas");
		if (transform == null)
		{
			if (m_transform.parent != null)
			{
				transform = m_transform.parent.Find("FixedAspectRootCanvas");
			}
			if (transform == null)
			{
				GameObject gameObject = m_rootCanvasPrefab.InstantiateOnParent(m_transform.parent);
				transform = gameObject.transform;
			}
		}
		m_rootCanvasTransform = transform as RectTransform;
	}

	private void FixUpCanvas()
	{
		CanvasScaler canvasScaler = base.gameObject.RequestComponent<CanvasScaler>();
		if (canvasScaler != null)
		{
			Object.Destroy(canvasScaler);
		}
		m_transform.SetParent(m_rootCanvasTransform, false);
		m_transform.anchorMin = new Vector2(0f, 0f);
		m_transform.anchorMax = new Vector2(1f, 1f);
		m_transform.pivot = new Vector2(0.5f, 0.5f);
		m_transform.offsetMin = new Vector2(0f, 0f);
		m_transform.offsetMax = new Vector2(0f, 0f);
		m_transform.localScale = Vector3.one;
		m_canvas.overrideSorting = true;
	}

	private void OnResolutionChanged(Rect _rect, float _screenWidth, float _screenHeight)
	{
		m_transform.anchorMin = new Vector2(_rect.xMin, _rect.yMin);
		m_transform.anchorMax = new Vector2(_rect.xMax, _rect.yMax);
	}
}
