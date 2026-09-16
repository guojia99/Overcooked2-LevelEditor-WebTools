using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerHandlePlacementReferral : ServerSynchroniserBase
{
	private HandlePlacementReferral m_referral;

	private IHandlePlacement[] m_iHandlePlacements;

	private IHandlePlacement m_iHandlePlacement;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_referral = (HandlePlacementReferral)synchronisedObject;
		if (m_referral.m_placementReferralObject != null)
		{
			m_iHandlePlacements = ComponentCache<IHandlePlacement>.GetComponents(m_referral.m_placementReferralObject);
		}
	}

	public void SetHandlePlacementReferree(IHandlePlacement _iHandlePlacement)
	{
		m_iHandlePlacement = _iHandlePlacement;
	}

	public IHandlePlacement GetHandlePlacementReferree()
	{
		if (m_iHandlePlacement == null && m_iHandlePlacements != null)
		{
			return HandlePlacementUtils.GetHighestPriority(m_iHandlePlacements);
		}
		return m_iHandlePlacement;
	}
}
