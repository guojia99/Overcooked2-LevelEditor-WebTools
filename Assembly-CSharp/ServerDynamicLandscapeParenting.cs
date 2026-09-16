using UnityEngine;

public class ServerDynamicLandscapeParenting : MonoBehaviour
{
	private DynamicLandscapeParenting m_dynamicLandscapeParenting;

	private GroundCast m_groundCast;

	private void Awake()
	{
		if (GameUtils.GetLevelConfig().m_disableDynamicParenting)
		{
			Object.Destroy(this);
			return;
		}
		m_dynamicLandscapeParenting = base.gameObject.RequireComponent<DynamicLandscapeParenting>();
		m_groundCast = base.gameObject.RequireComponent<GroundCast>();
	}

	private void OnEnable()
	{
		if (m_groundCast != null)
		{
			m_groundCast.RegisterGroundChangedCallback(OnGroundChanged);
			OnGroundChanged(m_groundCast.GetGroundCollider());
		}
	}

	private void OnDisable()
	{
		if (m_groundCast != null)
		{
			m_groundCast.UnregisterGroundChangedCallback(OnGroundChanged);
		}
	}

	private void OnGroundChanged(Collider _collider)
	{
		m_dynamicLandscapeParenting.OnGroundChange(_collider);
	}
}
