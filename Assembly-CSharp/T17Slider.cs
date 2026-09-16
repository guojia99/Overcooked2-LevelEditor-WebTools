using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[AddComponentMenu("T17_UI/Slider", 32)]
public class T17Slider : Slider, IT17EventHelper
{
	private T17EventSystem m_EventSystem;

	public void SetEventSystem(T17EventSystem gamersEventSystem)
	{
		m_EventSystem = gamersEventSystem;
	}

	public override void OnPointerDown(PointerEventData eventData)
	{
		if (eventData.button == PointerEventData.InputButton.Left)
		{
			if (IsInteractable() && base.navigation.mode != Navigation.Mode.None)
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
}
