using System;
using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerThrowableItem : ServerSynchroniserBase, IThrowable, IHandlePickup, IBaseHandlePickup
{
	private ThrowableItem m_throwableItem;

	private static int ms_AttachmentsLayer;

	private ThrowableItemMessage m_data = new ThrowableItemMessage();

	private const float c_minFlightTime = 0.1f;

	private const float c_maxFlightTime = 5f;

	private const float c_minVelocity = 0.15f;

	private const float c_minVelocitySqr = 0.0225f;

	private const float c_horizCollisionTolerance = 45f;

	private const float c_bounceAngleMax = 80f;

	private IAttachment m_attachment;

	private IThrower m_thrower;

	private float m_flightTimer;

	private bool m_flying;

	private IThrower m_previousThrower;

	private float m_throwerTimer;

	private ServerHandlePickupReferral m_pickupReferral;

	private AttachChangedCallback m_OnAttachChanged;

	private Generic<bool> m_inFlight;

	private GenericVoid<GameObject> m_landedCallback = delegate
	{
	};

	private List<Generic<bool>> m_canThrowCallbacks = new List<Generic<bool>>();

	private Collider[] m_ThrowStartColliders = new Collider[4];

	private Collider m_Collider;

	private int m_ignoredCollidersCount;

	private Transform m_Transform;

	public override EntityType GetEntityType()
	{
		return EntityType.ThrowableItem;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_throwableItem = (ThrowableItem)synchronisedObject;
		m_inFlight = IsFlying;
		m_pickupReferral = base.gameObject.RequestComponent<ServerHandlePickupReferral>();
		ServerLimitedQuantityItem component = GetComponent<ServerLimitedQuantityItem>();
		if (null != component)
		{
			component.AddInvincibilityCondition(m_inFlight);
		}
	}

	private void SendStateMessage(bool _inFlight, GameObject _thrower)
	{
		m_data.Initialise(_inFlight, _thrower);
		SendServerEvent(m_data);
	}

	protected virtual void Awake()
	{
		if (ms_AttachmentsLayer == 0)
		{
			ms_AttachmentsLayer = LayerMask.GetMask("Attachments");
		}
		m_Collider = GetComponent<Collider>();
		m_Transform = base.transform;
		m_OnAttachChanged = OnAttachChanged;
	}

	public void RegisterLandedCallback(GenericVoid<GameObject> _callback)
	{
		m_landedCallback = (GenericVoid<GameObject>)Delegate.Combine(m_landedCallback, _callback);
	}

	public void UnregisterLandedCallback(GenericVoid<GameObject> _callback)
	{
		m_landedCallback = (GenericVoid<GameObject>)Delegate.Remove(m_landedCallback, _callback);
	}

	public void RegisterCanThrowCallback(Generic<bool> _callback)
	{
		m_canThrowCallbacks.Add(_callback);
	}

	public void UnregisterCanThrowCallback(Generic<bool> _callback)
	{
		m_canThrowCallbacks.Remove(_callback);
	}

	public bool CanHandleThrow(IThrower _thrower, Vector2 _directionXZ)
	{
		return !m_canThrowCallbacks.CallForResult(false);
	}

	public void HandleThrow(IThrower _thrower, Vector2 _directionXZ)
	{
		_thrower.ThrowItem(base.gameObject, _directionXZ);
		m_thrower = _thrower;
		m_previousThrower = _thrower;
		if (m_attachment == null)
		{
			m_attachment = base.gameObject.RequestInterface<IAttachment>();
		}
		if (m_attachment != null)
		{
			m_attachment.RegisterAttachChangedCallback(m_OnAttachChanged);
		}
		if (m_pickupReferral != null)
		{
			m_pickupReferral.SetHandlePickupReferree(this);
		}
		m_flying = true;
		SendStateMessage(m_flying, ((MonoBehaviour)_thrower).gameObject);
		ResumeAndClearThrowStartCollisions();
		m_ignoredCollidersCount = Physics.OverlapBoxNonAlloc(m_Transform.position, m_Collider.bounds.extents, m_ThrowStartColliders, m_Transform.rotation, ms_AttachmentsLayer);
		for (int i = 0; i < m_ignoredCollidersCount; i++)
		{
			Physics.IgnoreCollision(m_Collider, m_ThrowStartColliders[i], true);
		}
	}

	private void ResumeAndClearThrowStartCollisions()
	{
		for (int i = 0; i < m_ignoredCollidersCount; i++)
		{
			Collider collider = m_ThrowStartColliders[i];
			if (collider != null && collider.gameObject != null)
			{
				Physics.IgnoreCollision(m_Collider, collider, false);
				m_ThrowStartColliders[i] = null;
			}
		}
		m_ignoredCollidersCount = 0;
	}

	public override void UpdateSynchronising()
	{
		float deltaTime = TimeManager.GetDeltaTime(base.gameObject);
		if (IsFlying())
		{
			m_flightTimer += deltaTime;
			if (m_flightTimer >= 0.1f)
			{
				IAttachment component = GetComponent<IAttachment>();
				if (component.AccessMotion().GetVelocity().sqrMagnitude < 0.0225f)
				{
					EndFlight();
				}
				if (m_flightTimer >= 5f)
				{
					EndFlight();
				}
				ResumeAndClearThrowStartCollisions();
			}
		}
		else
		{
			m_throwerTimer -= deltaTime;
			if (m_throwerTimer <= 0f)
			{
				m_previousThrower = null;
			}
		}
	}

	private void OnCollisionEnter(Collision _collision)
	{
		if (!IsFlying())
		{
			return;
		}
		IThrower thrower = _collision.gameObject.RequestInterface<IThrower>();
		if (thrower == null || thrower != m_thrower)
		{
			Vector3 normal = _collision.contacts[0].normal;
			float num = Vector3.Angle(normal, Vector3.up);
			if (num <= 45f)
			{
				EndFlight();
				m_landedCallback(_collision.gameObject);
			}
		}
	}

	private void OnAttachChanged(IParentable _parentable)
	{
		if (_parentable != null)
		{
			EndFlight();
		}
	}

	public void EndFlight()
	{
		if (m_pickupReferral != null && m_pickupReferral.GetHandlePickupReferree() == this)
		{
			m_pickupReferral.SetHandlePickupReferree(null);
		}
		if (m_attachment != null)
		{
			m_attachment.UnregisterAttachChangedCallback(m_OnAttachChanged);
		}
		m_previousThrower = m_thrower;
		m_throwerTimer = m_throwableItem.m_throwerTimeout;
		m_thrower = null;
		m_flightTimer = 0f;
		m_flying = false;
		SendStateMessage(m_flying, null);
		ResumeAndClearThrowStartCollisions();
	}

	public bool IsFlying()
	{
		return m_flying;
	}

	public float GetFlightTime()
	{
		return m_flightTimer;
	}

	public IThrower GetThrower()
	{
		return m_thrower;
	}

	public IThrower GetPreviousThrower()
	{
		return m_previousThrower;
	}

	public bool CanHandlePickup(ICarrier _carrier)
	{
		return !m_flying;
	}

	public int GetPickupPriority()
	{
		return int.MinValue;
	}

	public void HandlePickup(ICarrier _carrier, Vector2 _directionXZ)
	{
	}
}
