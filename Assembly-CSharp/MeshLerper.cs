using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class MeshLerper : MonoBehaviour
{
	public enum Target
	{
		Line = 0,
		RigidBody = 1,
		ServerPosition = 2,
		SetPosition = 3,
		CurrentPosition = 4
	}

	private Transform m_Transform;

	private Rigidbody m_TargetRigidBody;

	private Transform m_TargetRotationTransform;

	private Target m_TargetFrom = Target.ServerPosition;

	private Target m_TargetTo = Target.ServerPosition;

	private float m_LerpLength = 0.01f;

	private float m_LerpTime;

	private bool m_bInitialised;

	private Vector3 m_SetStartPosition = default(Vector3);

	private ClientPhysicsObjectSynchroniser m_ParentPhysicsObjectSynchroniser;

	private bool m_bActive;

	private static NetworkPredictionTweekables Tweekables;

	private SurfaceMovable m_SurfaceMoveable;

	private Vector3 m_InitalOffset = default(Vector3);

	private Vector3 m_Position = default(Vector3);

	private Quaternion m_Rotation = default(Quaternion);

	private Quaternion m_PreviousSetRotation = Quaternion.identity;

	private Transform m_PreviousParent;

	private Vector3 m_PreviousParentPosition = default(Vector3);

	private Quaternion m_PreviousParentRotation = default(Quaternion);

	private bool m_TakeNextRotation;

	private float m_Period;

	private float m_previousPositionSetTime;

	private float m_rotationSpeed = 1f;

	public void Initialise(ClientPhysicsObjectSynchroniser _physicsObjectSynchroniser, Rigidbody _rigidBody, Transform _rotationTransform, PhysicalAttachment _physicalAttachment)
	{
		m_ParentPhysicsObjectSynchroniser = _physicsObjectSynchroniser;
		m_PreviousParent = _physicsObjectSynchroniser.transform.parent;
		if (m_PreviousParent != null)
		{
			m_PreviousParentPosition = m_PreviousParent.position;
			m_PreviousParentRotation = m_PreviousParent.rotation;
		}
		m_TargetRigidBody = _rigidBody;
		m_TargetRotationTransform = _rotationTransform;
		m_bInitialised = true;
		m_SurfaceMoveable = _physicalAttachment.m_surfaceMovable;
	}

	public virtual void Awake()
	{
		m_Transform = base.transform;
		m_Position = m_Transform.position;
		m_Rotation = Quaternion.identity;
		if (m_Transform.parent != null)
		{
			m_InitalOffset = m_Transform.position - m_Transform.parent.position;
		}
		if (Tweekables == null)
		{
			Tweekables = GameUtils.RequireManager<MultiplayerController>().m_NetworkPredictionTweekables;
		}
	}

	public void SetLerpActive(bool _active)
	{
		m_bActive = _active;
		if (_active)
		{
			m_Position = base.transform.position;
			m_Rotation = Quaternion.identity;
			m_TakeNextRotation = true;
		}
		base.transform.localPosition = default(Vector3);
		base.transform.localRotation = Quaternion.identity;
		m_SetStartPosition = base.transform.position;
	}

	public void SetTargets(Target _from, Target _to, float _length)
	{
		if (_from == Target.CurrentPosition)
		{
			SetTargets(m_Position, _to, _length);
			return;
		}
		m_TargetFrom = _from;
		m_TargetTo = _to;
		m_LerpTime = 0f;
		if (_length == 0f)
		{
			_length = float.Epsilon;
		}
		m_LerpLength = _length;
	}

	public void SetTargets(Vector3 _from, Target _to, float _length)
	{
		m_SetStartPosition = _from;
		SetTargets(Target.SetPosition, _to, _length);
	}

	public void SetNextTarget(Target _next, float _length)
	{
		SetTargets(m_TargetTo, _next, _length);
	}

	public string GetTargetTo()
	{
		return m_TargetTo.ToString() + "[" + GetTargetPositionTo().ToString("0.00") + "]";
	}

	public string GetTargetFrom()
	{
		return m_TargetFrom.ToString() + "[" + GetTargetPositionFrom().ToString("0.00") + "]";
	}

	public void SnapToTarget(Target _snapTo)
	{
		if (m_bActive)
		{
			m_Position = GetTargetVector(_snapTo);
			m_Rotation = Quaternion.identity;
			m_Transform.position = m_Position;
			m_Transform.localRotation = Quaternion.identity;
			m_Rotation = Quaternion.identity;
		}
		m_TargetFrom = _snapTo;
		m_TargetTo = _snapTo;
		m_LerpLength = 0.001f;
	}

	public virtual void Update()
	{
		if (!m_bInitialised || !m_bActive)
		{
			return;
		}
		if (m_PreviousParent != m_ParentPhysicsObjectSynchroniser.transform.parent)
		{
			m_PreviousParent = m_ParentPhysicsObjectSynchroniser.transform.parent;
			if (m_PreviousParent != null)
			{
				m_PreviousParentPosition = m_PreviousParent.position;
				m_PreviousParentRotation = m_PreviousParent.rotation;
			}
			m_Rotation = Quaternion.identity;
		}
		else if (m_PreviousParent != null)
		{
			Vector3 position = m_PreviousParent.position;
			Quaternion rotation = m_PreviousParent.rotation;
			Vector3 vector = position - m_PreviousParentPosition;
			Quaternion quaternion = Quaternion.Inverse(m_PreviousParentRotation) * rotation;
			m_PreviousParentPosition = position;
			m_PreviousParentRotation = rotation;
			m_Position += vector;
			m_Position = position + quaternion * (m_Position - position);
		}
		float deltaTime = Time.deltaTime;
		float t = 1f;
		if (m_LerpLength > 0f)
		{
			t = m_LerpTime / m_LerpLength;
		}
		Vector3 vector2 = Vector3.Lerp(GetTargetPositionFrom(), GetTargetPositionTo(), t);
		Vector3 position2 = m_Position;
		float b = ((m_TargetTo != Target.ServerPosition) ? m_ParentPhysicsObjectSynchroniser.ServerPreviousVelocity().magnitude : m_ParentPhysicsObjectSynchroniser.GetServerVelocity().magnitude);
		position2 += m_SurfaceMoveable.GetVelocity() * deltaTime;
		float num = Mathf.Max(m_TargetRigidBody.velocity.magnitude, b);
		float num2 = Mathf.Max(num * 0.95f, Tweekables.LerpMinimumSpeed);
		vector2 += m_Transform.rotation * m_InitalOffset;
		if (m_TargetTo == Target.Line || m_TargetTo == Target.RigidBody || m_TargetTo == Target.ServerPosition)
		{
			float magnitude = (vector2 - position2).magnitude;
			float num3 = magnitude / num2;
			float num4 = Mathf.Clamp(num3 / Tweekables.LerpTime, 1f, Tweekables.LerpFactorMax);
			float num5 = Mathf.Clamp(magnitude / Tweekables.LerpDistanceFactor * (magnitude / Tweekables.LerpDistanceFactor), 1f, Tweekables.LerpDistanceFactorMax);
			num2 *= num4 * num5;
		}
		m_Rotation = Quaternion.RotateTowards(m_Rotation, Quaternion.identity, m_rotationSpeed * deltaTime);
		m_Position = Vector3.MoveTowards(position2, vector2, num2 * deltaTime);
		m_Transform.localRotation = m_Rotation;
		m_Transform.position = m_Position;
		m_LerpTime += deltaTime;
	}

	private Vector3 GetTargetPositionFrom()
	{
		return GetTargetVector(m_TargetFrom);
	}

	private Vector3 GetTargetPositionTo()
	{
		return GetTargetVector(m_TargetTo);
	}

	private Vector3 GetTargetVector(Target _target)
	{
		switch (_target)
		{
		case Target.RigidBody:
			return m_TargetRigidBody.position;
		case Target.Line:
			return m_ParentPhysicsObjectSynchroniser.GetLinePosition();
		case Target.ServerPosition:
			return m_ParentPhysicsObjectSynchroniser.GetExtrapolatedServerPosition();
		case Target.SetPosition:
			return m_SetStartPosition;
		default:
			return Vector3.zero;
		}
	}

	private Quaternion GetTargetQuaternion(Target _target)
	{
		switch (_target)
		{
		case Target.Line:
		case Target.SetPosition:
		case Target.CurrentPosition:
			return m_TargetRigidBody.rotation;
		case Target.ServerPosition:
			return m_ParentPhysicsObjectSynchroniser.GetServerRotation();
		default:
			return m_TargetRigidBody.rotation;
		}
	}

	public void SetPosition(Vector3 _position)
	{
		m_Position = _position;
	}

	public void SetServerPosition(Vector3 _position, Quaternion _rotation)
	{
		float time = Time.time;
		if (time != m_previousPositionSetTime)
		{
			m_Period = time - m_previousPositionSetTime;
			m_Period *= 1.05f;
		}
		m_previousPositionSetTime = time;
		if (m_TakeNextRotation)
		{
			m_TakeNextRotation = false;
			m_PreviousSetRotation = _rotation;
			m_Rotation = Quaternion.identity;
		}
		Quaternion rotation = Quaternion.Inverse(m_PreviousSetRotation) * _rotation;
		m_Rotation *= Quaternion.Inverse(rotation);
		m_rotationSpeed = Quaternion.Angle(m_Rotation, Quaternion.identity) / m_Period;
		m_PreviousSetRotation = _rotation;
	}
}
