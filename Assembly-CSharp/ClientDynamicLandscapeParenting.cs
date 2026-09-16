using UnityEngine;

public class ClientDynamicLandscapeParenting : MonoBehaviour
{
	private DynamicLandscapeParenting m_dynamicLandscapeParenting;

	private GroundCast m_groundCast;

	private void Awake()
	{
		if (GameUtils.GetLevelConfig().m_disableDynamicParenting)
		{
			Object.Destroy(this);
		}
	}
}
