using UnityEngine;
using UnityEngine.UI;

[ExecutionDependency(typeof(RectTransformExtension))]
[RequireComponent(typeof(UI_Move))]
public class HoverIconUIController : UIControllerBase
{
	[SerializeField]
	protected Vector2 m_anchorOffset;

	public bool ShouldFollow = true;

	private Transform m_followTransform;

	private Vector3 m_followOffset = Vector3.zero;

	protected RectTransform m_rectTransform;

	private UI_Move m_uiMove;

	private RectTransform m_canvasRect;

	public Vector2 GetOffset()
	{
		return m_anchorOffset;
	}

	public Transform GetFollowTransform()
	{
		return m_followTransform;
	}

	public void SetAnchorOffset(Vector2 _anchorOffset)
	{
		m_anchorOffset = _anchorOffset;
	}

	public void SetFollowOffset(Vector3 _followOffset)
	{
		m_followOffset = _followOffset;
		UpdateFollow();
	}

	public void SetFollowTransform(Transform _followTransform)
	{
		m_followTransform = _followTransform;
		UpdateFollow();
	}

	public void SetFollowTransform(Transform _followTransform, Vector3 _followOffset)
	{
		m_followTransform = _followTransform;
		m_followOffset = _followOffset;
		UpdateFollow();
	}

	public void SetVisibility(bool _value)
	{
		if (base.gameObject != null)
		{
			ILayoutElement[] array = base.gameObject.RequestInterfacesRecursive<ILayoutElement>();
			for (int i = 0; i < array.Length; i++)
			{
				(array[i] as MonoBehaviour).enabled = _value;
			}
		}
	}

	protected virtual void Awake()
	{
		m_rectTransform = base.transform as RectTransform;
		m_uiMove = base.gameObject.RequireComponent<UI_Move>();
	}

	protected virtual void OnEnable()
	{
		UpdateFollow();
	}

	public virtual void LateUpdate()
	{
		UpdateFollow();
	}

	private void UpdateFollow()
	{
		if (!ShouldFollow)
		{
			return;
		}
		if (m_canvasRect == null)
		{
			Canvas canvas = base.gameObject.RequestComponentUpwardsRecursive<Canvas>();
			if (canvas != null)
			{
				m_canvasRect = canvas.transform as RectTransform;
			}
		}
		if (m_canvasRect != null && m_uiMove != null)
		{
			Vector2 screenPos = GetScreenPos();
			Vector2 size = m_canvasRect.rect.size;
			Vector2 offset = new Vector2((screenPos.x + m_anchorOffset.x) * size.x, (screenPos.y + m_anchorOffset.y) * size.y);
			m_uiMove.Offset = offset;
		}
	}

	private Vector2 GetScreenPos()
	{
		if (Camera.main == null)
		{
			return Vector2.zero;
		}
		if (m_followTransform == null)
		{
			return m_followOffset;
		}
		return Camera.main.WorldToViewportPoint(m_followTransform.TransformPoint(m_followOffset));
	}
}
