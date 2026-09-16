using System;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerProjectile : ServerSynchroniserBase
{
	private float m_Duration = 2f;

	private Vector3 m_TargetPosition;

	private Vector3 m_Gravity;

	private Vector3 m_InitialPosition;

	private Vector3 m_InitialVelocity;

	private float m_Timer;

	private Rigidbody m_Rigidbody;

	private Transform m_OverrideTargetTransform;

	private VoidGeneric<ServerProjectile> m_ReachedTargetCallback = delegate
	{
	};

	private VoidGeneric<ServerProjectile, Collision> m_CollidedCallback = delegate
	{
	};

	public void RegisterReachedTargetCallback(VoidGeneric<ServerProjectile> _callback)
	{
		m_ReachedTargetCallback = (VoidGeneric<ServerProjectile>)Delegate.Combine(m_ReachedTargetCallback, _callback);
	}

	public void UnregisterReachedTargetCallback(VoidGeneric<ServerProjectile> _callback)
	{
		m_ReachedTargetCallback = (VoidGeneric<ServerProjectile>)Delegate.Remove(m_ReachedTargetCallback, _callback);
	}

	public void RegisterCollidedCallback(VoidGeneric<ServerProjectile, Collision> _callback)
	{
		m_CollidedCallback = (VoidGeneric<ServerProjectile, Collision>)Delegate.Combine(m_CollidedCallback, _callback);
	}

	public void UnregisterCollidedCallback(VoidGeneric<ServerProjectile, Collision> _callback)
	{
		m_CollidedCallback = (VoidGeneric<ServerProjectile, Collision>)Delegate.Remove(m_CollidedCallback, _callback);
	}

	public void SetTargetAndTimeToTarget(Vector3 targetPosition, float timeToTarget)
	{
		m_TargetPosition = targetPosition;
		m_Duration = timeToTarget;
	}

	public void SetTargetAndTimeToTarget(Transform targetPosition, float timeToTarget)
	{
		m_OverrideTargetTransform = targetPosition;
		m_Duration = timeToTarget;
	}

	public void SetGravity(Vector3 gravity)
	{
		m_Gravity = gravity;
	}

	public void Start()
	{
		m_Rigidbody = base.gameObject.RequestComponentUpwardsRecursive<Rigidbody>();
		m_Rigidbody.isKinematic = true;
		m_InitialPosition = m_Rigidbody.position;
		Vector3 targetPosition = GetTargetPosition();
		m_InitialVelocity.x = (targetPosition.x - base.transform.position.x - 0.5f * m_Gravity.x * m_Duration * m_Duration) / m_Duration;
		m_InitialVelocity.y = (targetPosition.y - base.transform.position.y - 0.5f * m_Gravity.y * m_Duration * m_Duration) / m_Duration;
		m_InitialVelocity.z = (targetPosition.z - base.transform.position.z - 0.5f * m_Gravity.z * m_Duration * m_Duration) / m_Duration;
	}

	public override void UpdateSynchronising()
	{
		base.UpdateSynchronising();
		m_Timer += TimeManager.GetDeltaTime(base.gameObject);
		Vector3 targetPosition = default(Vector3);
		targetPosition.x = m_InitialPosition.x + m_InitialVelocity.x * m_Timer + 0.5f * m_Gravity.x * m_Timer * m_Timer;
		targetPosition.y = m_InitialPosition.y + m_InitialVelocity.y * m_Timer + 0.5f * m_Gravity.y * m_Timer * m_Timer;
		targetPosition.z = m_InitialPosition.z + m_InitialVelocity.z * m_Timer + 0.5f * m_Gravity.z * m_Timer * m_Timer;
		bool flag = false;
		if (m_Timer >= m_Duration)
		{
			targetPosition = GetTargetPosition();
			flag = true;
		}
		m_Rigidbody.position = targetPosition;
		if (flag)
		{
			m_ReachedTargetCallback(this);
		}
	}

	public void OnCollisionEnter(Collision collision)
	{
		m_CollidedCallback(this, collision);
	}

	private Vector3 GetTargetPosition()
	{
		return (!(m_OverrideTargetTransform != null)) ? m_TargetPosition : m_OverrideTargetTransform.position;
	}

	private bool IsUsingOverrideTransform()
	{
		return (m_OverrideTargetTransform != null) ? true : false;
	}
}
