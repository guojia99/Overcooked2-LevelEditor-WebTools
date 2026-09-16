using UnityEngine;
using UnityEngine.EventSystems;

public class T17_UIPointerOverExitEvents : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IEventSystemHandler
{
	public EventTrigger.TriggerEvent m_OnPointerEnterEvent = new EventTrigger.TriggerEvent();

	public EventTrigger.TriggerEvent m_OnPointerExitEvent = new EventTrigger.TriggerEvent();

	public virtual void OnPointerEnter(PointerEventData eventData)
	{
		if (m_OnPointerEnterEvent != null)
		{
			m_OnPointerEnterEvent.Invoke(eventData);
		}
	}

	public virtual void OnPointerExit(PointerEventData eventData)
	{
		if (m_OnPointerExitEvent != null)
		{
			m_OnPointerExitEvent.Invoke(eventData);
		}
	}
}
