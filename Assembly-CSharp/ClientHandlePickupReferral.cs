using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientHandlePickupReferral : ClientSynchroniserBase
{
	private HandlePickupReferral m_referral;

	private List<Generic<bool, ICarrier>> m_allowBlockingCallbacks = new List<Generic<bool, ICarrier>>();

	private IClientHandlePickup m_iHandlePickup;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_referral = (HandlePickupReferral)synchronisedObject;
		if (m_referral.m_pickupReferralObject != null)
		{
			m_iHandlePickup = m_referral.m_pickupReferralObject.RequireInterface<IClientHandlePickup>();
		}
	}

	public void SetHandlePickupReferree(IClientHandlePickup _iHandlePickup)
	{
		m_iHandlePickup = _iHandlePickup;
	}

	public IClientHandlePickup GetHandlePickupReferree()
	{
		return m_iHandlePickup;
	}

	public void RegisterAllowReferralBlock(Generic<bool, ICarrier> _callback)
	{
		m_allowBlockingCallbacks.Add(_callback);
	}

	public void UnregisterAllowReferralBlock(Generic<bool, ICarrier> _callback)
	{
		m_allowBlockingCallbacks.Remove(_callback);
	}

	public bool CanBeBlocked(ICarrier _carrier)
	{
		return !m_allowBlockingCallbacks.CallForResult(false, _carrier);
	}
}
