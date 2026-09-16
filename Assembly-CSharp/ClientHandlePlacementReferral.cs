using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientHandlePlacementReferral : ClientSynchroniserBase
{
	private HandlePlacementReferral m_referral;

	private IClientHandlePlacement[] m_iHandlePlacements;

	private IClientHandlePlacement m_iHandlePlacement;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_referral = (HandlePlacementReferral)synchronisedObject;
		if (m_referral.m_placementReferralObject != null)
		{
			m_iHandlePlacements = ComponentCache<IClientHandlePlacement>.GetComponents(m_referral.m_placementReferralObject);
		}
	}

	public void SetHandlePlacementReferree(IClientHandlePlacement _iHandlePlacement)
	{
		m_iHandlePlacement = _iHandlePlacement;
	}

	public IClientHandlePlacement GetHandlePlacementReferree()
	{
		if (m_iHandlePlacement == null && m_iHandlePlacements != null)
		{
			return HandlePlacementUtils.GetHighestPriority(m_iHandlePlacements);
		}
		return m_iHandlePlacement;
	}
}
