using System;
using UnityEngine;

public class GroundCast : MonoBehaviour
{
	[SerializeField]
	private LayerMask m_landscapeMask;

	[SerializeField]
	private float m_radius;

	[SerializeField]
	private bool m_disableRigidbodySleep;

	private Vector3 m_offset;

	private Rigidbody m_rigidBody;

	private Collider m_collider;

	private const float c_maxGroundAngle = 58f;

	private const float c_maxRaycastDistance = 2f;

	private const int c_maxRaycastHits = 6;

	private const float c_minGroundDistance = 0.3f;

	private Ray m_ray = default(Ray);

	private RaycastHit[] m_rayHits = new RaycastHit[6];

	private Collider m_groundCollider;

	private LayerMask m_groundLayer = default(LayerMask);

	private Vector3 m_groundPoint = Vector3.zero;

	private Vector3 m_groundNormal = Vector3.zero;

	private float m_groundDistance;

	private Transform m_Transform;

	private bool m_isCurrent;

	private VoidGeneric<Collider> m_callback = delegate
	{
	};

	public Collider GetGroundCollider()
	{
		return m_groundCollider;
	}

	public LayerMask GetGroundLayer()
	{
		return m_groundLayer;
	}

	public Vector3 GetGroundPoint()
	{
		return m_groundPoint;
	}

	public Vector3 GetGroundNormal()
	{
		return m_groundNormal;
	}

	public float GetGroundDistance()
	{
		return m_groundDistance;
	}

	public bool HasGroundContact()
	{
		return m_isCurrent;
	}

	public void RegisterGroundChangedCallback(VoidGeneric<Collider> _callback)
	{
		m_callback = (VoidGeneric<Collider>)Delegate.Combine(m_callback, _callback);
	}

	public void UnregisterGroundChangedCallback(VoidGeneric<Collider> _callback)
	{
		m_callback = (VoidGeneric<Collider>)Delegate.Remove(m_callback, _callback);
	}

	public void Setup(Collider _collider, float _radius, Vector3 _rayOffset, LayerMask _mask)
	{
		m_collider = _collider;
		m_radius = _radius;
		m_offset = _rayOffset;
		m_landscapeMask = _mask;
	}

	public void ForceUpdateNow()
	{
		FindGround();
	}

	private void Awake()
	{
		m_rigidBody = base.gameObject.RequireComponent<Rigidbody>();
		m_collider = base.gameObject.RequestComponent<Collider>();
		m_Transform = base.transform;
	}

	private void Update()
	{
		if (!m_disableRigidbodySleep)
		{
			if (!m_rigidBody.IsSleeping())
			{
				FindGround();
			}
		}
		else
		{
			FindGround();
		}
	}

	private void FindGround()
	{
		Vector3 vector = m_Transform.TransformPoint(m_offset);
		m_ray.direction = -Vector3.up;
		m_ray.origin = vector + Vector3.up;
		int num = 0;
		num = ((!(m_radius > 0f)) ? Physics.RaycastNonAlloc(m_ray, m_rayHits, 2f, m_landscapeMask) : Physics.SphereCastNonAlloc(m_ray, m_radius, m_rayHits, 2f, m_landscapeMask));
		int num2 = -1;
		for (int i = 0; i < num; i++)
		{
			RaycastHit raycastHit = m_rayHits[i];
			if (!(raycastHit.distance <= 0f))
			{
				float f = Mathf.Clamp(Vector3.Dot(raycastHit.normal, Vector3.up), -1f, 1f);
				float num3 = Mathf.Acos(f) * 57.29578f;
				if (!(num3 > 58f) && (num2 < 0 || (raycastHit.distance > 0f && raycastHit.distance < m_rayHits[num2].distance)))
				{
					num2 = i;
				}
			}
		}
		if (num2 >= 0)
		{
			ProcessGroundHit(m_rayHits[num2]);
		}
		else
		{
			HitGround(null, Vector3.zero, Vector3.zero, 0f);
		}
	}

	private void ProcessGroundHit(RaycastHit _hit)
	{
		Collider collider = null;
		Vector3 point = Vector3.zero;
		Vector3 normal = Vector3.zero;
		float distance = 0f;
		Vector3 closestPointOnSurface = InteractWithItemHelper.GetClosestPointOnSurface(m_collider, _hit.point);
		Vector3 lhs = _hit.point - closestPointOnSurface;
		float magnitude = lhs.magnitude;
		if (magnitude <= 0.3f)
		{
			collider = _hit.collider;
			point = _hit.point;
			normal = _hit.normal;
			float num = Vector3.Dot(lhs, -Vector3.up);
			if (num > 0f)
			{
				distance = magnitude;
			}
		}
		HitGround(collider, point, normal, distance);
	}

	private void HitGround(Collider _collider, Vector3 _point, Vector3 _normal, float _distance)
	{
		bool isCurrent = m_isCurrent;
		Collider groundCollider = m_groundCollider;
		m_groundCollider = _collider;
		m_groundLayer = ((_collider != null) ? (1 << _collider.gameObject.layer) : 0);
		m_groundPoint = _point;
		m_groundNormal = _normal;
		m_groundDistance = _distance;
		if (_collider != null)
		{
			m_isCurrent = true;
		}
		else
		{
			m_isCurrent = false;
		}
		if (m_isCurrent != isCurrent || (m_isCurrent && m_groundCollider != groundCollider))
		{
			m_callback(_collider);
		}
	}

	public void ClearGround()
	{
		m_groundCollider = null;
		m_isCurrent = false;
	}
}
