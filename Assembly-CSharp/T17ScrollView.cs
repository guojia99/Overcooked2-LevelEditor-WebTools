using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class T17ScrollView : T17NavigableGrid, IScrollHandler, IEventSystemHandler
{
	public RectTransform m_ViewPort;

	public T17Scrollbar m_VerticalScrollBar;

	public bool m_bScrollToSelectedElement = true;

	private Vector2 m_Spacing = Vector2.zero;

	private Vector3[] m_ViewCorners = new Vector3[4];

	private Vector3[] m_ElementCorners = new Vector3[4];

	private Vector3[] m_ContentCorners = new Vector3[4];

	private Bounds m_ContentBounds;

	private Bounds m_ViewBounds;

	private Vector2 m_OriginalPosition = Vector2.zero;

	private Vector2 m_DesiredPosition = Vector2.zero;

	private Vector2 m_PrevPosition = Vector2.zero;

	private Bounds m_PrevContentBounds;

	private Bounds m_PrevViewBounds;

	private float m_LerpTime;

	private const float LERP_STEP = 0.3f;

	private GameObject m_ScrollToItem;

	private bool m_bSelectScrollItem;

	private int m_ScrollTimer;

	private const int SCROLL_WAIT = 5;

	public float verticalNormalizedPosition
	{
		get
		{
			UpdateBounds();
			if (m_ContentBounds.size.y <= m_ViewBounds.size.y)
			{
				return (m_ViewBounds.min.y > m_ContentBounds.min.y) ? 1 : 0;
			}
			return (m_ViewBounds.min.y - m_ContentBounds.min.y) / (m_ContentBounds.size.y - m_ViewBounds.size.y);
		}
		set
		{
			SetNormalizedPosition(value, 1);
		}
	}

	protected override void SingleTimeInitialize()
	{
		base.SingleTimeInitialize();
		if (m_ContentParent != null)
		{
			if (!base.isAltLayout)
			{
				HorizontalOrVerticalLayoutGroup component = m_ContentParent.GetComponent<HorizontalOrVerticalLayoutGroup>();
				if (component != null)
				{
					m_Spacing.y = component.spacing;
				}
			}
			else
			{
				T17GridLayoutGroup component2 = m_ContentParent.GetComponent<T17GridLayoutGroup>();
				if (component2 != null)
				{
					m_Spacing = component2.spacing;
				}
			}
			m_DesiredPosition = m_ContentParent.localPosition;
			m_OriginalPosition = m_DesiredPosition;
		}
		if (m_VerticalScrollBar != null)
		{
			m_VerticalScrollBar.onValueChanged.AddListener(SetVerticalNormalizedPosition);
		}
	}

	protected override void Update()
	{
		base.Update();
		if (m_ContentParent != null && m_LerpTime > 0f)
		{
			m_LerpTime -= Time.unscaledDeltaTime;
			m_ContentParent.localPosition = Vector2.Lerp(m_OriginalPosition, m_DesiredPosition, (0.3f - m_LerpTime) / 0.3f);
		}
		if (m_ScrollToItem != null)
		{
			if (m_ScrollTimer <= 0)
			{
				InternalScrollToItem();
				m_ScrollToItem = null;
			}
			m_ScrollTimer--;
		}
	}

	public override bool Hide(bool restoreInvokerState = true, bool isTabSwitch = false)
	{
		if (!base.Hide(restoreInvokerState, isTabSwitch))
		{
			return false;
		}
		ResetPosition();
		return true;
	}

	public override bool Show(GamepadUser currentGamer, BaseMenuBehaviour parent, GameObject invoker, bool hideInvoker = true)
	{
		bool result = base.Show(currentGamer, parent, invoker, hideInvoker);
		if (m_VerticalScrollBar != null && m_CachedEventSystem != null)
		{
			m_VerticalScrollBar.SetEventSystem(m_CachedEventSystem);
		}
		LayoutRebuilder.ForceRebuildLayoutImmediate(m_ContentParent);
		ResetSelection(true);
		UpdateBounds();
		UpdateScrollbars(Vector2.zero);
		UpdatePrevData();
		return result;
	}

	public void ResetPosition()
	{
		m_CurrentSelected = 0;
		m_PreviousSelected = 0;
		SetNormalizedPosition(1f, 1);
	}

	public void PageUp()
	{
		float y = m_ViewBounds.size.y;
		float y2 = m_DesiredPosition.y;
		int num = 0;
		if (y2 - y > 0f)
		{
			y2 -= y;
			float num2 = 0f;
			while (num2 < y2)
			{
				num2 += m_ContentSelectables[num].rect.height + m_Spacing.y;
				num++;
			}
			if (num < m_ContentSelectables.Count)
			{
				num2 -= m_ContentSelectables[num].rect.height + m_Spacing.y;
			}
			num--;
			m_DesiredPosition.y = num2;
		}
		else
		{
			m_DesiredPosition.y = 0f;
		}
		T17EventSystem eventSystemForGamepadUser = T17EventSystemsManager.Instance.GetEventSystemForGamepadUser(m_CurrentGamepadUser);
		eventSystemForGamepadUser.SetSelectedGameObject(m_ContentSelectables[num].gameObject);
	}

	public void PageDown()
	{
		float y = m_ViewBounds.size.y;
		float y2 = m_DesiredPosition.y;
		float y3 = m_ContentBounds.size.y;
		int num = 0;
		if (y2 + y < y3)
		{
			y2 += y;
			float num2 = 0f;
			while (num2 < y2 && num < m_ContentSelectables.Count)
			{
				num2 += m_ContentSelectables[num].rect.height + m_Spacing.y;
				num++;
			}
			if (num < m_ContentSelectables.Count)
			{
				num2 -= m_ContentSelectables[num].rect.height + m_Spacing.y;
			}
			num--;
			m_DesiredPosition.y = num2;
		}
		else
		{
			m_DesiredPosition.y = y3;
			num = m_ContentSelectables.Count - 1;
		}
		T17EventSystem eventSystemForGamepadUser = T17EventSystemsManager.Instance.GetEventSystemForGamepadUser(m_CurrentGamepadUser);
		eventSystemForGamepadUser.SetSelectedGameObject(m_ContentSelectables[num].gameObject);
	}

	protected override void OnElementSelected(Selectable sel, int index)
	{
		base.OnElementSelected(sel, index);
		bool flag = false;
		if (m_ContentSelectables == null || m_ContentSelectables.Count <= 1)
		{
			return;
		}
		if (m_bScrollToSelectedElement && !flag)
		{
			m_OriginalPosition = m_ContentParent.localPosition;
			m_LerpTime = 0.3f;
			if (m_CurrentSelected > m_PreviousSelected && m_CurrentSelected < m_ContentSelectables.Count)
			{
				m_ContentSelectables[m_ContentSelectables.Count - 1].GetWorldCorners(m_ElementCorners);
				m_ViewPort.GetWorldCorners(m_ViewCorners);
				if (m_ElementCorners[0].y < m_ViewCorners[0].y || m_ElementCorners[1].y < m_ViewCorners[0].y || m_ElementCorners[2].y < m_ViewCorners[3].y || m_ElementCorners[3].y < m_ViewCorners[3].y)
				{
					if (!base.isAltLayout)
					{
						m_DesiredPosition += new Vector2(0f, m_ContentSelectables[m_PreviousSelected].rect.height + m_Spacing.y);
					}
					else
					{
						float y = m_ContentSelectables[m_PreviousSelected].localPosition.y;
						float y2 = m_ContentSelectables[m_CurrentSelected].localPosition.y;
						m_DesiredPosition += new Vector2(0f, y - y2);
					}
				}
			}
			else if (m_PreviousSelected > 0 && m_CurrentSelected < m_ContentSelectables.Count - 1)
			{
				m_ViewPort.GetWorldCorners(m_ViewCorners);
				m_ContentSelectables[0].GetWorldCorners(m_ElementCorners);
				if (m_ElementCorners[0].y > m_ViewCorners[1].y || m_ElementCorners[1].y > m_ViewCorners[1].y || m_ElementCorners[2].y > m_ViewCorners[2].y || m_ElementCorners[3].y > m_ViewCorners[2].y)
				{
					if (!base.isAltLayout)
					{
						m_DesiredPosition -= new Vector2(0f, m_ContentSelectables[m_PreviousSelected].rect.height + m_Spacing.y);
					}
					else
					{
						float y3 = m_ContentSelectables[m_PreviousSelected].localPosition.y;
						float y4 = m_ContentSelectables[m_CurrentSelected].localPosition.y;
						m_DesiredPosition -= new Vector2(0f, y4 - y3);
					}
				}
			}
		}
		else if (flag)
		{
			m_OriginalPosition = m_ContentParent.localPosition;
			m_LerpTime = 0.3f;
			if (m_CurrentSelected != m_PreviousSelected && m_CurrentSelected < m_ContentSelectables.Count)
			{
				m_ContentSelectables[m_CurrentSelected].GetWorldCorners(m_ElementCorners);
				m_ViewPort.GetWorldCorners(m_ViewCorners);
				if (m_ElementCorners[0].y < m_ViewCorners[1].y && m_ElementCorners[1].y > m_ViewCorners[1].y)
				{
					float num = m_ElementCorners[1].y - m_ViewCorners[1].y;
					float num2 = m_ElementCorners[1].y - m_ElementCorners[0].y;
					float num3 = Mathf.Abs(num / num2);
					m_DesiredPosition -= new Vector2(0f, m_ContentSelectables[m_CurrentSelected].rect.size.y * num3 + m_Spacing.y);
				}
				else if ((!(m_ElementCorners[1].y <= m_ViewCorners[1].y) || !(m_ElementCorners[0].y >= m_ViewCorners[0].y)) && m_ElementCorners[1].y > m_ViewCorners[0].y && m_ElementCorners[0].y < m_ViewCorners[0].y)
				{
					float num4 = m_ViewCorners[0].y - m_ElementCorners[0].y;
					float num5 = m_ElementCorners[1].y - m_ElementCorners[0].y;
					float num6 = Mathf.Abs(num4 / num5);
					m_DesiredPosition += new Vector2(0f, m_ContentSelectables[m_CurrentSelected].rect.size.y * num6 + m_Spacing.y);
				}
			}
		}
		UpdateBounds();
		float num7 = m_ContentBounds.size.y - m_ViewBounds.size.y;
		float max = m_ContentParent.localPosition.y + m_ViewBounds.min.y - m_ContentBounds.min.y;
		float min = m_ContentParent.localPosition.y + m_ViewBounds.min.y - num7 - m_ContentBounds.min.y;
		m_DesiredPosition.y = Mathf.Clamp(m_DesiredPosition.y, min, max);
	}

	protected override bool ReselectCurrent(ref T17EventSystem system)
	{
		bool flag = base.ReselectCurrent(ref system);
		if (flag)
		{
			if (system.currentSelectedGameObject == m_ContentSelectables[0].gameObject)
			{
				ResetPosition();
			}
			m_LerpTime = 0f;
		}
		return flag;
	}

	private void SetVerticalNormalizedPosition(float value)
	{
		SetNormalizedPosition(value, 1, true);
	}

	private void SetNormalizedPosition(float value, int axis, bool fromScrollbar = false)
	{
		UpdateBounds();
		float num = m_ContentBounds.size[axis] - m_ViewBounds.size[axis];
		float num2 = m_ViewBounds.min[axis] - value * num;
		float num3 = m_ContentParent.localPosition[axis] + num2 - m_ContentBounds.min[axis];
		Vector3 localPosition = m_ContentParent.localPosition;
		if (Mathf.Abs(localPosition[axis] - num3) > 0.01f)
		{
			localPosition[axis] = num3;
			m_ContentParent.localPosition = localPosition;
			m_DesiredPosition = localPosition;
			UpdateBounds();
		}
	}

	private void UpdateBounds()
	{
		m_ViewBounds = new Bounds(m_ViewPort.rect.center, m_ViewPort.rect.size);
		m_ContentBounds = GetContentBounds();
		if (!(m_ContentParent == null))
		{
			Vector3 size = m_ContentBounds.size;
			Vector3 center = m_ContentBounds.center;
			Vector3 vector = m_ViewBounds.size - size;
			if (vector.x > 0f)
			{
				center.x -= vector.x * (m_ContentParent.pivot.x - 0.5f);
				size.x = m_ViewBounds.size.x;
			}
			if (vector.y > 0f)
			{
				center.y -= vector.y * (m_ContentParent.pivot.y - 0.5f);
				size.y = m_ViewBounds.size.y;
			}
			m_ContentBounds.size = size;
			m_ContentBounds.center = center;
		}
	}

	private Bounds GetContentBounds()
	{
		if (m_ContentParent == null)
		{
			return default(Bounds);
		}
		Vector3 vector = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
		Vector3 vector2 = new Vector3(float.MinValue, float.MinValue, float.MinValue);
		Matrix4x4 worldToLocalMatrix = m_ViewPort.worldToLocalMatrix;
		m_ContentParent.GetWorldCorners(m_ContentCorners);
		for (int i = 0; i < 4; i++)
		{
			Vector3 lhs = worldToLocalMatrix.MultiplyPoint3x4(m_ContentCorners[i]);
			vector = Vector3.Min(lhs, vector);
			vector2 = Vector3.Max(lhs, vector2);
		}
		Bounds result = new Bounds(vector, Vector3.zero);
		result.Encapsulate(vector2);
		return result;
	}

	private Vector2 CalculateOffset(Vector2 delta)
	{
		Vector2 zero = Vector2.zero;
		Vector2 vector = m_ContentBounds.min;
		Vector2 vector2 = m_ContentBounds.max;
		if (m_VerticalScrollBar != null)
		{
			vector.y += delta.y;
			vector2.y += delta.y;
			if (vector2.y < m_ViewBounds.max.y)
			{
				zero.y = m_ViewBounds.max.y - vector2.y;
			}
			else if (vector.y > m_ViewBounds.min.y)
			{
				zero.y = m_ViewBounds.min.y - vector.y;
			}
		}
		return zero;
	}

	protected virtual void LateUpdate()
	{
		if ((bool)m_ContentParent)
		{
			UpdateBounds();
			Vector2 offset = CalculateOffset(Vector2.zero);
			if (m_ViewBounds != m_PrevViewBounds || m_ContentBounds != m_PrevContentBounds || m_ContentParent.anchoredPosition != m_PrevPosition)
			{
				UpdateScrollbars(offset);
				UpdatePrevData();
			}
		}
	}

	private void UpdatePrevData()
	{
		if (m_ContentParent == null)
		{
			m_PrevPosition = Vector2.zero;
		}
		else
		{
			m_PrevPosition = m_ContentParent.anchoredPosition;
		}
		m_PrevViewBounds = m_ViewBounds;
		m_PrevContentBounds = m_ContentBounds;
	}

	private void UpdateScrollbars(Vector2 offset)
	{
		if ((bool)m_VerticalScrollBar)
		{
			if (m_ContentBounds.size.y > 0f)
			{
				m_VerticalScrollBar.size = Mathf.Clamp01((m_ViewBounds.size.y - Mathf.Abs(offset.y)) / m_ContentBounds.size.y);
			}
			else
			{
				m_VerticalScrollBar.size = 1f;
			}
			m_VerticalScrollBar.value = verticalNormalizedPosition;
		}
	}

	public void ScrollToEntry(GameObject scrollTo, bool bSelect)
	{
		m_bSelectScrollItem = bSelect;
		m_ScrollToItem = scrollTo;
		m_ScrollTimer = 5;
	}

	private void InternalScrollToItem()
	{
		int count = m_ContentSelectables.Count;
		float num = 0f;
		T17GridLayoutGroup component = m_ContentParent.GetComponent<T17GridLayoutGroup>();
		for (int i = 0; i < count; i++)
		{
			RectTransform rectTransform = m_ContentSelectables[i];
			if (rectTransform != null && rectTransform.gameObject != m_ScrollToItem)
			{
				bool flag = true;
				if (base.isAltLayout && component != null && (i + 1) % component.m_CellCountX != 0)
				{
					flag = false;
				}
				if (flag)
				{
					float height = rectTransform.rect.height;
					num += height + m_Spacing.y;
				}
			}
			if (rectTransform.gameObject == m_ScrollToItem)
			{
				if (m_bSelectScrollItem)
				{
					m_PreviousSelected = i;
					m_CurrentSelected = i;
				}
				m_DesiredPosition.y = num;
				m_LerpTime = 0.3f;
				break;
			}
		}
	}

	public void ResetSelection(bool isTop)
	{
		if (m_ContentSelectables.Count == 0)
		{
			m_PreviousSelected = 0;
			m_CurrentSelected = 0;
			return;
		}
		int num = 0;
		if (!isTop)
		{
			num = m_ContentSelectables.Count - 1;
		}
		m_PreviousSelected = num;
		m_CurrentSelected = num;
		T17EventSystem eventSystemForGamepadUser = T17EventSystemsManager.Instance.GetEventSystemForGamepadUser(m_CurrentGamepadUser);
		eventSystemForGamepadUser.SetSelectedGameObject(m_ContentSelectables[num].gameObject);
		ScrollToEntry(m_ContentSelectables[num].gameObject, true);
	}

	public int GetCurrentSelected()
	{
		return m_CurrentSelected;
	}

	public void InstantlyScrollToItem(GameObject item, bool bSelect)
	{
		int count = m_ContentSelectables.Count;
		float num = 0f;
		T17GridLayoutGroup component = m_ContentParent.GetComponent<T17GridLayoutGroup>();
		for (int i = 0; i < count; i++)
		{
			RectTransform rectTransform = m_ContentSelectables[i];
			if (rectTransform != null && rectTransform.gameObject != item)
			{
				bool flag = true;
				if (base.isAltLayout && component != null && (i + 1) % component.m_CellCountX != 0)
				{
					flag = false;
				}
				if (flag)
				{
					float height = rectTransform.rect.height;
					num += height + m_Spacing.y;
				}
			}
			if (rectTransform.gameObject == item)
			{
				if (m_bSelectScrollItem)
				{
					m_PreviousSelected = i;
					m_CurrentSelected = i;
				}
				float num2 = m_ContentBounds.size.y - m_ViewBounds.size.y;
				float max = m_ContentParent.localPosition.y + m_ViewBounds.min.y - m_ContentBounds.min.y;
				float min = m_ContentParent.localPosition.y + m_ViewBounds.min.y - num2 - m_ContentBounds.min.y;
				num = Mathf.Clamp(num, min, max);
				m_ContentParent.localPosition = new Vector2(m_ContentParent.localPosition.x, num);
				break;
			}
		}
	}

	public void OnScroll(PointerEventData eventData)
	{
		if (base.isActiveAndEnabled && m_VerticalScrollBar != null)
		{
			m_VerticalScrollBar.OnScroll(eventData);
		}
	}
}
