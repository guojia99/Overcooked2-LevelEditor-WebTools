using System;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

[AddComponentMenu("Scripts/Game/Ingredients/PhysicalAttachment")]
[RequireComponent(typeof(Collider))]
public class PhysicalAttachment : MonoBehaviour
{
	[Serializable]
	public class RigidbodyParams
	{
		public float m_mass;

		public float m_drag;

		public float m_angularDrag;

		public bool m_useGravity;

		public RigidbodyInterpolation m_interpolation;

		public CollisionDetectionMode m_collisionDetectionMode;

		public RigidbodyConstraints m_constraints;
	}

	[Serializable]
	public class GroundCastParams
	{
		public bool m_coliderOverride;

		public Collider m_collider;

		[Space]
		public bool m_radiusOverride;

		public float m_radius;

		[Space]
		public bool m_rayOffsetOverride;

		public Vector3 m_rayOffset;

		[Space]
		public bool m_maskOverride;

		public LayerMask m_mask;
	}

	[SerializeField]
	public RigidbodyParams m_rigidBodyParams;

	[SerializeField]
	public GroundCastParams m_groundCastParams = new GroundCastParams();

	[NonSerialized]
	public Rigidbody m_container;

	[NonSerialized]
	public RigidbodyMotion m_motion;

	[NonSerialized]
	public SurfaceMovable m_surfaceMovable;

	[NonSerialized]
	public MeshLerper m_meshLerper;

	[NonSerialized]
	public GameObject m_originalMesh;

	[NonSerialized]
	public GroundCast m_groundCast;

	private bool m_fakeMeshActive;

	private static int ms_GroundCastLayers = -1;

	public void InactiveSetup()
	{
		if (m_container == null)
		{
			CreateRigidBodyContainer();
		}
	}

	public void Awake()
	{
		if (ms_GroundCastLayers == -1)
		{
			ms_GroundCastLayers = LayerMask.GetMask("Ground", "SlopedGround", "Worktops");
		}
		if (m_container == null)
		{
			CreateRigidBodyContainer();
		}
	}

	private void OnDestroy()
	{
		if (m_container != null)
		{
			DestroyRigidBodyContainer();
		}
	}

	public bool GetFakeMeshActive()
	{
		return m_fakeMeshActive;
	}

	private void CreateRigidBodyContainer()
	{
		GameObject gameObject = new GameObject(base.gameObject.name + "_Rigidbody");
		gameObject.transform.position = base.transform.position;
		gameObject.transform.rotation = base.transform.rotation;
		m_container = gameObject.AddComponent<Rigidbody>();
		m_container.position = base.transform.position;
		m_container.rotation = base.transform.rotation;
		m_motion = gameObject.AddComponent<RigidbodyMotion>();
		gameObject.AddComponent<ObjectContainer>();
		m_container.mass = m_rigidBodyParams.m_mass;
		m_container.drag = m_rigidBodyParams.m_drag;
		m_container.angularDrag = m_rigidBodyParams.m_angularDrag;
		m_container.useGravity = m_rigidBodyParams.m_useGravity;
		m_container.interpolation = m_rigidBodyParams.m_interpolation;
		m_container.collisionDetectionMode = m_rigidBodyParams.m_collisionDetectionMode;
		m_container.constraints = m_rigidBodyParams.m_constraints;
		gameObject.AddComponent<ForwardCollisionToChildren>();
		gameObject.AddComponent<DynamicLandscapeParenting>();
		PhysicsObjectSynchroniser physicsObjectSynchroniser = gameObject.AddComponent<PhysicsObjectSynchroniser>();
		physicsObjectSynchroniser.SetPhysicalAttachment(this);
		m_surfaceMovable = gameObject.AddComponent<SurfaceMovable>();
		m_motion.SetKinematic(true);
		m_groundCast = gameObject.GetComponent<GroundCast>();
		if (m_groundCast != null)
		{
			Collider collider = ((!m_groundCastParams.m_coliderOverride) ? base.gameObject.GetComponent<Collider>() : m_groundCastParams.m_collider);
			Vector3 rayOffset = ((!m_groundCastParams.m_rayOffsetOverride) ? base.gameObject.transform.InverseTransformPoint(collider.bounds.center) : m_groundCastParams.m_rayOffset);
			LayerMask mask = ((!m_groundCastParams.m_maskOverride) ? ((LayerMask)ms_GroundCastLayers) : m_groundCastParams.m_mask);
			float radius = 0f;
			if (m_groundCastParams.m_radiusOverride)
			{
				radius = m_groundCastParams.m_radius;
			}
			else if (collider != null && HasXZRotationConstraints(m_container))
			{
				Vector3 extents = collider.bounds.extents;
				radius = extents.XZ().magnitude;
			}
			m_groundCast.Setup(collider, radius, rayOffset, mask);
		}
	}

	private void DestroyRigidBodyContainer()
	{
		if (m_meshLerper != null)
		{
			UnityEngine.Object.Destroy(m_meshLerper.gameObject);
		}
		UnityEngine.Object.Destroy(m_container.gameObject);
		m_surfaceMovable = null;
		m_motion = null;
		m_container = null;
	}

	public void UseStaticPositioning()
	{
		if (m_meshLerper != null)
		{
			m_meshLerper.SetLerpActive(false);
		}
		m_fakeMeshActive = false;
	}

	public void UseMeshLerp()
	{
		if (m_meshLerper != null)
		{
			m_meshLerper.SetLerpActive(true);
		}
		m_fakeMeshActive = true;
	}

	private bool HasXZRotationConstraints(Rigidbody rigidbody)
	{
		return (rigidbody.constraints & (RigidbodyConstraints)80) != 0;
	}
}
