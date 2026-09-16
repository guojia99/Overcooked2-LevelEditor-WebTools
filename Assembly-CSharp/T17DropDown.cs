using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("T17_UI/Dropdown", 35)]
[RequireComponent(typeof(RectTransform))]
public class T17DropDown : Dropdown, IT17EventHelper
{
	public T17EventSystem GetDomain()
	{
		return null;
	}

	public GameObject GetGameobject()
	{
		return base.gameObject;
	}

	public void SetEventSystem(T17EventSystem gamersEventSystem = null)
	{
	}
}
