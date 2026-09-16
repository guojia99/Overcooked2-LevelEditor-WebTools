using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

public class PointerDelegate : Selectable
{
	public UnityEvent PointerEnter;

	public UnityEvent PointerExit;

	protected override void Awake()
	{
		base.Awake();
		if (PointerEnter == null)
		{
			PointerEnter = new UnityEvent();
		}
		if (PointerExit == null)
		{
			PointerExit = new UnityEvent();
		}
	}

	public override void OnPointerEnter(PointerEventData eventData)
	{
		base.OnPointerEnter(eventData);
		if (PointerEnter != null)
		{
			PointerEnter.Invoke();
		}
	}

	public override void OnPointerExit(PointerEventData eventData)
	{
		base.OnPointerExit(eventData);
		if (PointerExit != null)
		{
			PointerExit.Invoke();
		}
	}
}
