using UnityEngine;
using UnityEngine.EventSystems;

public class T17_UISelectDeselectEvents : MonoBehaviour, ISelectHandler, IDeselectHandler, IEventSystemHandler
{
	public EventTrigger.TriggerEvent m_OnSelectEvent = new EventTrigger.TriggerEvent();

	public EventTrigger.TriggerEvent m_OnDeselectEvent = new EventTrigger.TriggerEvent();

	public virtual void OnSelect(BaseEventData eventData)
	{
		if (m_OnSelectEvent != null)
		{
			m_OnSelectEvent.Invoke(eventData);
		}
	}

	public virtual void OnDeselect(BaseEventData eventData)
	{
		if (m_OnDeselectEvent != null)
		{
			m_OnDeselectEvent.Invoke(eventData);
		}
	}
}
