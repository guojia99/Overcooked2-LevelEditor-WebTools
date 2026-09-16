using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientPressureSwitchCosmeticDecisions : ClientSynchroniserBase
{
	private PressureSwitchCosmeticDecisions m_decisions;

	private ClientTriggerZone m_triggerZone;

	private float m_normalY;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_decisions = (PressureSwitchCosmeticDecisions)synchronisedObject;
		m_triggerZone = base.gameObject.RequireComponent<ClientTriggerZone>();
		m_normalY = m_decisions.m_buttonBit.transform.localPosition.y;
	}

	private void Update()
	{
		if (!(m_triggerZone == null))
		{
			if (m_triggerZone.IsOccupied())
			{
				m_decisions.m_buttonBit.material = m_decisions.m_occupiedMaterial;
				m_decisions.m_buttonBit.transform.localPosition = m_decisions.m_buttonBit.transform.localPosition.WithY(m_normalY + m_decisions.m_occupiedButtonVerticalOffset);
			}
			else
			{
				m_decisions.m_buttonBit.material = m_decisions.m_unoccuppiedMaterial;
				m_decisions.m_buttonBit.transform.localPosition = m_decisions.m_buttonBit.transform.localPosition.WithY(m_normalY);
			}
		}
	}
}
