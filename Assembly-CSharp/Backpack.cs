using UnityEngine;

public class Backpack : CarryableItem
{
	[SerializeField]
	public float m_pickupAngle = 90f;

	[SerializeField]
	public Collider m_collider;

	[HideInInspector]
	public Vector3 m_restingColliderCenter;

	[HideInInspector]
	public Vector3 m_restingColliderSize;

	[HideInInspector]
	public Vector3 m_carriedColliderCenter;

	[HideInInspector]
	public Vector3 m_carriedColliderSize;

	public bool m_usesSeparateColliders { get; private set; }

	public bool CanHandleDispenserPickup(ICarrier _carrier)
	{
		Vector3 vector = _carrier.AccessGameObject().transform.position - base.transform.position;
		vector.y = 0f;
		Vector3 normalized = vector.normalized;
		float f = Vector3.Dot(-base.transform.forward, normalized);
		float num = Mathf.Acos(f) * 57.29578f;
		return num <= m_pickupAngle * 0.5f;
	}

	private void Awake()
	{
		BoxCollider boxCollider = base.gameObject.RequestComponentInImmediateChildren<BoxCollider>();
		m_usesSeparateColliders = boxCollider != null;
		if (m_usesSeparateColliders)
		{
			m_restingColliderCenter = boxCollider.center;
			m_restingColliderSize = boxCollider.size;
			Object.Destroy(boxCollider.gameObject);
		}
		BoxCollider boxCollider2 = m_collider as BoxCollider;
		m_usesSeparateColliders &= boxCollider2 != null;
		if (m_usesSeparateColliders)
		{
			m_carriedColliderCenter = boxCollider2.center;
			m_carriedColliderSize = boxCollider2.size;
			boxCollider2.center = m_restingColliderCenter;
			boxCollider2.size = m_restingColliderSize;
		}
	}
}
