using UnityEngine;

public class WorldMapLevelParenting : MonoBehaviour
{
	protected void Awake()
	{
		if (base.gameObject.RequestComponent<GridAutoParenting>() != null)
		{
			Transform parent = base.gameObject.transform.parent;
			GameObject gameObject = parent.gameObject.RequestChild("Tile/FlagBase");
			if (gameObject != null)
			{
				base.transform.SetParent(gameObject.transform, false);
				base.transform.localRotation = Quaternion.identity;
			}
		}
	}
}
