using UnityEngine;

public class ServerMapAvatarDynamicLandscapeParenting : MonoBehaviour
{
	private MapAvatarDynamicLandscapeParenting m_dynamicLandscapeParenting;

	private MapAvatarGroundCast m_groundCast;

	private void Awake()
	{
		m_dynamicLandscapeParenting = base.gameObject.RequireComponent<MapAvatarDynamicLandscapeParenting>();
		m_groundCast = base.gameObject.RequireComponent<MapAvatarGroundCast>();
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
