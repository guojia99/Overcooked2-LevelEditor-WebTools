using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(GridLayoutGroup))]
public class GridElementSnap : MonoBehaviour
{
	private GridLayoutGroup m_GridLayout;

	private RectTransform m_RectTransform;

	private bool m_bButtonScrolling;

	private bool m_bScrollingLeft;

	private bool m_bScrollingRight;

	private bool m_bButtonScrollThisFrame;

	private bool m_bButtonScrollJustReleased;

	private Vector3 m_OriginSnappedPosition = Vector3.zero;

	private Vector3 m_TargetSnapPosition = Vector3.zero;

	public float m_ScrollSpeed = 4.5f;

	public float m_NoControlRestingSpeed = 8f;

	public float m_SnapStartVelocity = 100f;

	private void Awake()
	{
		m_GridLayout = GetComponent<GridLayoutGroup>();
		m_RectTransform = GetComponent<RectTransform>();
	}

	private void Update()
	{
		if (!m_bButtonScrolling)
		{
			return;
		}
		if (m_bButtonScrollThisFrame)
		{
			Vector3 vector = Vector3.zero;
			if (m_bScrollingLeft)
			{
				vector = new Vector3(0f - GetCellWidth(), 0f, 0f);
			}
			else if (m_bScrollingRight)
			{
				vector = new Vector3(GetCellWidth(), 0f, 0f);
			}
			Vector3 vector2 = Vector3.Lerp(m_RectTransform.localPosition, m_RectTransform.localPosition + vector, Mathf.Min(1f, Time.deltaTime * m_ScrollSpeed)) - m_RectTransform.localPosition;
			m_RectTransform.localPosition += vector2;
			m_bButtonScrollThisFrame = false;
			m_bButtonScrollJustReleased = true;
			return;
		}
		if (m_bButtonScrollJustReleased)
		{
			ConsiderSnapToCurrentPosition();
			m_bButtonScrollJustReleased = false;
		}
		m_RectTransform.localPosition = Vector3.Lerp(m_RectTransform.localPosition, m_TargetSnapPosition, Mathf.Min(1f, Time.deltaTime * m_NoControlRestingSpeed));
		if (Vector3.Distance(m_RectTransform.localPosition, m_TargetSnapPosition) < 1f)
		{
			m_RectTransform.localPosition = m_TargetSnapPosition;
			m_bButtonScrolling = false;
			m_bScrollingLeft = false;
			m_bScrollingRight = false;
			m_OriginSnappedPosition = m_TargetSnapPosition;
			m_TargetSnapPosition = Vector3.zero;
		}
	}

	public void ConsiderSnapToCurrentPosition()
	{
		Vector3 vector = CalculateCurrentSnappedPosition();
		if (vector != m_OriginSnappedPosition && m_TargetSnapPosition != vector)
		{
			m_TargetSnapPosition = vector;
		}
	}

	public void SnapToNearest(ScrollRect scrollingRect)
	{
		bool flag = scrollingRect.velocity.x < 0f;
		m_TargetSnapPosition.x = CalculateCurrentSnappedPosition().x;
		m_bButtonScrolling = true;
		m_bScrollingLeft = flag;
		m_bScrollingRight = !flag;
		m_bButtonScrollThisFrame = true;
		m_bButtonScrollJustReleased = false;
		scrollingRect.velocity = Vector2.zero;
		scrollingRect = null;
	}

	public Vector3 CalculateCurrentSnappedPosition()
	{
		float cellWidth = GetCellWidth();
		float num = cellWidth / 2f;
		float num2 = m_RectTransform.localPosition.x - num;
		num2 = Mathf.Round(num2 / cellWidth) * cellWidth;
		num2 += num;
		return new Vector3(num2, 0f, 0f);
	}

	public float GetCellWidth()
	{
		return m_GridLayout.cellSize.x + m_GridLayout.spacing.x;
	}

	public void ForceTargetPosition(Vector3 position)
	{
		m_OriginSnappedPosition = position;
		m_RectTransform.localPosition = m_OriginSnappedPosition;
	}

	public void SetTargetElement(float xDif)
	{
		if (!m_bButtonScrolling)
		{
			Vector3 vector = CalculateCurrentSnappedPosition();
			m_TargetSnapPosition.x = vector.x + xDif;
			m_bButtonScrolling = true;
			m_bScrollingLeft = xDif > 0f;
			m_bScrollingRight = !(xDif > 0f);
			m_bButtonScrollThisFrame = true;
			m_bButtonScrollJustReleased = false;
		}
	}

	public void ButtonScroll(bool scrollLeft, bool isNewInput)
	{
		bool flag = (m_bButtonScrolling && m_bScrollingLeft != scrollLeft) || isNewInput;
		if (!m_bButtonScrolling || flag)
		{
			m_OriginSnappedPosition = CalculateCurrentSnappedPosition();
			if (scrollLeft)
			{
				m_TargetSnapPosition.x = m_OriginSnappedPosition.x - GetCellWidth();
			}
			else
			{
				m_TargetSnapPosition.x = m_OriginSnappedPosition.x + GetCellWidth();
			}
		}
		m_bButtonScrolling = true;
		m_bScrollingLeft = scrollLeft;
		m_bScrollingRight = !scrollLeft;
		m_bButtonScrollThisFrame = true;
		m_bButtonScrollJustReleased = false;
	}
}
