using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[AddComponentMenu("T17_UI/Scrollbar", 31)]
public class T17Scrollbar : Scrollbar, IT17EventHelper, IScrollHandler, IEventSystemHandler
{
	[Range(0.01f, 0.2f)]
	public float m_ScrollSensitivity = 0.05f;

	private T17EventSystem m_EventSystem;

	private bool m_bReceivedScrollEvent;

	public void SetEventSystem(T17EventSystem gamersEventSystem = null)
	{
		m_EventSystem = gamersEventSystem;
	}

	public override void OnPointerDown(PointerEventData eventData)
	{
		if (eventData.button == PointerEventData.InputButton.Left)
		{
			if (IsInteractable() && base.navigation.mode != Navigation.Mode.None && m_EventSystem != null)
			{
				m_EventSystem.SetSelectedGameObject(base.gameObject, eventData);
			}
			base.OnPointerDown(eventData);
		}
	}

	public override void Select()
	{
		if (!m_EventSystem.alreadySelecting)
		{
			m_EventSystem.SetSelectedGameObject(base.gameObject);
		}
	}

	public T17EventSystem GetDomain()
	{
		return m_EventSystem;
	}

	public GameObject GetGameobject()
	{
		return base.gameObject;
	}

	public bool CanReselectOnMouseDisable()
	{
		return true;
	}

	public bool ReleaseSelectionOnPointerClickOrExit()
	{
		return true;
	}

	private void Update()
	{
		m_bReceivedScrollEvent = false;
	}

	public void OnScroll(PointerEventData eventData)
	{
		if (IsActive() && !m_bReceivedScrollEvent)
		{
			m_bReceivedScrollEvent = true;
			Vector2 scrollDelta = eventData.scrollDelta;
			if (Mathf.Abs(scrollDelta.x) > Mathf.Abs(scrollDelta.y))
			{
				scrollDelta.y = scrollDelta.x;
			}
			scrollDelta.x = 0f;
			base.value += scrollDelta.y * m_ScrollSensitivity * Mathf.Lerp(1f, 10f, base.size);
		}
	}
}
