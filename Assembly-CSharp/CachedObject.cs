using UnityEngine;

public class CachedObject : MonoBehaviour
{
	private void OnDestroy()
	{
		ComponentCacheRegistry.EraseObject(base.gameObject);
	}
}
