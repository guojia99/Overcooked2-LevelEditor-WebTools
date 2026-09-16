using UnityEngine;

public interface IT17EventHelper
{
	void SetEventSystem(T17EventSystem gamersEventSystem = null);

	T17EventSystem GetDomain();

	GameObject GetGameobject();
}
