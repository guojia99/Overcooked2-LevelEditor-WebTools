using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(RectTransform))]
public class RectTransformExtension : MonoBehaviour
{
	public enum ScalingBehaviour
	{
		DontPreserve = 0,
		PreserveHorizontalAnchors = 1,
		PreserveVerticalAnchors = 2
	}

	[SerializeField]
	private ScalingBehaviour m_scalingBehaviour = ScalingBehaviour.PreserveVerticalAnchors;

	[SerializeField]
	private Vector2 m_scalePivot = Vector2.zero;

	[SerializeField]
	private Vector2 m_fixedRatioAnchorMin = Vector2.zero;

	[SerializeField]
	private Vector2 m_fixedRatioAnchorMax = Vector2.one;

	[SerializeField]
	private Vector3 m_rotation = Vector3.zero;

	[SerializeField]
	private Vector2 m_rotationPivot = Vector2.zero;

	[SerializeField]
	private Vector2 m_scale = new Vector2(1f, 1f);

	private DrivenRectTransformTracker m_rectDriver = default(DrivenRectTransformTracker);

	private RectTransform m_rectTransform;

	private Vector2 m_anchorOffset = Vector2.zero;

	private Vector2 m_pixelOffset = Vector2.zero;

	public Vector2 AnchorOffset
	{
		get
		{
			return m_anchorOffset;
		}
		set
		{
			m_anchorOffset = value;
			UpdateAspectRatio();
		}
	}

	public Vector2 PixelOffset
	{
		get
		{
			return m_pixelOffset;
		}
		set
		{
			m_pixelOffset = value;
			UpdateAspectRatio();
		}
	}

	public Vector2 anchorMin
	{
		get
		{
			return m_rectTransform.anchorMin;
		}
	}

	public Vector2 anchorMax
	{
		get
		{
			return m_rectTransform.anchorMax;
		}
	}

	public Vector2 offsetMin
	{
		get
		{
			return m_rectTransform.offsetMin;
		}
	}

	public Vector2 offsetMax
	{
		get
		{
			return m_rectTransform.offsetMax;
		}
	}

	public Vector3 rotation
	{
		get
		{
			return m_rotation;
		}
		set
		{
			m_rotation = value;
		}
	}

	public Vector2 scale
	{
		get
		{
			return m_scale;
		}
		set
		{
			m_scale = value;
		}
	}

	private void Awake()
	{
		UpdateAspectRatio();
	}

	private void Setup()
	{
		if (m_rectTransform == null)
		{
			m_rectTransform = base.gameObject.RequireComponent<RectTransform>();
			m_rectTransform.hideFlags = HideFlags.None;
			m_rectDriver.Add(this, m_rectTransform, DrivenTransformProperties.All);
		}
	}

	private void LateUpdate()
	{
		UpdateAspectRatio();
	}

	private void UpdateAspectRatio()
	{
		Setup();
		if (!(Camera.main == null))
		{
			float aspect = Camera.main.aspect;
			m_rectTransform.offsetMin = m_pixelOffset;
			m_rectTransform.offsetMax = m_pixelOffset;
			m_rectTransform.localScale = Vector2.one;
			m_rectTransform.anchorMin = m_anchorOffset + ConvertFixedRatioCoordToReal(m_fixedRatioAnchorMin, aspect, m_scalingBehaviour);
			m_rectTransform.anchorMax = m_anchorOffset + ConvertFixedRatioCoordToReal(m_fixedRatioAnchorMax, aspect, m_scalingBehaviour);
			m_rectTransform.localRotation = Quaternion.Euler(m_rotation);
			m_rectTransform.pivot = m_rotationPivot;
		}
	}

	private Vector2 GetScale(float _aspect, ScalingBehaviour _scalingBehaviour)
	{
		switch (_scalingBehaviour)
		{
		case ScalingBehaviour.PreserveHorizontalAnchors:
			return new Vector2(m_scale.x, m_scale.y * _aspect);
		case ScalingBehaviour.PreserveVerticalAnchors:
			return new Vector2(m_scale.x / _aspect, m_scale.y);
		default:
			return new Vector3(m_scale.x, m_scale.y);
		}
	}

	private Vector2 GetFixedRatioCoord(Vector2 _pos, float _aspect, ScalingBehaviour _scalingBehaviour)
	{
		Vector2 vector = GetScale(_aspect, _scalingBehaviour);
		return new Vector2((_pos.x - m_scalePivot.x) / vector.x + m_scalePivot.x, (_pos.y - m_scalePivot.y) / vector.y + m_scalePivot.y);
	}

	private Vector2 ConvertFixedRatioCoordToReal(Vector2 _fixedRatio, float _aspect, ScalingBehaviour _scalingBehaviour)
	{
		Vector2 vector = GetScale(_aspect, _scalingBehaviour);
		return new Vector2((_fixedRatio.x - m_scalePivot.x) * vector.x + m_scalePivot.x, (_fixedRatio.y - m_scalePivot.y) * vector.y + m_scalePivot.y);
	}
}
