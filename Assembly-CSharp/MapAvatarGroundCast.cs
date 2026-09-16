using System;
using UnityEngine;

public class MapAvatarGroundCast : MonoBehaviour
{
	public struct MapAvatarGroundHits
	{
		public RaycastHit frontLeft;

		public RaycastHit frontRight;

		public RaycastHit rear;

		public RaycastHit initial;
	}

	private const float c_raycastDistance = 99f;

	private const float c_maxRaycastAngle = 45f;

	private const float c_maxGroundAngle = 45f;

	private const float c_maxGroundDistance = 1f;

	private const float c_distanceEpsilon = 0.05f;

	private const float c_raycastYOffset = 20f;

	private const int c_maxRaycastHits = 6;

	[SerializeField]
	private LayerMask m_landscapeMask = default(LayerMask);

	[SerializeField]
	private Transform m_frontLeftPoint;

	[SerializeField]
	private Transform m_frontRightPoint;

	[SerializeField]
	private Transform m_rearPoint;

	private Vector3 m_centerPointLocal = Vector3.zero;

	private Ray m_ray = default(Ray);

	private RaycastHit[] m_rayHits = new RaycastHit[6];

	private MapAvatarGroundHits m_raycastHits = default(MapAvatarGroundHits);

	private Rigidbody m_rigidBody;

	private Collider m_groundCollider;

	private Plane m_groundPlane;

	private float m_distanceToGround;

	private bool m_isGrounded;

	private VoidGeneric<Collider> m_callback = delegate
	{
	};

	private static RaycastHit[] m_sortedHits = new RaycastHit[2];

	public void RegisterGroundChangedCallback(VoidGeneric<Collider> _callback)
	{
		m_callback = (VoidGeneric<Collider>)Delegate.Combine(m_callback, _callback);
	}

	public void UnregisterGroundChangedCallback(VoidGeneric<Collider> _callback)
	{
		m_callback = (VoidGeneric<Collider>)Delegate.Remove(m_callback, _callback);
	}

	public Collider GetGroundCollider()
	{
		return m_groundCollider;
	}

	public bool HasGroundContact()
	{
		return m_isGrounded;
	}

	public Vector3 GetGroundNormal()
	{
		return m_groundPlane.normal;
	}

	public Vector3 GetClosestPointOnGround()
	{
		return base.transform.position + m_distanceToGround * -m_groundPlane.normal;
	}

	public Vector3 GetClosestPointOnGround(Vector3 position)
	{
		if (GroundCastFromPoint(position + new Vector3(0f, 20f, 0f), ref m_raycastHits.initial))
		{
			return m_raycastHits.initial.point;
		}
		return position + new Vector3(0f, 20f, 0f);
	}

	public bool IsAboveGround()
	{
		return m_groundPlane.GetSide(base.transform.position);
	}

	public void ForceUpdateNow()
	{
		FindGround();
	}

	private void Awake()
	{
		m_rigidBody = base.gameObject.RequireComponent<Rigidbody>();
	}

	private void Start()
	{
		m_groundPlane.SetNormalAndPosition(Vector3.up, Vector3.zero);
		Vector3 position = (m_frontLeftPoint.position + m_frontRightPoint.position + m_rearPoint.position) / 3f;
		m_centerPointLocal = base.transform.InverseTransformPoint(position);
	}

	private void Update()
	{
		if (!m_rigidBody.IsSleeping())
		{
			FindGround();
		}
	}

	private void FindGround()
	{
		GroundCastFromPoint(m_frontLeftPoint.position, ref m_raycastHits.frontLeft);
		GroundCastFromPoint(m_frontRightPoint.position, ref m_raycastHits.frontRight);
		GroundCastFromPoint(m_rearPoint.position, ref m_raycastHits.rear);
		CalculateGroundPlane(ref m_groundPlane, m_raycastHits.frontLeft, m_raycastHits.frontRight, m_raycastHits.rear);
		m_distanceToGround = m_groundPlane.GetDistanceToPoint(base.transform.position);
		Collider groundCollider = m_groundCollider;
		bool isGrounded = m_isGrounded;
		m_isGrounded = m_distanceToGround < 0.05f;
		if (m_isGrounded)
		{
			GroundCastFromPoint(base.transform.TransformPoint(m_centerPointLocal), ref m_raycastHits.initial);
			m_groundCollider = m_raycastHits.initial.collider;
		}
		else
		{
			m_groundCollider = null;
		}
		if (m_isGrounded != isGrounded || (m_isGrounded && m_groundCollider != groundCollider))
		{
			m_callback(m_groundCollider);
		}
		Vector3 normal = m_groundPlane.normal;
		Vector3 normalized = Vector3.Cross(normal, Vector3.forward).normalized;
		Vector3 normalized2 = Vector3.Cross(normalized, normal).normalized;
		Debug.DrawRay(base.transform.position + Vector3.up, normal, Color.red);
		Debug.DrawRay(base.transform.position + Vector3.up, normalized, Color.green);
		Debug.DrawRay(base.transform.position + Vector3.up, normalized2, Color.cyan);
	}

	private bool GroundCastFromPoint(Vector3 _point, ref RaycastHit _hit)
	{
		m_ray.origin = _point;
		m_ray.direction = Vector3.down;
		int num = Physics.RaycastNonAlloc(m_ray, m_rayHits, 99f, m_landscapeMask);
		int num2 = -1;
		for (int i = 0; i < num; i++)
		{
			RaycastHit raycastHit = m_rayHits[i];
			if (!(raycastHit.distance <= 0f))
			{
				float f = Mathf.Clamp(Vector3.Dot(raycastHit.normal, Vector3.up), -1f, 1f);
				float num3 = Mathf.Acos(f);
				if (!(num3 > 45f) && (num2 < 0 || (raycastHit.distance > 0f && raycastHit.distance < m_rayHits[num2].distance)))
				{
					num2 = i;
				}
			}
		}
		if (num2 != -1)
		{
			_hit = m_rayHits[num2];
			return true;
		}
		return false;
	}

	private bool HitGround(RaycastHit _hit)
	{
		return _hit.collider != null && _hit.distance < 1f && Vector3.Angle(_hit.normal, Vector3.up) < 45f;
	}

	private bool CalculateGroundPlane(ref Plane _plane, RaycastHit _fl, RaycastHit _fr, RaycastHit _r)
	{
		int num = 0;
		if (HitGround(_fl) && HitGround(_fr))
		{
			int num2 = CompareHitsToGround(_fl, _fr);
			if (num2 > 0)
			{
				m_sortedHits[num++] = _fr;
			}
			else
			{
				m_sortedHits[num++] = _fl;
			}
		}
		else if (HitGround(_fl))
		{
			m_sortedHits[num++] = _fl;
		}
		else if (HitGround(_fr))
		{
			m_sortedHits[num++] = _fr;
		}
		if (HitGround(_r))
		{
			m_sortedHits[num++] = _r;
		}
		if (num >= 2)
		{
			Vector3 vector = m_sortedHits[1].point - m_sortedHits[0].point;
			Vector3 inPoint = m_sortedHits[0].point + vector * 0.5f;
			Vector3 normalized = (m_sortedHits[0].normal + m_sortedHits[1].normal).normalized;
			Vector3 normalized2 = Vector3.Cross(normalized, vector.normalized).normalized;
			Vector3 normalized3 = Vector3.Cross(vector, normalized2).normalized;
			if (Vector3.Angle(normalized3, _plane.normal) < 45f)
			{
				_plane.SetNormalAndPosition(normalized3, inPoint);
			}
		}
		if (num == 1)
		{
			_plane.SetNormalAndPosition(m_sortedHits[0].normal, m_sortedHits[0].point);
		}
		if (num == 0)
		{
			_plane.SetNormalAndPosition(Vector3.up, -(Vector3.up * float.MaxValue));
		}
		if (num > 0)
		{
			Debug.DrawRay(m_sortedHits[0].point, m_sortedHits[0].normal, Color.green);
		}
		if (num > 1)
		{
			Debug.DrawRay(m_sortedHits[1].point, m_sortedHits[1].normal, Color.green);
		}
		if (num > 2)
		{
			Debug.DrawRay(m_sortedHits[2].point, m_sortedHits[2].normal, Color.blue);
		}
		if (num >= 2)
		{
			Debug.DrawLine(m_sortedHits[0].point + Vector3.up, m_sortedHits[1].point + Vector3.up, Color.yellow);
		}
		return num > 0;
	}

	private int CompareHitsToGround(RaycastHit _x, RaycastHit _y)
	{
		float y = _x.point.y;
		return _y.point.y.CompareTo(y);
	}
}
