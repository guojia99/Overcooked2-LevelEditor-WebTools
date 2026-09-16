using UnityEngine;

[RequireComponent(typeof(GroundCast))]
[RequireComponent(typeof(Rigidbody))]
public class SurfaceMovable : MonoBehaviour
{
	private Rigidbody m_rigidBody;

	private GroundCast m_groundCast;

	private Collider m_collider;

	private IMovingSurface m_prevSurface;

	private IMovingSurface m_surface;

	private Vector3 m_surfaceVelocity = Vector3.zero;

	public Vector3 GetVelocity()
	{
		return m_surfaceVelocity;
	}

	private void Awake()
	{
		m_rigidBody = base.gameObject.RequireComponent<Rigidbody>();
		m_groundCast = base.gameObject.RequireComponent<GroundCast>();
		m_collider = base.gameObject.GetComponentInChildren<Collider>();
		if (m_groundCast != null)
		{
			m_groundCast.RegisterGroundChangedCallback(OnGroundChanged);
		}
	}

	private void OnDisable()
	{
		m_surfaceVelocity = Vector3.zero;
		m_surface = null;
		m_prevSurface = null;
	}

	private void Update()
	{
		if (m_surface != null)
		{
			m_surfaceVelocity = m_surface.CalculateVelocityAtPoint(m_groundCast.GetGroundPoint(), m_prevSurface);
		}
		else
		{
			m_surfaceVelocity = Vector3.zero;
		}
	}

	private void OnGroundChanged(Collider groundCollider)
	{
		IMovingSurface movingSurface = ((!(groundCollider != null)) ? null : groundCollider.gameObject.RequestInterface<IMovingSurface>());
		if (movingSurface != null)
		{
			if (movingSurface != m_surface)
			{
				m_prevSurface = m_surface;
				m_surface = movingSurface;
			}
		}
		else
		{
			m_surface = null;
			m_prevSurface = null;
		}
	}
}
