using System;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerAttachmentThrower : ServerSynchroniserBase, IThrower
{
	private AttachmentThrower m_thrower;

	private const float c_alertRaycastRadius = 1f;

	private const float c_alertRaycastDistance = 10f;

	private const int kRaycastHitsMax = 10;

	private static RaycastHit[] ms_raycastHits = new RaycastHit[10];

	private static int m_playersLayerMask = 0;

	private GenericVoid<GameObject> m_throwCallback = delegate
	{
	};

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_thrower = (AttachmentThrower)synchronisedObject;
	}

	public void RegisterThrowCallback(GenericVoid<GameObject> _callback)
	{
		m_throwCallback = (GenericVoid<GameObject>)Delegate.Combine(m_throwCallback, _callback);
	}

	public void UnregisterThrowCallback(GenericVoid<GameObject> _callback)
	{
		m_throwCallback = (GenericVoid<GameObject>)Delegate.Remove(m_throwCallback, _callback);
	}

	private void Awake()
	{
		m_playersLayerMask = LayerMask.GetMask("Players");
	}

	public void ThrowItem(GameObject _object, Vector2 _directionXZ)
	{
		IAttachment attachment = _object.RequestInterface<IAttachment>();
		if (attachment != null)
		{
			Vector3 velocity = CalculateThrowVelocity(_directionXZ);
			attachment.AccessMotion().SetVelocity(velocity);
			ICatchable catchable = _object.RequestInterface<ICatchable>();
			if (catchable != null)
			{
				AlertPotentialCatchers(catchable, attachment.AccessGameObject().transform.position, _directionXZ);
			}
		}
		m_throwCallback(_object);
	}

	private Vector3 CalculateThrowVelocity(Vector2 _directionXZ)
	{
		Vector3 zero = Vector3.zero;
		zero.x = _directionXZ.x * m_thrower.m_throwForce;
		zero.z = _directionXZ.y * m_thrower.m_throwForce;
		zero.y = Mathf.Tan((float)Math.PI / 180f * m_thrower.m_throwInclination) * m_thrower.m_throwForce;
		return zero;
	}

	private void AlertPotentialCatchers(ICatchable _object, Vector3 _position, Vector2 _directionXZ)
	{
		Vector3 direction = VectorUtils.FromXZ(_directionXZ, 0f);
		int num = Physics.SphereCastNonAlloc(_position, 1f, direction, ms_raycastHits, 10f, m_playersLayerMask);
		for (int i = 0; i < num; i++)
		{
			RaycastHit raycastHit = ms_raycastHits[i];
			GameObject gameObject = raycastHit.collider.gameObject;
			if (!(gameObject == base.gameObject))
			{
				IHandleCatch handleCatch = gameObject.RequestInterface<IHandleCatch>();
				if (handleCatch != null)
				{
					handleCatch.AlertToThrownItem(_object, this, _directionXZ);
				}
			}
		}
	}

	public void OnFailedToThrowItem(GameObject _object)
	{
	}
}
