using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientThrowableItem : ClientSynchroniserBase, IClientThrowable, IClientHandlePickup, IBaseHandlePickup
{
	private ThrowableItem m_throwableItem;

	private IClientThrower m_thrower;

	private bool m_isFlying;

	private ParticleSystem m_pfx;

	private ClientHandlePickupReferral m_pickupReferral;

	private List<Generic<bool>> m_canThrowCallbacks = new List<Generic<bool>>();

	public override EntityType GetEntityType()
	{
		return EntityType.ThrowableItem;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_throwableItem = (ThrowableItem)synchronisedObject;
		m_pickupReferral = base.gameObject.RequestComponent<ClientHandlePickupReferral>();
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		ThrowableItemMessage throwableItemMessage = (ThrowableItemMessage)serialisable;
		if (throwableItemMessage.m_inFlight)
		{
			IClientThrower thrower = throwableItemMessage.m_thrower.RequestInterface<IClientThrower>();
			StartFlight(thrower);
		}
		else
		{
			EndFlight();
		}
	}

	public void RegisterCanThrowCallback(Generic<bool> _callback)
	{
		m_canThrowCallbacks.Add(_callback);
	}

	public void UnregisterCanThrowCallback(Generic<bool> _callback)
	{
		m_canThrowCallbacks.Remove(_callback);
	}

	public bool CanHandleThrow(IClientThrower _thrower, Vector2 _directionXZ)
	{
		return !m_canThrowCallbacks.CallForResult(false);
	}

	public bool IsFlying()
	{
		return m_isFlying;
	}

	public IClientThrower GetThrower()
	{
		return m_thrower;
	}

	private void StartFlight(IClientThrower _thrower)
	{
		m_isFlying = true;
		m_thrower = _thrower;
		if (m_pickupReferral != null)
		{
			m_pickupReferral.SetHandlePickupReferree(this);
		}
		if (m_throwableItem.m_throwParticle != null)
		{
			Transform parent = NetworkUtils.FindVisualRoot(base.gameObject);
			GameObject gameObject = m_throwableItem.m_throwParticle.InstantiateOnParent(parent);
			m_pfx = gameObject.GetComponent<ParticleSystem>();
		}
	}

	private void EndFlight()
	{
		m_isFlying = false;
		m_thrower = null;
		if (m_pickupReferral != null && m_pickupReferral.GetHandlePickupReferree() == this)
		{
			m_pickupReferral.SetHandlePickupReferree(null);
		}
		if (m_pfx != null)
		{
			m_pfx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
		}
	}

	public bool CanHandlePickup(ICarrier _carrier)
	{
		return !m_isFlying;
	}

	public int GetPickupPriority()
	{
		return int.MinValue;
	}
}
