using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class RigidbodyMotion : MonoBehaviour
{
	private Rigidbody m_rigidbody;

	private void Awake()
	{
		m_rigidbody = base.gameObject.GetComponent<Rigidbody>();
	}

	private void OnDisable()
	{
		m_rigidbody.velocity = Vector3.zero;
	}

	public void SetKinematic(bool _kinematic)
	{
		m_rigidbody.isKinematic = _kinematic;
	}

	public void SetVelocity(Vector3 _velocity)
	{
		m_rigidbody.velocity = _velocity;
	}

	public void AddVelocity(Vector3 _additionalVelocity)
	{
		m_rigidbody.velocity += _additionalVelocity;
	}

	public void Accelerate(Vector3 _force)
	{
		m_rigidbody.AddForce(_force, ForceMode.Acceleration);
	}

	public void Movement(Vector3 _movement)
	{
		m_rigidbody.MovePosition(m_rigidbody.position + _movement * TimeManager.GetDeltaTime(base.gameObject));
	}

	public void Movement(Vector3 _movement, float _delta)
	{
		m_rigidbody.MovePosition(m_rigidbody.position + _movement * _delta);
	}

	public Vector3 GetVelocity()
	{
		return m_rigidbody.velocity;
	}

	public Vector3 GetVelocityXZ()
	{
		Vector3 velocity = GetVelocity();
		return velocity - Vector3.Dot(velocity, Vector3.up) * Vector3.up;
	}

	public void SetPosition(ref Vector3 position)
	{
		m_rigidbody.MovePosition(position);
	}

	public Vector3 GetPosition()
	{
		return m_rigidbody.position;
	}

	public void SetRotation(ref Quaternion rotation)
	{
		m_rigidbody.rotation = rotation;
	}

	public Quaternion GetRotation()
	{
		return m_rigidbody.rotation;
	}
}
